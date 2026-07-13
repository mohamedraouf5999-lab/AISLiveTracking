using System.Text.Json;

namespace AISLiveTracking.API.Models;

public class AisMessageEnvelope
{
    public string? MessageType { get; set; }

    public JsonElement Message { get; set; }

    public JsonElement MetaData { get; set; }
}