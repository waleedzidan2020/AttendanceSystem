using System.Security.Cryptography;
using System.Text;
using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using AttendanceSystem.Enums;
using AttendanceSystem.Models;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Services;

public class WorkerDeviceVerificationService : IWorkerDeviceVerificationService
{
    private const int EnrollmentLifetimeMinutes = 10;
    private const int ChallengeLifetimeMinutes = 2;
    private const int AttendanceAuthorizationLifetimeMinutes = 2;
    private const string EnrollmentPurpose = "Registration";
    private const string AuthenticationPurposePrefix = "Authentication:";

    private readonly ApplicationDbContext _db;
    private readonly IFido2 _fido2;
    private readonly ILogger<WorkerDeviceVerificationService> _logger;

    public WorkerDeviceVerificationService(ApplicationDbContext db, IFido2 fido2, ILogger<WorkerDeviceVerificationService> logger)
    {
        _db = db;
        _fido2 = fido2;
        _logger = logger;
    }

    public async Task<DeviceVerificationSettingResponse> GetSettingAsync(CancellationToken ct = default)
    {
        var setting = await GetOrCreateSettingAsync(ct);
        return new DeviceVerificationSettingResponse(setting.RequireWorkerDeviceVerification);
    }

    public async Task<DeviceVerificationSettingResponse> SetSettingAsync(bool enabled, CancellationToken ct = default)
    {
        var setting = await GetOrCreateSettingAsync(ct);
        setting.RequireWorkerDeviceVerification = enabled;
        setting.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new DeviceVerificationSettingResponse(enabled);
    }

    public async Task<EmployeeDeviceStatusResponse> GetEmployeeDeviceStatusAsync(Guid employeeId, CancellationToken ct = default)
    {
        var credential = await _db.EmployeeWebAuthnCredentials.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return credential is null
            ? new EmployeeDeviceStatusResponse(false, false, null)
            : new EmployeeDeviceStatusResponse(true, credential.IsActive, credential.LastUsedAt);
    }

    public async Task<StartDeviceEnrollmentResponse?> StartEnrollmentAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employeeExists = await _db.Employees.AsNoTracking().AnyAsync(x => x.Id == employeeId, ct);
        if (!employeeExists) return null;

        var now = DateTime.UtcNow;
        await CleanupExpiredStateAsync(now, ct);

