using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using AttendanceSystem.Enums;
using AttendanceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Services;

public class WorkerDeviceVerificationService : IWorkerDeviceVerificationService
{
    private const int EnrollmentLifetimeMinutes = 10;
    private const int ChallengeLifetimeMinutes = 2;
    private const int AttendanceAuthorizationLifetimeMinutes = 2;
    private const string EnrollmentPurpose = "Registration";
    private const string AuthenticationPurposePrefix = "Authentication:";
    private const string DeviceCredentialType = "webcrypto-p256";
    private const string DeviceAlgorithm = "ECDSA-P256-SHA256";

    private readonly ApplicationDbContext _db;
    private readonly ILogger<WorkerDeviceVerificationService> _logger;

    public WorkerDeviceVerificationService(ApplicationDbContext db, ILogger<WorkerDeviceVerificationService> logger)
    {
        _db = db;
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
            .Where(x => x.EmployeeId == employeeId && x.CredentialType == DeviceCredentialType)
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
        if (!employee.IsActive)
            return ApiResponse<DeviceEnrollmentOptionsResponse>.Fail("EMPLOYEE_INACTIVE", "Employee is inactive.");

        var deviceId = Guid.NewGuid();
        var flow = new WebAuthnFlowState
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EnrollmentAuthorizationId = auth.Id,
            Purpose = EnrollmentPurpose,
            OptionsJson = JsonSerializer.Serialize(new EnrollmentFlowState(deviceId)),
            ExpiresAtUtc = now.AddMinutes(ChallengeLifetimeMinutes),
            CreatedAtUtc = now
        };
        _db.WebAuthnFlowStates.Add(flow);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<DeviceEnrollmentOptionsResponse>.Ok(new(flow.Id, employee.FullName, deviceId, DeviceAlgorithm));
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
            return ApiResponse<object>.Fail("INVALID_AUTHENTICATION_CHALLENGE", "The registration request is invalid.");
        if (flow.ExpiresAtUtc <= now)
            return ApiResponse<object>.Fail("EXPIRED_AUTHENTICATION_CHALLENGE", "The registration request has expired.");

        EnrollmentFlowState? enrollmentState;
        try
        {
            enrollmentState = JsonSerializer.Deserialize<EnrollmentFlowState>(flow.OptionsJson);
        }
        catch (JsonException)
        {
            enrollmentState = null;
        }

        if (enrollmentState is null || enrollmentState.DeviceId != request.DeviceId)
            return ApiResponse<object>.Fail("INVALID_DEVICE_CREDENTIAL", "The device registration does not match this request.");

        if (!TryNormalizePublicKey(request.PublicKey, out var publicKeyJson))
            return ApiResponse<object>.Fail("INVALID_DEVICE_PUBLIC_KEY", "The device public key is invalid.");

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
                CredentialId = request.DeviceId.ToByteArray(),
                PublicKey = Encoding.UTF8.GetBytes(publicKeyJson),
                UserHandle = Array.Empty<byte>(),
                SignCount = 0,
                CredentialType = DeviceCredentialType,
                IsActive = true,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            _logger.LogInformation("Browser device enrollment completed for employee {EmployeeId} and device {DeviceId}", auth.EmployeeId, request.DeviceId);
            return ApiResponse<object>.Ok(new { registered = true, deviceId = request.DeviceId }, "Device registered successfully.");
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
            return ApiResponse<DeviceAuthenticationOptionsResponse>.Ok(new(false, null, null, null, null));

