namespace AttendanceSystem.Models;

public class SystemSetting
{
    public int Id { get; set; } = 1;
    public bool RequireWorkerDeviceVerification { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
