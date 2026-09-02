namespace AISLiveTracking.API.Services;

public static class GeoCalculator
{
    public static double HaversineDistanceKm(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        const double earthRadiusKm = 6371.0;

        var lat1 = DegreesToRadians(latitude1);
        var lat2 = DegreesToRadians(latitude2);

        var deltaLat = DegreesToRadians(latitude2 - latitude1);
        var deltaLon = DegreesToRadians(longitude2 - longitude1);

        var a =
            Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
            Math.Cos(lat1) *
            Math.Cos(lat2) *
            Math.Sin(deltaLon / 2) *
            Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(
            Math.Sqrt(a),
            Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}