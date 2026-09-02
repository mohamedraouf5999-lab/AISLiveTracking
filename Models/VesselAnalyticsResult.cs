namespace AISLiveTracking.API.Models;

public class VesselAnalyticsResult
{
    public long Mmsi { get; set; }

    public AnalyticsWindow Window { get; set; } = new();

    public double DistanceKm { get; set; }

    public double DistanceExcludedKm { get; set; }

    public AnalyticsSpeed Speed { get; set; } = new();

    public AnalyticsTime Time { get; set; } = new();

    public double IdleHours { get; set; }

    public AnalyticsGaps Gaps { get; set; } = new();

    public AnalyticsReporting Reporting { get; set; } = new();
}

public class AnalyticsWindow
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public double CoveragePercent { get; set; }
}

public class AnalyticsSpeed
{
    public double? AvgSogKnots { get; set; }

    public double? MaxSogKnots { get; set; }

    public double? MaxImpliedKnots { get; set; }
}

public class AnalyticsTime
{
    public double UnderwayHours { get; set; }

    public double StoppedHours { get; set; }

    public Dictionary<int, double> ByNavStatus { get; set; } = new();
}

public class AnalyticsGaps
{
    public int Count { get; set; }

    public double TotalDarkHours { get; set; }

    public AnalyticsGap? Longest { get; set; }
}

public class AnalyticsGap
{
    public double Hours { get; set; }

    public DateTime From { get; set; }

    public DateTime To { get; set; }
}

public class AnalyticsReporting
{
    public int Messages { get; set; }

    public double? MedianIntervalSeconds { get; set; }

    public DateTime? First { get; set; }

    public DateTime? Last { get; set; }
}