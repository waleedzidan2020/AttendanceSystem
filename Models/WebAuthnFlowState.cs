namespace AttendanceSystem.Models;

public class WebAuthnFlowState
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public Guid? EnrollmentAuthorizationId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
