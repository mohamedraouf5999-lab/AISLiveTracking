namespace AISLiveTracking.API.Models;

public class MetaData
{
    public long MMSI { get; set; }

    public string? ShipName { get; set; }

    public string? time_utc { get; set; }
}