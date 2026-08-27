namespace AISLiveTracking.API.Models;

public class PositionHistoryResult
{
    public List<Position> Items { get; set; } = new();

    public string? NextCursor { get; set; }
}