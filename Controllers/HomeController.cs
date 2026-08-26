using Microsoft.AspNetCore.Mvc;
namespace AttendanceSystem.Controllers;
public class HomeController : Controller { [HttpGet("/")] public IActionResult Index() => View(); }
