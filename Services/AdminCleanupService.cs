using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Services;

public class AdminCleanupService : IAdminCleanupService
{
    private readonly ApplicationDbContext _db;

    public AdminCleanupService(ApplicationDbContext db) => _db = db;

    public async Task<AdminCleanupResponse> DeleteByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        if (date == DateOnly.MinValue)
            throw new ArgumentOutOfRangeException(nameof(date), "A valid cleanup date is required.");

        var egyptTimeZone = GetEgyptTimeZone();
        var startLocal = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(date.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var startUtc = ConvertLocalBoundaryToUtc(startLocal, egyptTimeZone);
        var endUtc = ConvertLocalBoundaryToUtc(endLocal, egyptTimeZone);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var deletedRejectedAttempts = await _db.AttendanceAttempts
                .Where(x => !x.Accepted && x.AttemptTimeUtc >= startUtc && x.AttemptTimeUtc < endUtc)
                .ExecuteDeleteAsync(ct);

            var deletedAttendance = await _db.AttendanceRecords
                .Where(x => x.AttendanceDate == date)
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

    public Task<AdminCleanupResponse> DeleteTodayAsync(CancellationToken ct = default)
    {
        var egyptTimeZone = GetEgyptTimeZone();
        var egyptNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptTimeZone);
        return DeleteByDateAsync(DateOnly.FromDateTime(egyptNow), ct);
    }

    private static DateTime ConvertLocalBoundaryToUtc(DateTime localTime, TimeZoneInfo timeZone)
    {
        while (timeZone.IsInvalidTime(localTime))
            localTime = localTime.AddMinutes(1);

        return TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);
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
