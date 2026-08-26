using AttendanceSystem.DTOs;
using AttendanceSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Controllers.Api;

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/cleanup")]
public class AdminCleanupController : ControllerBase
{
    private readonly IAdminCleanupService _service;

    public AdminCleanupController(IAdminCleanupService service) => _service = service;

    [HttpPost("today")]
    [HttpDelete("today")]
    public async Task<IActionResult> DeleteToday(CancellationToken ct)
    {
        var result = await _service.DeleteTodayAsync(ct);
        return Ok(ApiResponse<AdminCleanupResponse>.Ok(result, "Today's data was deleted successfully."));
    }
}
