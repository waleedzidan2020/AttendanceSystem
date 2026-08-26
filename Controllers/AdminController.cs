using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AttendanceSystem.Controllers;
[Authorize(Roles="Admin")]
public class AdminController : Controller
{
    [AllowAnonymous, HttpGet("/admin/login")] public IActionResult Login() => User.Identity?.IsAuthenticated == true ? Redirect("/admin/dashboard") : View();
    [HttpGet("/admin/dashboard")] public IActionResult Dashboard() => View();
    [HttpGet("/admin/employees")] public IActionResult Employees() => View();
    [HttpGet("/admin/sites")] public IActionResult Sites() => View();
    [HttpGet("/admin/attendance")] public IActionResult Attendance() => View();
    [HttpGet("/admin/rejected-attempts")] public IActionResult RejectedAttempts() => View();
    [HttpGet("/admin/reports")] public IActionResult Reports() => View();
}
