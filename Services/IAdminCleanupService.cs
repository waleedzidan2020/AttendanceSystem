using AttendanceSystem.DTOs;

namespace AttendanceSystem.Services;

public interface IAdminCleanupService
{
    Task<AdminCleanupResponse> DeleteTodayAsync(CancellationToken ct = default);
}
