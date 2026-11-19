namespace AISLiveTracking.API.Services;

public static class PositionValidator
{
    public static bool IsValidMmsi(long mmsi)
    {
        return mmsi >= 100_000_000 &&
               mmsi <= 999_999_999;
    }

    public static bool IsUnavailableAisFix(
        double latitude,
        double longitude)
    {
        return latitude == 91 ||
               longitude == 181;
    }

    public static bool IsValidLatitude(double latitude)
    {
        return latitude >= -90 &&
               latitude <= 90;
    }

    public static bool IsValidLongitude(double longitude)
    {
        return longitude >= -180 &&
               longitude <= 180;
    }
}