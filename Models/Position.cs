namespace AISLiveTracking.API.Models;

public class Position
{
    public long Id { get; set; }

    public int VesselId { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public decimal? SOG { get; set; }

    public decimal? COG { get; set; }

    public int? Heading { get; set; }

    public int? NavigationStatus { get; set; }

    public DateTime PositionTimestamp { get; set; }

    public DateTime CreatedAt { get; set; }
}