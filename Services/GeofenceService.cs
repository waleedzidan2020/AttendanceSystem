namespace AttendanceSystem.Services;
public class GeofenceService : IGeofenceService
{
    public decimal CalculateDistanceMeters(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        const double radius = 6371000d;
        static double Rad(double d) => d * Math.PI / 180d;
        var dLat = Rad((double)(lat2 - lat1));
        var dLon = Rad((double)(lon2 - lon1));
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad((double)lat1)) * Math.Cos(Rad((double)lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round((decimal)(radius * c), 2);
    }
}
