using AttendanceSystem.DTOs;
using AttendanceSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AttendanceSystem.Controllers.Api;
[ApiController, Authorize(Roles="Admin"), Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _service;
    public AdminDashboardController(IAdminDashboardService service)=>_service=service;
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct)=>Ok(ApiResponse<DashboardResponse>.Ok(await _service.GetAsync(ct)));
}
