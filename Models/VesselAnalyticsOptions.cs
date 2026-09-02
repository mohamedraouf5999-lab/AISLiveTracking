namespace AISLiveTracking.API.Models;

public class VesselAnalyticsOptions
{
    public double GapThresholdHours { get; set; } = 1.0;

    public double StopSpeedKnots { get; set; } = 0.5;

    public double IdleMinimumMinutes { get; set; } = 30.0;
}