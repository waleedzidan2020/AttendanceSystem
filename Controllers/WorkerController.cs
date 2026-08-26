using Microsoft.AspNetCore.Mvc;
namespace AttendanceSystem.Controllers;
public class WorkerController : Controller { [HttpGet("/worker/checkin")] public IActionResult CheckIn() => View(); }
