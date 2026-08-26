using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using AttendanceSystem.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AttendanceSystem.Controllers.Api;
[ApiController,Authorize(Roles="Admin"),Route("api/admin/attendance")]
public class AdminAttendanceController:ControllerBase
{
    private readonly ApplicationDbContext _db; public AdminAttendanceController(ApplicationDbContext db)=>_db=db;
    [HttpGet]
    public async Task<IActionResult> List([FromQuery]DateOnly? date,[FromQuery]Guid? employeeId,[FromQuery]string? employeeCode,[FromQuery]Guid? siteId,[FromQuery]string? status,[FromQuery]int page=1,[FromQuery]int pageSize=25,CancellationToken ct=default)
    {
        page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var q=_db.AttendanceRecords.AsNoTracking().Include(x=>x.Employee).Include(x=>x.WorkSite).AsQueryable();
        if(date.HasValue)q=q.Where(x=>x.AttendanceDate==date);if(employeeId.HasValue)q=q.Where(x=>x.EmployeeId==employeeId);if(!string.IsNullOrWhiteSpace(employeeCode))q=q.Where(x=>x.Employee.EmployeeCode==employeeCode.Trim());if(siteId.HasValue)q=q.Where(x=>x.WorkSiteId==siteId);
        if(!string.IsNullOrWhiteSpace(status)&&Enum.TryParse<AttendanceStatus>(status,true,out var s))q=q.Where(x=>x.Status==s);
        var total=await q.CountAsync(ct);var items=await q.OrderByDescending(x=>x.CheckInTimeUtc).Skip((page-1)*pageSize).Take(pageSize)
            .Select(x=>new AttendanceListItemResponse(x.Id,x.EmployeeId,x.Employee.EmployeeCode,x.Employee.FullName,x.WorkSiteId,x.WorkSite.Name,x.CheckInTimeUtc,x.CheckOutTimeUtc,x.CheckInDistanceMeters,x.CheckInAccuracyMeters,x.Status.ToString(),x.IsLate,x.LateMinutes)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResponse<AttendanceListItemResponse>>.Ok(new(items,page,pageSize,total,(int)Math.Ceiling(total/(double)pageSize))));
    }
    [HttpGet("rejected-attempts")]
    public async Task<IActionResult> Rejected([FromQuery]DateOnly? date,[FromQuery]Guid? employeeId,[FromQuery]Guid? siteId,[FromQuery]string? reason,[FromQuery]int page=1,[FromQuery]int pageSize=25,CancellationToken ct=default)
    {
        page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var q=_db.AttendanceAttempts.AsNoTracking().Include(x=>x.Employee).Include(x=>x.WorkSite).Where(x=>!x.Accepted);
        if(date.HasValue){var start=date.Value.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc);var end=start.AddDays(1);q=q.Where(x=>x.AttemptTimeUtc>=start&&x.AttemptTimeUtc<end);}if(employeeId.HasValue)q=q.Where(x=>x.EmployeeId==employeeId);if(siteId.HasValue)q=q.Where(x=>x.WorkSiteId==siteId);
        if(!string.IsNullOrWhiteSpace(reason)&&Enum.TryParse<AttendanceRejectReason>(reason,true,out var rr))q=q.Where(x=>x.RejectReason==rr);
        var total=await q.CountAsync(ct);var items=await q.OrderByDescending(x=>x.AttemptTimeUtc).Skip((page-1)*pageSize).Take(pageSize).Select(x=>new RejectedAttemptResponse(x.Id,x.Employee!=null?x.Employee.FullName:null,x.SubmittedEmployeeCode,x.AttemptTimeUtc,x.AttemptType.ToString(),x.WorkSite!=null?x.WorkSite.Name:null,x.DistanceMeters,x.AccuracyMeters,x.RejectReason.ToString(),x.IpAddress,x.UserAgent)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResponse<RejectedAttemptResponse>>.Ok(new(items,page,pageSize,total,(int)Math.Ceiling(total/(double)pageSize))));
    }
}
