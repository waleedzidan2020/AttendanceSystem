using AttendanceSystem.DTOs;
namespace AttendanceSystem.Services;
public interface IAttendanceService
{
    Task<ApiResponse<WorkerStatusResponse>> GetStatusAsync(string employeeCode, CancellationToken ct = default);
    Task<ApiResponse<AttendanceOperationResponse>> CheckInAsync(CheckInRequest request, string? ip, string? userAgent, CancellationToken ct = default);
    Task<ApiResponse<AttendanceOperationResponse>> CheckOutAsync(CheckOutRequest request, string? ip, string? userAgent, CancellationToken ct = default);
}
