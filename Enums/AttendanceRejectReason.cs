namespace AttendanceSystem.Enums;
public enum AttendanceRejectReason
{
    None = 0,
    EmployeeNotFound = 1,
    EmployeeInactive = 2,
    SiteInactive = 3,
    OutsideGeofence = 4,
    PoorLocationAccuracy = 5,
    AlreadyCheckedIn = 6,
    NoOpenCheckIn = 7,
    InvalidCoordinates = 8,
    DuplicateRequest = 9
}
