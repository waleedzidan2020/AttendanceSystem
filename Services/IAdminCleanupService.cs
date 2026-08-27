using AttendanceSystem.DTOs;

namespace AttendanceSystem.Services;

public interface IAdminCleanupService
{
    Task<AdminCleanupResponse> DeleteByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<AdminCleanupResponse> DeleteTodayAsync(CancellationToken ct = default);
}
