using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using AttendanceSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AttendanceSystem.Controllers.Api;
[ApiController,Authorize(Roles="Admin"),Route("api/admin/sites")]
public class WorkSitesController:ControllerBase
{
    private readonly ApplicationDbContext _db; public WorkSitesController(ApplicationDbContext db)=>_db=db;
    [HttpGet] public async Task<IActionResult> List([FromQuery]int page=1,[FromQuery]int pageSize=100,CancellationToken ct=default){page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var q=_db.WorkSites.AsNoTracking();var total=await q.CountAsync(ct);var items=await q.OrderBy(x=>x.Name).Skip((page-1)*pageSize).Take(pageSize).Select(x=>new WorkSiteListItemResponse(x.Id,x.Name,x.Description,x.Latitude,x.Longitude,x.AllowedRadiusMeters,x.MaxAllowedAccuracyMeters,x.IsActive)).ToListAsync(ct);return Ok(ApiResponse<PagedResponse<WorkSiteListItemResponse>>.Ok(new(items,page,pageSize,total,(int)Math.Ceiling(total/(double)pageSize))));}
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id,CancellationToken ct){var x=await _db.WorkSites.AsNoTracking().FirstOrDefaultAsync(s=>s.Id==id,ct);return x is null?NotFound():Ok(ApiResponse<WorkSiteListItemResponse>.Ok(new(x.Id,x.Name,x.Description,x.Latitude,x.Longitude,x.AllowedRadiusMeters,x.MaxAllowedAccuracyMeters,x.IsActive)));}
    [HttpPost] public async Task<IActionResult>Create(WorkSiteCreateRequest r,CancellationToken ct){var x=new WorkSite{Id=Guid.NewGuid(),Name=r.Name.Trim(),Description=r.Description,Latitude=r.Latitude,Longitude=r.Longitude,AllowedRadiusMeters=r.AllowedRadiusMeters,MaxAllowedAccuracyMeters=r.MaxAllowedAccuracyMeters,IsActive=r.IsActive,CreatedAt=DateTime.UtcNow};_db.Add(x);await _db.SaveChangesAsync(ct);return Ok(new ApiResponse<object>(true,new{id=x.Id},"Site created."));}
    [HttpPut("{id:guid}")] public async Task<IActionResult>Update(Guid id,WorkSiteUpdateRequest r,CancellationToken ct){var x=await _db.WorkSites.FindAsync([id],ct);if(x is null)return NotFound();x.Name=r.Name.Trim();x.Description=r.Description;x.Latitude=r.Latitude;x.Longitude=r.Longitude;x.AllowedRadiusMeters=r.AllowedRadiusMeters;x.MaxAllowedAccuracyMeters=r.MaxAllowedAccuracyMeters;x.IsActive=r.IsActive;x.UpdatedAt=DateTime.UtcNow;await _db.SaveChangesAsync(ct);return Ok(new ApiResponse<object>(true,null,"Site updated."));}
    [HttpDelete("{id:guid}")] public async Task<IActionResult>Disable(Guid id,CancellationToken ct){var x=await _db.WorkSites.FindAsync([id],ct);if(x is null)return NotFound();x.IsActive=false;x.UpdatedAt=DateTime.UtcNow;await _db.SaveChangesAsync(ct);return Ok(new ApiResponse<object>(true,null,"Site disabled."));}
}
