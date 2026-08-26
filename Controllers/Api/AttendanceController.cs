using AttendanceSystem.DTOs;
using AttendanceSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace AttendanceSystem.Controllers.Api;
[ApiController, Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _service;
    public AttendanceController(IAttendanceService service) => _service = service;

    [HttpGet("status"), EnableRateLimiting("worker-status")]
    public async Task<IActionResult> Status([FromQuery] string employeeCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(employeeCode)) return BadRequest(new ApiResponse<object>(false, null, "Employee code is required.", "VALIDATION_ERROR"));
        var r = await _service.GetStatusAsync(employeeCode, ct); return ToAction(r);
    }

    [HttpPost("checkin"), EnableRateLimiting("worker-write")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request, CancellationToken ct)
        => ToAction(await _service.CheckInAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct));

    [HttpPost("checkout"), EnableRateLimiting("worker-write")]
    public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest request, CancellationToken ct)
        => ToAction(await _service.CheckOutAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct));

    private IActionResult ToAction<T>(ApiResponse<T> r) => r.Success ? Ok(r) : r.ErrorCode switch
    {
        "EMPLOYEE_NOT_FOUND" => NotFound(r),
        "ALREADY_CHECKED_IN" or "NO_ACTIVE_CHECKIN" or "DUPLICATE_REQUEST" => Conflict(r),
        _ => BadRequest(r)
    };
}
