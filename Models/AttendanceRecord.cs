using AttendanceSystem.Enums;
namespace AttendanceSystem.Models;
public class AttendanceRecord
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public Guid WorkSiteId { get; set; }
    public WorkSite WorkSite { get; set; } = null!;
    public DateTime CheckInTimeUtc { get; set; }
    public decimal CheckInLatitude { get; set; }
    public decimal CheckInLongitude { get; set; }
    public decimal CheckInAccuracyMeters { get; set; }
    public decimal CheckInDistanceMeters { get; set; }
    public DateTime? CheckOutTimeUtc { get; set; }
    public decimal? CheckOutLatitude { get; set; }
    public decimal? CheckOutLongitude { get; set; }
    public decimal? CheckOutAccuracyMeters { get; set; }
    public decimal? CheckOutDistanceMeters { get; set; }
    public AttendanceStatus Status { get; set; }
    public bool IsLate { get; set; }
    public int LateMinutes { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
