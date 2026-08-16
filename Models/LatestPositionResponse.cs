namespace AISLiveTracking.API.Models;

public class LatestPositionResponse
{
    public long Mmsi { get; set; }

    public int? Imo { get; set; }

    public string? Name { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public decimal? Sog { get; set; }

    public decimal? Cog { get; set; }

    public int? NavStatus { get; set; }

    public string? NavStatusText { get; set; }

    public DateTime TimestampUtc { get; set; }

    public long AgeSeconds { get; set; }
}