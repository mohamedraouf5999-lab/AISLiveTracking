namespace AISLiveTracking.API.Models;

public class SubscriptionRequest
{
    public string APIKey { get; set; } = string.Empty;

    public List<List<List<double>>> BoundingBoxes { get; set; } = new();

    public List<string> FilterMessageTypes { get; set; } = new();
}