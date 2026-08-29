using System.ComponentModel.DataAnnotations;
using AttendanceSystem.Enums;
using Fido2NetLib;
using Fido2NetLib.Objects;

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

public record DeviceEnrollmentOptionsResponse(Guid ChallengeId, string EmployeeName, CredentialCreateOptions Options);

public class CompleteDeviceEnrollmentRequest
{
    [Required] public string EnrollmentToken { get; set; } = string.Empty;
    [Required] public Guid ChallengeId { get; set; }
    [Required] public AuthenticatorAttestationRawResponse Credential { get; set; } = null!;
}

public class DeviceAuthenticationOptionsRequest
{
    [Required, StringLength(50)] public string EmployeeCode { get; set; } = string.Empty;
    [Required] public AttendanceAttemptType AttemptType { get; set; }
}

public record DeviceAuthenticationOptionsResponse(bool Required, Guid? ChallengeId, AssertionOptions? Options);

public class CompleteDeviceAuthenticationRequest
{
    [Required, StringLength(50)] public string EmployeeCode { get; set; } = string.Empty;
    [Required] public AttendanceAttemptType AttemptType { get; set; }
    [Required] public Guid ChallengeId { get; set; }
    [Required] public AuthenticatorAssertionRawResponse Credential { get; set; } = null!;
}

public record CompleteDeviceAuthenticationResponse(string AttendanceAuthorization, DateTime ExpiresAtUtc);
