using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly ApplicationDbContext _db;

    public AdminDashboardService(ApplicationDbContext db) => _db = db;

    public async Task<DashboardResponse> GetAsync(CancellationToken ct = default)
    {
        var egyptTimeZone = GetEgyptTimeZone();
        var egyptNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptTimeZone);
        var today = DateOnly.FromDateTime(egyptNow);

        var startLocal = DateTime.SpecifyKind(egyptNow.Date, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, egyptTimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, egyptTimeZone);

        var total = await _db.Employees.CountAsync(x => x.IsActive, ct);
        var present = await _db.AttendanceRecords
            .Where(x => x.AttendanceDate == today)
            .Select(x => x.EmployeeId)
            .Distinct()
            .CountAsync(ct);
        var late = await _db.AttendanceRecords
            .Where(x => x.AttendanceDate == today && x.IsLate)
            .Select(x => x.EmployeeId)
            .Distinct()
            .CountAsync(ct);
        var rejected = await _db.AttendanceAttempts.CountAsync(
            x => !x.Accepted && x.AttemptTimeUtc >= startUtc && x.AttemptTimeUtc < endUtc,
            ct);
        var checkedIn = await _db.AttendanceRecords.CountAsync(x => x.CheckOutTimeUtc == null, ct);

        return new(total, present, Math.Max(0, total - present), late, rejected, checkedIn);
    }

    private static TimeZoneInfo GetEgyptTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
    }
}
