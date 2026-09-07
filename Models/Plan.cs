namespace AISLiveTracking.API.Models;

public class Plan
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RateLimitPerMinute { get; set; }
    public int? MonthlyQuota { get; set; }
    public int? MaxHistoryDays { get; set; }
    public bool AnalyticsEnabled { get; set; }
}