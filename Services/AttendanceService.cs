using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using AttendanceSystem.Enums;
using AttendanceSystem.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AttendanceSystem.Services;

public class AttendanceService : IAttendanceService
{
    private readonly ApplicationDbContext _db;
    private readonly IGeofenceService _geofence;
    private readonly IWorkerDeviceVerificationService _deviceVerification;
    private readonly ILogger<AttendanceService> _logger;
    public AttendanceService(ApplicationDbContext db, IGeofenceService geofence, IWorkerDeviceVerificationService deviceVerification, ILogger<AttendanceService> logger)
    { _db = db; _geofence = geofence; _deviceVerification = deviceVerification; _logger = logger; }

    public async Task<ApiResponse<WorkerStatusResponse>> GetStatusAsync(string employeeCode, CancellationToken ct = default)
    {
        var code = employeeCode.Trim();
        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeCode == code, ct);
        if (employee is null) return ApiResponse<WorkerStatusResponse>.Fail("EMPLOYEE_NOT_FOUND", "Employee code was not found.");
        if (!employee.IsActive) return ApiResponse<WorkerStatusResponse>.Fail("EMPLOYEE_INACTIVE", "Employee is inactive.");
        var open = await _db.AttendanceRecords.AsNoTracking().Include(x => x.WorkSite)
            .Where(x => x.EmployeeId == employee.Id && x.CheckOutTimeUtc == null)
            .OrderByDescending(x => x.CheckInTimeUtc).FirstOrDefaultAsync(ct);
        ActiveAttendanceDto? active = open is null ? null : new(open.Id, open.WorkSite.Name, open.CheckInTimeUtc);
        return ApiResponse<WorkerStatusResponse>.Ok(new(employee.EmployeeCode, employee.FullName, open is not null, active));
    }

    public Task<ApiResponse<AttendanceOperationResponse>> CheckInAsync(CheckInRequest request, string? ip, string? userAgent, CancellationToken ct = default)
        => ProcessAsync(request, AttendanceAttemptType.CheckIn, ip, userAgent, ct);

    public Task<ApiResponse<AttendanceOperationResponse>> CheckOutAsync(CheckOutRequest request, string? ip, string? userAgent, CancellationToken ct = default)
        => ProcessAsync(request, AttendanceAttemptType.CheckOut, ip, userAgent, ct);

    private async Task<ApiResponse<AttendanceOperationResponse>> ProcessAsync(CheckInRequest request, AttendanceAttemptType type, string? ip, string? userAgent, CancellationToken ct)
    {
        var existingAttempt = await _db.AttendanceAttempts.AsNoTracking().FirstOrDefaultAsync(x => x.RequestId == request.RequestId, ct);
        if (existingAttempt is not null)
            return ApiResponse<AttendanceOperationResponse>.Fail("DUPLICATE_REQUEST", "This request has already been processed.");

        var now = DateTime.UtcNow;
        var attempt = new AttendanceAttempt
        {
            Id = Guid.NewGuid(), RequestId = request.RequestId, SubmittedEmployeeCode = request.EmployeeCode.Trim(),
            AttemptType = type, AttemptTimeUtc = now, Latitude = request.Latitude, Longitude = request.Longitude,
            AccuracyMeters = request.Accuracy, Accepted = false, RejectReason = AttendanceRejectReason.None,
            IpAddress = ip is { Length: > 64 } ? ip[..64] : ip, UserAgent = userAgent is { Length: > 500 } ? userAgent[..500] : userAgent, CreatedAt = now
        };

        _db.AttendanceAttempts.Add(attempt);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _db.ChangeTracker.Clear();
            return ApiResponse<AttendanceOperationResponse>.Fail("DUPLICATE_REQUEST", "This request has already been processed.");
        }

        if (!ValidCoordinates(request))
            return await RejectAsync(attempt, AttendanceRejectReason.InvalidCoordinates, "INVALID_COORDINATES", "Coordinates are invalid.", ct);

        var employee = await _db.Employees.Include(x => x.WorkSite).FirstOrDefaultAsync(x => x.EmployeeCode == request.EmployeeCode.Trim(), ct);
        if (employee is null)
            return await RejectAsync(attempt, AttendanceRejectReason.EmployeeNotFound, "EMPLOYEE_NOT_FOUND", "Employee code was not found.", ct);
        attempt.EmployeeId = employee.Id;
        attempt.WorkSiteId = employee.WorkSiteId;
        if (!employee.IsActive)
            return await RejectAsync(attempt, AttendanceRejectReason.EmployeeInactive, "EMPLOYEE_INACTIVE", "Employee is inactive.", ct);
        if (!employee.WorkSite.IsActive)
            return await RejectAsync(attempt, AttendanceRejectReason.SiteInactive, "SITE_INACTIVE", "Work site is inactive.", ct);

        var deviceFailure = await _deviceVerification.ValidateAndConsumeAttendanceAuthorizationAsync(employee.Id, type, request.AttendanceAuthorization, ct);
        if (deviceFailure.HasValue)
        {
            var (code, message) = DeviceFailure(deviceFailure.Value);
            return await RejectAsync(attempt, deviceFailure.Value, code, message, ct);
        }

        if (request.Accuracy > employee.WorkSite.MaxAllowedAccuracyMeters)
        {
            var responseData = new AttendanceOperationResponse(Guid.Empty, employee.EmployeeCode, employee.FullName, employee.WorkSite.Name, now, null, null, 0, request.Accuracy, "Rejected", null, employee.WorkSite.MaxAllowedAccuracyMeters);
            await SaveRejectedAsync(attempt, AttendanceRejectReason.PoorLocationAccuracy, ct);
            return new(false, responseData, "Location accuracy is too low.", "POOR_LOCATION_ACCURACY");
        }

        var distance = _geofence.CalculateDistanceMeters(request.Latitude, request.Longitude, employee.WorkSite.Latitude, employee.WorkSite.Longitude);
        attempt.DistanceMeters = distance;
        if (distance > employee.WorkSite.AllowedRadiusMeters)
        {
            var responseData = new AttendanceOperationResponse(Guid.Empty, employee.EmployeeCode, employee.FullName, employee.WorkSite.Name, now, null, null, distance, request.Accuracy, "Rejected", employee.WorkSite.AllowedRadiusMeters, null);
            await SaveRejectedAsync(attempt, AttendanceRejectReason.OutsideGeofence, ct);
            return new(false, responseData, "You are outside the allowed work site.", "OUTSIDE_GEOFENCE");
        }

        if (type == AttendanceAttemptType.CheckIn)
            return await PerformCheckInAsync(employee, attempt, request, distance, now, ct);
        return await PerformCheckOutAsync(employee, attempt, request, distance, now, ct);
    }

    private async Task<ApiResponse<AttendanceOperationResponse>> PerformCheckInAsync(Employee employee, AttendanceAttempt attempt, CheckInRequest request, decimal distance, DateTime now, CancellationToken ct)
    {
        if (await _db.AttendanceRecords.AnyAsync(x => x.EmployeeId == employee.Id && x.CheckOutTimeUtc == null, ct))
            return await RejectAsync(attempt, AttendanceRejectReason.AlreadyCheckedIn, "ALREADY_CHECKED_IN", "Employee already has an active check-in.", ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(), EmployeeId = employee.Id, WorkSiteId = employee.WorkSiteId,
                CheckInTimeUtc = now, CheckInLatitude = request.Latitude, CheckInLongitude = request.Longitude,
                CheckInAccuracyMeters = request.Accuracy, CheckInDistanceMeters = distance,
                Status = AttendanceStatus.Present, IsLate = false, LateMinutes = 0,
                AttendanceDate = GetEgyptDate(now), CreatedAt = now
            };
            attempt.Accepted = true; attempt.RejectReason = AttendanceRejectReason.None;
            _db.AttendanceRecords.Add(record);
            await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            _logger.LogInformation("Check-in accepted for employee {EmployeeId} at site {SiteId}", employee.Id, employee.WorkSiteId);
            return ApiResponse<AttendanceOperationResponse>.Ok(new(record.Id, employee.EmployeeCode, employee.FullName, employee.WorkSite.Name, record.CheckInTimeUtc, null, null, distance, request.Accuracy, record.Status.ToString()), "Check-in successfully recorded.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await tx.RollbackAsync(ct); _db.ChangeTracker.Clear();
            var savedAttempt = await _db.AttendanceAttempts.FirstOrDefaultAsync(x => x.RequestId == attempt.RequestId, ct);
            if (savedAttempt is not null)
            {
                savedAttempt.Accepted = false;
                savedAttempt.RejectReason = AttendanceRejectReason.AlreadyCheckedIn;
                try { await _db.SaveChangesAsync(ct); } catch { }
            }
            return ApiResponse<AttendanceOperationResponse>.Fail("ALREADY_CHECKED_IN", "Employee already has an active check-in.");
        }
    }

    private async Task<ApiResponse<AttendanceOperationResponse>> PerformCheckOutAsync(Employee employee, AttendanceAttempt attempt, CheckInRequest request, decimal distance, DateTime now, CancellationToken ct)
    {
        var record = await _db.AttendanceRecords.FirstOrDefaultAsync(x => x.EmployeeId == employee.Id && x.CheckOutTimeUtc == null, ct);
        if (record is null)
            return await RejectAsync(attempt, AttendanceRejectReason.NoOpenCheckIn, "NO_ACTIVE_CHECKIN", "Employee does not have an active check-in.", ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        record.CheckOutTimeUtc = now; record.CheckOutLatitude = request.Latitude; record.CheckOutLongitude = request.Longitude;
        record.CheckOutAccuracyMeters = request.Accuracy; record.CheckOutDistanceMeters = distance; record.Status = AttendanceStatus.Completed; record.UpdatedAt = now;
        attempt.Accepted = true; attempt.RejectReason = AttendanceRejectReason.None;
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var worked = Math.Max(0, (int)Math.Round((now - record.CheckInTimeUtc).TotalMinutes));
        return ApiResponse<AttendanceOperationResponse>.Ok(new(record.Id, employee.EmployeeCode, employee.FullName, employee.WorkSite.Name, record.CheckInTimeUtc, now, worked, distance, request.Accuracy, record.Status.ToString()), "Check-out successfully recorded.");
    }

    private static DateOnly GetEgyptDate(DateTime utcNow)
    {
        var tz = GetEgyptTimeZone();
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz));
    }

    private static TimeZoneInfo GetEgyptTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
    }

    private static (string Code, string Message) DeviceFailure(AttendanceRejectReason reason) => reason switch
    {
        AttendanceRejectReason.DeviceVerificationRequired => ("DEVICE_VERIFICATION_REQUIRED", "Device verification is required."),
        AttendanceRejectReason.ExpiredAuthenticationChallenge => ("EXPIRED_AUTHENTICATION_CHALLENGE", "Device verification has expired."),
        AttendanceRejectReason.InvalidAuthenticationChallenge => ("INVALID_AUTHENTICATION_CHALLENGE", "Device verification has already been used or is invalid."),
        AttendanceRejectReason.DeviceCredentialRevoked => ("DEVICE_CREDENTIAL_REVOKED", "The registered device credential was revoked."),
        _ => ("INVALID_DEVICE_CREDENTIAL", "The device verification is invalid.")
    };

    private static bool ValidCoordinates(CheckInRequest r) => r.Latitude is >= -90 and <= 90 && r.Longitude is >= -180 and <= 180 && r.Accuracy > 0;
    private async Task<ApiResponse<AttendanceOperationResponse>> RejectAsync(AttendanceAttempt attempt, AttendanceRejectReason reason, string code, string message, CancellationToken ct)
    { await SaveRejectedAsync(attempt, reason, ct); return ApiResponse<AttendanceOperationResponse>.Fail(code, message); }
    private async Task SaveRejectedAsync(AttendanceAttempt attempt, AttendanceRejectReason reason, CancellationToken ct)
    { attempt.Accepted = false; attempt.RejectReason = reason; await _db.SaveChangesAsync(ct); }
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;
}
