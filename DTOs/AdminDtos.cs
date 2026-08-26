using System.ComponentModel.DataAnnotations;
namespace AttendanceSystem.DTOs;

public class AdminLoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}
public record AdminLoginResponse(string FullName);
public record DashboardResponse(int TotalEmployees, int PresentToday, int AbsentToday, int LateToday, int RejectedAttemptsToday, int CurrentlyCheckedIn);

public class EmployeeCreateRequest
{
    [Required, StringLength(50)] public string EmployeeCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string FullName { get; set; } = string.Empty;
    [StringLength(30)] public string? PhoneNumber { get; set; }
    [Required] public Guid WorkSiteId { get; set; }
    public bool IsActive { get; set; } = true;
}
public class EmployeeUpdateRequest : EmployeeCreateRequest { }
public record EmployeeListItemResponse(Guid Id, string EmployeeCode, string FullName, string? PhoneNumber, Guid WorkSiteId, string SiteName, bool IsActive);

public class WorkSiteCreateRequest
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Range(-90, 90)] public decimal Latitude { get; set; }
    [Range(-180, 180)] public decimal Longitude { get; set; }
    [Range(1, 100000)] public int AllowedRadiusMeters { get; set; } = 100;
    [Range(1, 100000)] public int MaxAllowedAccuracyMeters { get; set; } = 50;
    public bool IsActive { get; set; } = true;
}
public class WorkSiteUpdateRequest : WorkSiteCreateRequest { }
public record WorkSiteListItemResponse(Guid Id, string Name, string? Description, decimal Latitude, decimal Longitude, int AllowedRadiusMeters, int MaxAllowedAccuracyMeters, bool IsActive);

public record ReportSummaryResponse(int TotalEmployees, int TotalPresent, int TotalLate, int TotalAbsent, int TotalRejectedAttempts);
