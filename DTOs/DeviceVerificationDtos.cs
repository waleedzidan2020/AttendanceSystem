using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AttendanceSystem.Enums;

namespace AttendanceSystem.DTOs;

public record DeviceVerificationSettingResponse(bool Enabled);

public class UpdateDeviceVerificationSettingRequest
{
    public bool Enabled { get; set; }
}

public record EmployeeDeviceStatusResponse(bool Registered, bool Active, DateTime? LastUsedAt);

public record StartDeviceEnrollmentResponse(string EnrollmentToken, string EnrollmentUrl, DateTime ExpiresAtUtc);

public class DeviceEnrollmentOptionsRequest
{
    [Required] public string EnrollmentToken { get; set; } = string.Empty;
}

public record DeviceEnrollmentOptionsResponse(Guid ChallengeId, string EmployeeName, Guid DeviceId, string Algorithm);

public class DevicePublicKeyJwk
{
    [JsonPropertyName("kty")]
    [Required]
    public string Kty { get; set; } = string.Empty;

    [JsonPropertyName("crv")]
    [Required]
    public string Crv { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    [Required]
    public string X { get; set; } = string.Empty;

    [JsonPropertyName("y")]
    [Required]
    public string Y { get; set; } = string.Empty;

    [JsonPropertyName("d")]
    public string? D { get; set; }
}

public class CompleteDeviceEnrollmentRequest
{
    [Required] public string EnrollmentToken { get; set; } = string.Empty;
    [Required] public Guid ChallengeId { get; set; }
    [Required] public Guid DeviceId { get; set; }
    [Required] public DevicePublicKeyJwk PublicKey { get; set; } = null!;
}

public class DeviceAuthenticationOptionsRequest
{
    [Required, StringLength(50)] public string EmployeeCode { get; set; } = string.Empty;
    [Required] public AttendanceAttemptType AttemptType { get; set; }
}

public record DeviceAuthenticationOptionsResponse(
    bool Required,
    Guid? ChallengeId,
    Guid? DeviceId,
    string? Algorithm,
    string? DataToSign);

public class CompleteDeviceAuthenticationRequest
{
    [Required, StringLength(50)] public string EmployeeCode { get; set; } = string.Empty;
    [Required] public AttendanceAttemptType AttemptType { get; set; }
    [Required] public Guid ChallengeId { get; set; }
    [Required] public Guid DeviceId { get; set; }
    [Required] public string Signature { get; set; } = string.Empty;
}

public record CompleteDeviceAuthenticationResponse(string AttendanceAuthorization, DateTime ExpiresAtUtc);
