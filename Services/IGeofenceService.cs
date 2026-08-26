namespace AttendanceSystem.Services;
public interface IGeofenceService
{
    decimal CalculateDistanceMeters(decimal lat1, decimal lon1, decimal lat2, decimal lon2);
}
