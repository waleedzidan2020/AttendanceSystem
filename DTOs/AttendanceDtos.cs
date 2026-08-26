using System.ComponentModel.DataAnnotations;
namespace AttendanceSystem.DTOs;

public class CheckInRequest
{
    [Required] public Guid RequestId { get; set; }
    [Required, StringLength(50)] public string EmployeeCode { get; set; } = string.Empty;
    [Range(-90, 90)] public decimal Latitude { get; set; }
    [Range(-180, 180)] public decimal Longitude { get; set; }
    [Range(0.01, 100000)] public decimal Accuracy { get; set; }
}

public class CheckOutRequest : CheckInRequest { }

public record ActiveAttendanceDto(Guid AttendanceId, string SiteName, DateTime CheckInTimeUtc);
public record WorkerStatusResponse(string EmployeeCode, string EmployeeName, bool IsCheckedIn, ActiveAttendanceDto? ActiveAttendance);
public record AttendanceOperationResponse(
    Guid AttendanceId,
    string EmployeeCode,
    string EmployeeName,
    string SiteName,
    DateTime CheckInTimeUtc,
    DateTime? CheckOutTimeUtc,
    int? WorkedMinutes,
    decimal DistanceMeters,
    decimal AccuracyMeters,
    string Status,
    int? AllowedRadiusMeters = null,
    int? MaximumAllowedAccuracyMeters = null);

public record AttendanceListItemResponse(
    Guid AttendanceId,
    Guid EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    Guid SiteId,
    string SiteName,
    DateTime CheckInTimeUtc,
    DateTime? CheckOutTimeUtc,
    decimal CheckInDistanceMeters,
    decimal CheckInAccuracyMeters,
    string Status,
    bool IsLate,
    int LateMinutes);

public record RejectedAttemptResponse(
    Guid Id,
    string? EmployeeName,
    string? SubmittedEmployeeCode,
    DateTime AttemptTimeUtc,
    string AttemptType,
    string? SiteName,
    decimal? DistanceMeters,
    decimal? AccuracyMeters,
    string RejectReason,
    string? IpAddress,
    string? UserAgent);
