using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Services;

public class AdminCleanupService : IAdminCleanupService
{
    private readonly ApplicationDbContext _db;

    public AdminCleanupService(ApplicationDbContext db) => _db = db;

    public async Task<AdminCleanupResponse> DeleteTodayAsync(CancellationToken ct = default)
    {
        var egyptTimeZone = GetEgyptTimeZone();
        var egyptNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptTimeZone);
        var egyptDate = DateOnly.FromDateTime(egyptNow);

        var startLocal = DateTime.SpecifyKind(egyptNow.Date, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, egyptTimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, egyptTimeZone);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var deletedRejectedAttempts = await _db.AttendanceAttempts
                .Where(x => !x.Accepted && x.AttemptTimeUtc >= startUtc && x.AttemptTimeUtc < endUtc)
                .ExecuteDeleteAsync(ct);

            // AttendanceDate is the application's business-day column and is indexed.
            // Using it keeps cleanup consistent with dashboard/date filters.
            var deletedAttendance = await _db.AttendanceRecords
                .Where(x => x.AttendanceDate == egyptDate)
                .ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);

            return new AdminCleanupResponse(deletedAttendance, deletedRejectedAttempts);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
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
