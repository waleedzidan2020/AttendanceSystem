namespace AttendanceSystem.Models;
public class WorkSite
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int AllowedRadiusMeters { get; set; } = 100;
    public int MaxAllowedAccuracyMeters { get; set; } = 50;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public ICollection<AttendanceAttempt> AttendanceAttempts { get; set; } = new List<AttendanceAttempt>();
}