        var code = request.EmployeeCode.Trim();
        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeCode == code, ct);
        if (employee is null)
            return ApiResponse<DeviceAuthenticationOptionsResponse>.Fail("EMPLOYEE_NOT_FOUND", "Employee code was not found.");
        if (!employee.IsActive)
            return ApiResponse<DeviceAuthenticationOptionsResponse>.Fail("EMPLOYEE_INACTIVE", "Employee is inactive.");

        var credential = await _db.EmployeeWebAuthnCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeId == employee.Id && x.IsActive && x.CredentialType == DeviceCredentialType, ct);
        if (credential is null || credential.CredentialId.Length != 16)
            return ApiResponse<DeviceAuthenticationOptionsResponse>.Fail("DEVICE_NOT_REGISTERED", "No active browser device credential is registered for this employee.");

        var deviceId = new Guid(credential.CredentialId);
        var flowId = Guid.NewGuid();
        var challenge = CreateSecureToken();
        var dataToSign = $"v1|{flowId:N}|{challenge}|{employee.Id:N}|{(int)request.AttemptType}|{deviceId:N}";
        var now = DateTime.UtcNow;

        var flow = new WebAuthnFlowState
        {
            Id = flowId,
            EmployeeId = employee.Id,
            Purpose = AuthenticationPurposePrefix + request.AttemptType,
            OptionsJson = JsonSerializer.Serialize(new AuthenticationFlowState(deviceId, dataToSign)),
            ExpiresAtUtc = now.AddMinutes(ChallengeLifetimeMinutes),
            CreatedAtUtc = now
        };
        _db.WebAuthnFlowStates.Add(flow);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<DeviceAuthenticationOptionsResponse>.Ok(new(true, flow.Id, deviceId, DeviceAlgorithm, dataToSign));
    }

    public async Task<ApiResponse<CompleteDeviceAuthenticationResponse>> CompleteAuthenticationAsync(CompleteDeviceAuthenticationRequest request, CancellationToken ct = default)
    {
        var setting = await GetOrCreateSettingAsync(ct);
        if (!setting.RequireWorkerDeviceVerification)
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("DEVICE_VERIFICATION_DISABLED", "Device verification is currently disabled.");

        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeCode == request.EmployeeCode.Trim(), ct);
        if (employee is null)
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("EMPLOYEE_NOT_FOUND", "Employee code was not found.");
        if (!employee.IsActive)
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("EMPLOYEE_INACTIVE", "Employee is inactive.");

        var now = DateTime.UtcNow;
        var flow = await _db.WebAuthnFlowStates.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == request.ChallengeId &&
            x.EmployeeId == employee.Id &&
            x.Purpose == AuthenticationPurposePrefix + request.AttemptType, ct);
        if (flow is null || flow.ConsumedAtUtc is not null)
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("INVALID_AUTHENTICATION_CHALLENGE", "Authentication challenge is invalid.");
        if (flow.ExpiresAtUtc <= now)
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("EXPIRED_AUTHENTICATION_CHALLENGE", "Authentication challenge has expired.");

        AuthenticationFlowState? authenticationState;
        try
        {
            authenticationState = JsonSerializer.Deserialize<AuthenticationFlowState>(flow.OptionsJson);
        }
        catch (JsonException)
        {
            authenticationState = null;
        }

        if (authenticationState is null || authenticationState.DeviceId != request.DeviceId)
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("DEVICE_MISMATCH", "The registered device does not match this request.");

        var credential = await _db.EmployeeWebAuthnCredentials.FirstOrDefaultAsync(x =>
            x.EmployeeId == employee.Id &&
            x.IsActive &&
            x.CredentialType == DeviceCredentialType, ct);
        if (credential is null || credential.CredentialId.Length != 16 || !credential.CredentialId.SequenceEqual(request.DeviceId.ToByteArray()))
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("INVALID_DEVICE_CREDENTIAL", "The device is not registered for this employee.");

        if (!VerifyDeviceSignature(credential.PublicKey, authenticationState.DataToSign, request.Signature))
        {
            _logger.LogWarning("Browser device signature verification failed for employee {EmployeeId} and device {DeviceId}", employee.Id, request.DeviceId);
            return ApiResponse<CompleteDeviceAuthenticationResponse>.Fail("INVALID_DEVICE_SIGNATURE", "Device verification failed.");
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
            _logger.LogInformation("Browser device verification succeeded for employee {EmployeeId} and device {DeviceId}", employee.Id, request.DeviceId);
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
            .Where(x => x.EmployeeId == employeeId && x.IsActive && x.CredentialType == DeviceCredentialType)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.UpdatedAt, now), ct);
        if (updated == 0) return false;
        _logger.LogInformation("Browser device credential revoked for employee {EmployeeId}", employeeId);
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

    private static bool TryNormalizePublicKey(DevicePublicKeyJwk key, out string normalizedJson)
    {
        normalizedJson = string.Empty;
        if (!string.Equals(key.Kty, "EC", StringComparison.Ordinal) ||
            !string.Equals(key.Crv, "P-256", StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(key.D))
            return false;

        if (!TryBase64UrlDecode(key.X, out var x) || x.Length != 32 ||
            !TryBase64UrlDecode(key.Y, out var y) || y.Length != 32)
            return false;

        try
        {
            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = x, Y = y }
            });
            _ = ecdsa.ExportParameters(false);
        }
        catch (CryptographicException)
        {
            return false;
        }

        normalizedJson = JsonSerializer.Serialize(new { kty = "EC", crv = "P-256", x = key.X, y = key.Y });
        return true;
    }

    private static bool VerifyDeviceSignature(byte[] storedPublicKey, string dataToSign, string signatureText)
    {
        if (storedPublicKey.Length == 0 || storedPublicKey.Length > 2048 || dataToSign.Length > 2048)
            return false;
        if (!TryBase64UrlDecode(signatureText, out var signature) || signature.Length != 64)
            return false;

        try
        {
            var jwk = JsonSerializer.Deserialize<DevicePublicKeyJwk>(Encoding.UTF8.GetString(storedPublicKey));
            if (jwk is null || !TryBase64UrlDecode(jwk.X, out var x) || !TryBase64UrlDecode(jwk.Y, out var y) || x.Length != 32 || y.Length != 32)
                return false;

            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = x, Y = y }
            });
            return ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(dataToSign),
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or FormatException)
        {
            return false;
        }
    }

    private static bool TryBase64UrlDecode(string value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512) return false;
        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            bytes = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string CreateSecureToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed record EnrollmentFlowState(Guid DeviceId);
    private sealed record AuthenticationFlowState(Guid DeviceId, string DataToSign);
}
