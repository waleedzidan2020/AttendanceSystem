using AttendanceSystem.Data;
using AttendanceSystem.DTOs;
using AttendanceSystem.Enums;
using Microsoft.EntityFrameworkCore;
namespace AttendanceSystem.Services;
public class AdminDashboardService : IAdminDashboardService
{
    private readonly ApplicationDbContext _db;
    public AdminDashboardService(ApplicationDbContext db) => _db = db;
    public async Task<DashboardResponse> GetAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var total = await _db.Employees.CountAsync(x => x.IsActive, ct);
        var present = await _db.AttendanceRecords.Where(x => x.AttendanceDate == today).Select(x => x.EmployeeId).Distinct().CountAsync(ct);
        var late = await _db.AttendanceRecords.Where(x => x.AttendanceDate == today && x.IsLate).Select(x => x.EmployeeId).Distinct().CountAsync(ct);
        var rejected = await _db.AttendanceAttempts.CountAsync(x => x.AttemptTimeUtc.Date == DateTime.UtcNow.Date && !x.Accepted, ct);
        var checkedIn = await _db.AttendanceRecords.CountAsync(x => x.CheckOutTimeUtc == null, ct);
        return new(total, present, Math.Max(0, total - present), late, rejected, checkedIn);
    }
}
