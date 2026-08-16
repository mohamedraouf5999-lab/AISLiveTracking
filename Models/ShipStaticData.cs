namespace AISLiveTracking.API.Models;

public class ShipStaticData
{
    public long UserID { get; set; }

    public bool Valid { get; set; }

    public int ImoNumber { get; set; }

    public string? CallSign { get; set; }

    public string? Name { get; set; }

    public int Type { get; set; }

    public ShipStaticDataDimension? Dimension { get; set; }

    public ShipStaticDataEta? Eta { get; set; }

    public decimal MaximumStaticDraught { get; set; }

    public string? Destination { get; set; }
}

public class ShipStaticDataDimension
{
    public short A { get; set; }
    public short B { get; set; }
    public short C { get; set; }
    public short D { get; set; }
}

public class ShipStaticDataEta
{
    public int Month { get; set; }
    public int Day { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
}