using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using AttendanceSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AttendanceSystem.Controllers.Api;
[ApiController, Authorize(Roles = "Admin"), Route("api/admin/employees")]
public class EmployeesController : ControllerBase
{
    private readonly ApplicationDbContext _db; public EmployeesController(ApplicationDbContext db) => _db = db;
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] Guid? siteId, [FromQuery] bool? isActive, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var q = _db.Employees.AsNoTracking().Include(x => x.WorkSite).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => x.EmployeeCode.Contains(search) || x.FullName.Contains(search));
        if (siteId.HasValue) q = q.Where(x => x.WorkSiteId == siteId); if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive);
        var total = await q.CountAsync(ct); var items = await q.OrderBy(x => x.EmployeeCode).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new EmployeeListItemResponse(x.Id, x.EmployeeCode, x.FullName, x.PhoneNumber, x.WorkSiteId, x.WorkSite.Name, x.IsActive)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResponse<EmployeeListItemResponse>>.Ok(new(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize))));
    }
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) { var x = await _db.Employees.AsNoTracking().Include(e => e.WorkSite).FirstOrDefaultAsync(e => e.Id == id, ct); return x is null ? NotFound() : Ok(ApiResponse<EmployeeListItemResponse>.Ok(new(x.Id, x.EmployeeCode, x.FullName, x.PhoneNumber, x.WorkSiteId, x.WorkSite.Name, x.IsActive))); }
    [HttpPost]
    public async Task<IActionResult> Create(EmployeeCreateRequest r, CancellationToken ct) { if (!await _db.WorkSites.AnyAsync(x => x.Id == r.WorkSiteId, ct)) return BadRequest(new ApiResponse<object>(false, null, "Work site not found.", "VALIDATION_ERROR")); if (await _db.Employees.AnyAsync(x => x.EmployeeCode == r.EmployeeCode, ct)) return Conflict(new ApiResponse<object>(false, null, "Employee code already exists.", "VALIDATION_ERROR")); var x = new Employee { Id = Guid.NewGuid(), EmployeeCode = r.EmployeeCode.Trim(), FullName = r.FullName.Trim(), PhoneNumber = r.PhoneNumber, WorkSiteId = r.WorkSiteId, IsActive = r.IsActive, CreatedAt = DateTime.UtcNow }; _db.Add(x); await _db.SaveChangesAsync(ct); return Ok(new ApiResponse<object>(true, new { id = x.Id }, "Employee created.")); }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, EmployeeUpdateRequest r, CancellationToken ct) { var x = await _db.Employees.FindAsync([id], ct); if (x is null) return NotFound(); if (await _db.Employees.AnyAsync(e => e.Id != id && e.EmployeeCode == r.EmployeeCode, ct)) return Conflict(new ApiResponse<object>(false, null, "Employee code already exists.", "VALIDATION_ERROR")); x.EmployeeCode = r.EmployeeCode.Trim(); x.FullName = r.FullName.Trim(); x.PhoneNumber = r.PhoneNumber; x.WorkSiteId = r.WorkSiteId; x.IsActive = r.IsActive; x.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct); return Ok(new ApiResponse<object>(true, null, "Employee updated.")); }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct) { var x = await _db.Employees.FindAsync([id], ct); if (x is null) return NotFound(); x.IsActive = false; x.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct); return Ok(new ApiResponse<object>(true, null, "Employee disabled.")); }
}
