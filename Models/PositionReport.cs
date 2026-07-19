namespace AISLiveTracking.API.Models;

public class PositionReport
{
    public long UserID { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double? Sog { get; set; }

    public double? Cog { get; set; }

    public int? TrueHeading { get; set; }

    public int NavigationalStatus { get; set; }

    public int RateOfTurn { get; set; }

    public bool PositionAccuracy { get; set; }

    public bool Valid { get; set; }
}