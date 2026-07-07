namespace AISLiveTracking.API.Models;

public class Vessel
{
    public int Id { get; set; }

    public long MMSI { get; set; }

    public int? IMO { get; set; }

    public string? VesselName { get; set; }

    public string? CallSign { get; set; }

    public int? ShipType { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}