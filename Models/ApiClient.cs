namespace AISLiveTracking.API.Models;

public class ApiClient
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PlanId { get; set; }
    public DateTime CreatedUtc { get; set; }
}