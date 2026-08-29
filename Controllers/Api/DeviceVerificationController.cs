using AttendanceSystem.DTOs;
using AttendanceSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AttendanceSystem.Controllers.Api;

[ApiController, Route("api/device-verification")]
public class DeviceVerificationController : ControllerBase
{
    private readonly IWorkerDeviceVerificationService _service;

    public DeviceVerificationController(IWorkerDeviceVerificationService service) => _service = service;

    [HttpPost("enrollment/options"), EnableRateLimiting("worker-write")]
    public async Task<IActionResult> EnrollmentOptions([FromBody] DeviceEnrollmentOptionsRequest request, CancellationToken ct)
        => ToAction(await _service.CreateEnrollmentOptionsAsync(request.EnrollmentToken, ct));

    [HttpPost("enrollment/complete"), EnableRateLimiting("worker-write")]
    public async Task<IActionResult> CompleteEnrollment([FromBody] CompleteDeviceEnrollmentRequest request, CancellationToken ct)
        => ToAction(await _service.CompleteEnrollmentAsync(request, ct));

    [HttpPost("authentication/options"), EnableRateLimiting("worker-write")]
    public async Task<IActionResult> AuthenticationOptions([FromBody] DeviceAuthenticationOptionsRequest request, CancellationToken ct)
        => ToAction(await _service.CreateAuthenticationOptionsAsync(request, ct));

    [HttpPost("authentication/complete"), EnableRateLimiting("worker-write")]
    public async Task<IActionResult> CompleteAuthentication([FromBody] CompleteDeviceAuthenticationRequest request, CancellationToken ct)
        => ToAction(await _service.CompleteAuthenticationAsync(request, ct));

    private IActionResult ToAction<T>(ApiResponse<T> response) => response.Success ? Ok(response) : response.ErrorCode switch
    {
        "EMPLOYEE_NOT_FOUND" => NotFound(response),
        "INVALID_ENROLLMENT_TOKEN" or "INVALID_AUTHENTICATION_CHALLENGE" or "EXPIRED_AUTHENTICATION_CHALLENGE" => BadRequest(response),
        _ => BadRequest(response)
    };
}
