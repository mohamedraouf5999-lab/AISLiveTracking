using System.Text.Json.Serialization;

namespace AISLiveTracking.API.Models;

public class Position
{
    private DateTime _msgTimestampUtc;

    public long Id { get; set; }

    public long Mmsi { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public decimal? Sog { get; set; }

    public decimal? Cog { get; set; }

    public int? TrueHeading { get; set; }

    public int? NavStatus { get; set; }

    public int? RateOfTurn { get; set; }

    public bool PositionAccuracy { get; set; }

    [JsonPropertyName("timestampUtc")]
    public DateTime MsgTimestampUtc
    {
        get => _msgTimestampUtc;
        set => _msgTimestampUtc =
            DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    public DateTime ReceivedUtc { get; set; }
}