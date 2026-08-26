using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AttendanceSystem.Controllers.Api;
[ApiController,Authorize(Roles="Admin"),Route("api/admin/reports")]
public class ReportsController:ControllerBase
{
    private readonly ApplicationDbContext _db; public ReportsController(ApplicationDbContext db)=>_db=db;
    [HttpGet("attendance-summary")]
    public async Task<IActionResult> Summary([FromQuery]DateOnly? from,[FromQuery]DateOnly? to,[FromQuery]Guid? siteId,[FromQuery]Guid? employeeId,CancellationToken ct=default)
    {
        var start=from??DateOnly.FromDateTime(DateTime.UtcNow);var end=to??start;var q=_db.AttendanceRecords.AsNoTracking().Where(x=>x.AttendanceDate>=start&&x.AttendanceDate<=end);if(siteId.HasValue)q=q.Where(x=>x.WorkSiteId==siteId);if(employeeId.HasValue)q=q.Where(x=>x.EmployeeId==employeeId);
        var totalPresent=await q.CountAsync(ct);var totalLate=await q.CountAsync(x=>x.IsLate,ct);var employees=await _db.Employees.CountAsync(x=>x.IsActive&&(!siteId.HasValue||x.WorkSiteId==siteId)&&(!employeeId.HasValue||x.Id==employeeId),ct);
        var rejectedStart=start.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc);var rejectedEnd=end.AddDays(1).ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc);var rejectedQ=_db.AttendanceAttempts.AsNoTracking().Where(x=>!x.Accepted&&x.AttemptTimeUtc>=rejectedStart&&x.AttemptTimeUtc<rejectedEnd);if(siteId.HasValue)rejectedQ=rejectedQ.Where(x=>x.WorkSiteId==siteId);if(employeeId.HasValue)rejectedQ=rejectedQ.Where(x=>x.EmployeeId==employeeId);var rejected=await rejectedQ.CountAsync(ct);
        var days=end.DayNumber-start.DayNumber+1;var absent=Math.Max(0,employees*days-totalPresent);return Ok(ApiResponse<ReportSummaryResponse>.Ok(new(employees,totalPresent,totalLate,absent,rejected)));
    }
}
