namespace AISLiveTracking.API.Services;

public class AnalyticsOptions
{
    public int GapThresholdMinutes { get; set; }

    public decimal StopSpeedKnots { get; set; }

    public int IdleMinimumMinutes { get; set; }
}