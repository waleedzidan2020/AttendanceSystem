using AttendanceSystem.DTOs;
using AttendanceSystem.Enums;

namespace AttendanceSystem.Services;

public interface IWorkerDeviceVerificationService
{
    Task<DeviceVerificationSettingResponse> GetSettingAsync(CancellationToken ct = default);
    Task<DeviceVerificationSettingResponse> SetSettingAsync(bool enabled, CancellationToken ct = default);
    Task<EmployeeDeviceStatusResponse> GetEmployeeDeviceStatusAsync(Guid employeeId, CancellationToken ct = default);
    Task<StartDeviceEnrollmentResponse?> StartEnrollmentAsync(Guid employeeId, CancellationToken ct = default);
    Task<ApiResponse<DeviceEnrollmentOptionsResponse>> CreateEnrollmentOptionsAsync(string token, CancellationToken ct = default);
    Task<ApiResponse<object>> CompleteEnrollmentAsync(CompleteDeviceEnrollmentRequest request, CancellationToken ct = default);
    Task<ApiResponse<DeviceAuthenticationOptionsResponse>> CreateAuthenticationOptionsAsync(DeviceAuthenticationOptionsRequest request, CancellationToken ct = default);
    Task<ApiResponse<CompleteDeviceAuthenticationResponse>> CompleteAuthenticationAsync(CompleteDeviceAuthenticationRequest request, CancellationToken ct = default);
    Task<bool> RevokeCredentialAsync(Guid employeeId, CancellationToken ct = default);
    Task<AttendanceRejectReason?> ValidateAndConsumeAttendanceAuthorizationAsync(Guid employeeId, AttendanceAttemptType attemptType, string? token, CancellationToken ct = default);
}