        await _db.DeviceEnrollmentAuthorizations
            .Where(x => x.EmployeeId == employeeId && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ConsumedAtUtc, now), ct);

        var token = CreateSecureToken();
        var expires = now.AddMinutes(EnrollmentLifetimeMinutes);
        _db.DeviceEnrollmentAuthorizations.Add(new DeviceEnrollmentAuthorization
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            TokenHash = HashToken(token),
            ExpiresAtUtc = expires,
            CreatedAtUtc = now
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Device enrollment started for employee {EmployeeId}", employeeId);
        var enrollmentUrl = $"https://waleedzidan2020.github.io/workermanagement/device-enrollment.html?token={Uri.EscapeDataString(token)}";
        return new StartDeviceEnrollmentResponse(token, enrollmentUrl, expires);
    }

    public async Task<ApiResponse<DeviceEnrollmentOptionsResponse>> CreateEnrollmentOptionsAsync(string token, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var auth = await FindEnrollmentAuthorizationAsync(token, ct);
        if (auth is null || auth.ConsumedAtUtc is not null || auth.ExpiresAtUtc <= now)
            return ApiResponse<DeviceEnrollmentOptionsResponse>.Fail("INVALID_ENROLLMENT_TOKEN", "Enrollment authorization is invalid or expired.");

        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == auth.EmployeeId, ct);
        if (employee is null)
            return ApiResponse<DeviceEnrollmentOptionsResponse>.Fail("EMPLOYEE_NOT_FOUND", "Employee code was not found.");

        var existingKeys = await _db.EmployeeWebAuthnCredentials.AsNoTracking()
            .Where(x => x.EmployeeId == employee.Id && x.IsActive)
            .Select(x => x.CredentialId)
            .ToListAsync(ct);

        var user = new Fido2User
        {
            DisplayName = employee.FullName,
            Name = employee.EmployeeCode,
            Id = employee.Id.ToByteArray()
        };

        var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = user,
            ExcludeCredentials = existingKeys.Select(x => new PublicKeyCredentialDescriptor(x)).ToList(),
            AuthenticatorSelection = AuthenticatorSelection.Default,
            AttestationPreference = AttestationConveyancePreference.None,
            Extensions = new AuthenticationExtensionsClientInputs { CredProps = true }
        });

        var flow = new WebAuthnFlowState
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EnrollmentAuthorizationId = auth.Id,
            Purpose = EnrollmentPurpose,
            OptionsJson = options.ToJson(),
            ExpiresAtUtc = now.AddMinutes(ChallengeLifetimeMinutes),
            CreatedAtUtc = now
        };
        _db.WebAuthnFlowStates.Add(flow);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<DeviceEnrollmentOptionsResponse>.Ok(new(flow.Id, employee.FullName, options));
    }

    public async Task<ApiResponse<object>> CompleteEnrollmentAsync(CompleteDeviceEnrollmentRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var auth = await FindEnrollmentAuthorizationAsync(request.EnrollmentToken, ct);
        if (auth is null || auth.ConsumedAtUtc is not null || auth.ExpiresAtUtc <= now)
            return ApiResponse<object>.Fail("INVALID_ENROLLMENT_TOKEN", "Enrollment authorization is invalid or expired.");

        var flow = await _db.WebAuthnFlowStates.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == request.ChallengeId &&
            x.EmployeeId == auth.EmployeeId &&
            x.EnrollmentAuthorizationId == auth.Id &&
            x.Purpose == EnrollmentPurpose, ct);

        if (flow is null || flow.ConsumedAtUtc is not null)
            return ApiResponse<object>.Fail("INVALID_AUTHENTICATION_CHALLENGE", "The registration challenge is invalid.");
        if (flow.ExpiresAtUtc <= now)
            return ApiResponse<object>.Fail("EXPIRED_AUTHENTICATION_CHALLENGE", "The registration challenge has expired.");

        var options = CredentialCreateOptions.FromJson(flow.OptionsJson);
        IsCredentialIdUniqueToUserAsyncDelegate uniqueCallback = async (args, cancellationToken) =>
            !await _db.EmployeeWebAuthnCredentials.AsNoTracking()
                .AnyAsync(x => x.CredentialId.SequenceEqual(args.CredentialId), cancellationToken);

        RegisteredPublicKeyCredential verified;
        try
        {
            verified = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = request.Credential,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = uniqueCallback
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebAuthn registration verification failed for employee {EmployeeId}", auth.EmployeeId);
            return ApiResponse<object>.Fail("WEBAUTHN_VERIFICATION_FAILED", "Device registration verification failed.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var consumedFlow = await _db.WebAuthnFlowStates
                .Where(x => x.Id == flow.Id && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ConsumedAtUtc, now), ct);
            var consumedEnrollment = await _db.DeviceEnrollmentAuthorizations
                .Where(x => x.Id == auth.Id && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ConsumedAtUtc, now), ct);

            if (consumedFlow != 1 || consumedEnrollment != 1)
            {
                await transaction.RollbackAsync(ct);
                return ApiResponse<object>.Fail("INVALID_AUTHENTICATION_CHALLENGE", "The registration request has already been used or expired.");
            }

            await _db.EmployeeWebAuthnCredentials
                .Where(x => x.EmployeeId == auth.EmployeeId && x.IsActive)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.UpdatedAt, now), ct);

            _db.EmployeeWebAuthnCredentials.Add(new EmployeeWebAuthnCredential
            {
                Id = Guid.NewGuid(),
                EmployeeId = auth.EmployeeId,
                CredentialId = verified.Id,
                PublicKey = verified.PublicKey,
                UserHandle = verified.User.Id,
                SignCount = verified.SignCount,
                CredentialType = "public-key",
                IsActive = true,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            _logger.LogInformation("Device enrollment completed for employee {EmployeeId}", auth.EmployeeId);
            return ApiResponse<object>.Ok(new { registered = true }, "Device registered successfully.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ApiResponse<DeviceAuthenticationOptionsResponse>> CreateAuthenticationOptionsAsync(DeviceAuthenticationOptionsRequest request, CancellationToken ct = default)
    {
        var setting = await GetOrCreateSettingAsync(ct);
        if (!setting.RequireWorkerDeviceVerification)
            return ApiResponse<DeviceAuthenticationOptionsResponse>.Ok(new(false, null, null));

        var code = request.EmployeeCode.Trim();
        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeCode == code, ct);
        if (employee is null)
            return ApiResponse<DeviceAuthenticationOptionsResponse>.Fail("EMPLOYEE_NOT_FOUND", "Employee code was not found.");
        if (!employee.IsActive)
            return ApiResponse<DeviceAuthenticationOptionsResponse>.Fail("EMPLOYEE_INACTIVE", "Employee is inactive.");

        var credential = await _db.EmployeeWebAuthnCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeId == employee.Id && x.IsActive, ct);
        if (credential is null)
            return ApiResponse<DeviceAuthenticationOptionsResponse>.Fail("DEVICE_NOT_REGISTERED", "No active device credential is registered for this employee.");

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = new[] { new PublicKeyCredentialDescriptor(credential.CredentialId) },
            UserVerification = UserVerificationRequirement.Required
        });

        var now = DateTime.UtcNow;
        var flow = new WebAuthnFlowState
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Purpose = AuthenticationPurposePrefix + request.AttemptType,
            OptionsJson = options.ToJson(),
            ExpiresAtUtc = now.AddMinutes(ChallengeLifetimeMinutes),
            CreatedAtUtc = now
        };
        _db.WebAuthnFlowStates.Add(flow);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<DeviceAuthenticationOptionsResponse>.Ok(new(true, flow.Id, options));
    }

    public async Task<ApiResponse<CompleteDeviceAuthenticationResponse>> CompleteAuthenticationAsync(CompleteDeviceAuthenticationRequest request, CancellationToken ct = default)
    {
        var setting = await GetOrCreateSettingAsync(ct);
        if (!setting.RequireWorkerDeviceVerification)
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("DEVICE_VERIFICATION_DISABLED", "Device verification is currently disabled.");

        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeCode == request.EmployeeCode.Trim(), ct);
        if (employee is null)
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("EMPLOYEE_NOT_FOUND", "Employee code was not found.");

        var now = DateTime.UtcNow;
        var flow = await _db.WebAuthnFlowStates.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == request.ChallengeId &&
            x.EmployeeId == employee.Id &&
            x.Purpose == AuthenticationPurposePrefix + request.AttemptType, ct);
        if (flow is null || flow.ConsumedAtUtc is not null)
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("INVALID_AUTHENTICATION_CHALLENGE", "Authentication challenge is invalid.");
        if (flow.ExpiresAtUtc <= now)
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("EXPIRED_AUTHENTICATION_CHALLENGE", "Authentication challenge has expired.");

        var credential = await _db.EmployeeWebAuthnCredentials.FirstOrDefaultAsync(x => x.EmployeeId == employee.Id && x.IsActive, ct);
        if (credential is null || !credential.CredentialId.SequenceEqual(request.Credential.RawId))
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("INVALID_DEVICE_CREDENTIAL", "The credential is not registered for this employee.");

        var options = AssertionOptions.FromJson(flow.OptionsJson);
        IsUserHandleOwnerOfCredentialIdAsync ownerCallback = async (args, cancellationToken) =>
        {
            if (!args.CredentialId.SequenceEqual(credential.CredentialId)) return false;
            if (args.UserHandle is null || args.UserHandle.Length == 0) return true;
            return args.UserHandle.SequenceEqual(credential.UserHandle) &&
                   await _db.EmployeeWebAuthnCredentials.AsNoTracking().AnyAsync(x =>
                       x.Id == credential.Id && x.EmployeeId == employee.Id && x.IsActive, cancellationToken);
        };

        VerifyAssertionResult verified;
        try
        {
            verified = await _fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = request.Credential,
                OriginalOptions = options,
                StoredPublicKey = credential.PublicKey,
                StoredSignatureCounter = checked((uint)credential.SignCount),
                IsUserHandleOwnerOfCredentialIdCallback = ownerCallback
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebAuthn authentication verification failed for employee {EmployeeId}", employee.Id);
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("WEBAUTHN_VERIFICATION_FAILED", "Device verification failed.");
        }

        var attendanceToken = CreateSecureToken();
        var expires = now.AddMinutes(AttendanceAuthorizationLifetimeMinutes);
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var consumedFlow = await _db.WebAuthnFlowStates
                .Where(x => x.Id == flow.Id && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ConsumedAtUtc, now), ct);
            if (consumedFlow != 1)
            {
                await transaction.RollbackAsync(ct);
                return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("INVALID_AUTHENTICATION_CHALLENGE", "Authentication challenge has already been used or expired.");
            }

            credential.SignCount = verified.SignCount;
            credential.LastUsedAt = now;
            credential.UpdatedAt = now;
            _db.AttendanceAuthorizations.Add(new AttendanceAuthorization
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                AttemptType = request.AttemptType,
                TokenHash = HashToken(attendanceToken),
                ExpiresAtUtc = expires,
                CreatedAtUtc = now
            });
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            _logger.LogInformation("WebAuthn verification succeeded for employee {EmployeeId}", employee.Id);
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Ok(new(attendanceToken, expires));
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> RevokeCredentialAsync(Guid employeeId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var updated = await _db.EmployeeWebAuthnCredentials
            .Where(x => x.EmployeeId == employeeId && x.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.UpdatedAt, now), ct);
        if (updated == 0) return false;
        _logger.LogInformation("Device credential revoked for employee {EmployeeId}", employeeId);
        return true;
    }

    public async Task<AttendanceRejectReason?> ValidateAndConsumeAttendanceAuthorizationAsync(Guid employeeId, AttendanceAttemptType attemptType, string? token, CancellationToken ct = default)
    {
        var setting = await GetOrCreateSettingAsync(ct);
        if (!setting.RequireWorkerDeviceVerification) return null;
        if (string.IsNullOrWhiteSpace(token)) return AttendanceRejectReason.DeviceVerificationRequired;

        var now = DateTime.UtcNow;
        var hash = HashToken(token);
        var authorization = await _db.AttendanceAuthorizations.AsNoTracking().FirstOrDefaultAsync(x =>
            x.TokenHash == hash && x.EmployeeId == employeeId && x.AttemptType == attemptType, ct);
        if (authorization is null) return AttendanceRejectReason.InvalidDeviceCredential;
        if (authorization.ConsumedAtUtc is not null) return AttendanceRejectReason.InvalidAuthenticationChallenge;
        if (authorization.ExpiresAtUtc <= now) return AttendanceRejectReason.ExpiredAuthenticationChallenge;

        var consumed = await _db.AttendanceAuthorizations
            .Where(x => x.Id == authorization.Id && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ConsumedAtUtc, now), ct);
        return consumed == 1 ? null : AttendanceRejectReason.InvalidAuthenticationChallenge;
    }

    private async Task<SystemSetting> GetOrCreateSettingAsync(CancellationToken ct)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (setting is not null) return setting;
        setting = new SystemSetting { Id = 1, RequireWorkerDeviceVerification = false, UpdatedAt = DateTime.UtcNow };
        _db.SystemSettings.Add(setting);
        await _db.SaveChangesAsync(ct);
        return setting;
    }

    private async Task<DeviceEnrollmentAuthorization?> FindEnrollmentAuthorizationAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = HashToken(token);
        return await _db.DeviceEnrollmentAuthorizations.AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
    }

    private async Task CleanupExpiredStateAsync(DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-1);
        await _db.WebAuthnFlowStates.Where(x => x.ExpiresAtUtc < cutoff).ExecuteDeleteAsync(ct);
        await _db.AttendanceAuthorizations.Where(x => x.ExpiresAtUtc < cutoff).ExecuteDeleteAsync(ct);
        await _db.DeviceEnrollmentAuthorizations.Where(x => x.ExpiresAtUtc < cutoff).ExecuteDeleteAsync(ct);
    }

    private static string CreateSecureToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
