using AttendanceSystem.DTOs;
using AttendanceSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Controllers.Api;

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/device-verification")]
public class AdminDeviceVerificationController : ControllerBase
{
    private readonly IWorkerDeviceVerificationService _service;

    public AdminDeviceVerificationController(IWorkerDeviceVerificationService service) => _service = service;

    [HttpGet("setting")]
    public async Task<IActionResult> GetSetting(CancellationToken ct)
        => Ok(ApiResponse<DeviceVerificationSettingResponse>.Ok(await _service.GetSettingAsync(ct)));

    [HttpPost("setting")]
    public async Task<IActionResult> SetSetting([FromBody] UpdateDeviceVerificationSettingRequest request, CancellationToken ct)
        => Ok(ApiResponse<DeviceVerificationSettingResponse>.Ok(await _service.SetSettingAsync(request.Enabled, ct), request.Enabled ? "Device verification enabled." : "Device verification disabled."));

    [HttpGet("employees/{employeeId:guid}/credential")]
    public async Task<IActionResult> GetCredentialStatus(Guid employeeId, CancellationToken ct)
        => Ok(ApiResponse<EmployeeDeviceStatusResponse>.Ok(await _service.GetEmployeeDeviceStatusAsync(employeeId, ct)));

    [HttpPost("employees/{employeeId:guid}/enrollment/start")]
    public async Task<IActionResult> StartEnrollment(Guid employeeId, CancellationToken ct)
    {
        var result = await _service.StartEnrollmentAsync(employeeId, ct);
        return result is null
            ? NotFound(ApiResponse<object>.Fail("EMPLOYEE_NOT_FOUND", "Employee was not found."))
            : Ok(ApiResponse<StartDeviceEnrollmentResponse>.Ok(result, "Device enrollment started."));
    }

    [HttpPost("employees/{employeeId:guid}/credential/revoke")]
    public async Task<IActionResult> Revoke(Guid employeeId, CancellationToken ct)
    {
        var revoked = await _service.RevokeCredentialAsync(employeeId, ct);
        return Ok(ApiResponse<object>.Ok(new { revoked }, revoked ? "Device credential revoked." : "No active device credential was found."));
    }
}
