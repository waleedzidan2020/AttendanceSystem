using AttendanceSystem.DTOs;
namespace AttendanceSystem.Services;
public interface IAdminDashboardService { Task<DashboardResponse> GetAsync(CancellationToken ct = default); }
