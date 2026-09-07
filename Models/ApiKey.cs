namespace AISLiveTracking.API.Models;

public class ApiKey
{
    public Guid Id { get; set; }

    public Guid ClientId { get; set; }

    public byte[] KeyHash { get; set; } = [];

    public string KeyPrefix { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? RevokedUtc { get; set; }
}