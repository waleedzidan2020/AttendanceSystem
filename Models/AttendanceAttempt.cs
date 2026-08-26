using AttendanceSystem.Enums;
namespace AttendanceSystem.Models;
public class AttendanceAttempt
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid? WorkSiteId { get; set; }
    public WorkSite? WorkSite { get; set; }
    public string? SubmittedEmployeeCode { get; set; }
    public AttendanceAttemptType AttemptType { get; set; }
    public DateTime AttemptTimeUtc { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? AccuracyMeters { get; set; }
    public decimal? DistanceMeters { get; set; }
    public bool Accepted { get; set; }
    public AttendanceRejectReason RejectReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
