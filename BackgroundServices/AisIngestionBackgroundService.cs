using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Text;
using AISLiveTracking.API.Models;
using System.Text.Json;


namespace AISLiveTracking.API.BackgroundServices;

public class AisIngestionBackgroundService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AisIngestionBackgroundService> _logger;

    public AisIngestionBackgroundService(
        IConfiguration configuration,
        ILogger<AisIngestionBackgroundService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();

                var url = _configuration["AisStream:WebSocketUrl"];

                _logger.LogInformation("Connecting to AIS Stream...");

                await socket.ConnectAsync(new Uri(url!), stoppingToken);

                _logger.LogInformation("Connected successfully.");
                var subscription = new SubscriptionRequest
                {
                    APIKey = _configuration["AisStream:ApiKey"]!,
                    BoundingBoxes = new()
    {
        new()
        {
            new() { -90, -180 },
            new() { 90, 180 }
        }
    },
                    FilterMessageTypes = new()
    {
        "PositionReport",
        "ShipStaticData"
    }
                };


                var json = JsonSerializer.Serialize(subscription);
                var bytes = Encoding.UTF8.GetBytes(json);

                await socket.SendAsync(
    bytes,
    WebSocketMessageType.Text,
    true,
    stoppingToken);
                _logger.LogInformation("Subscription sent successfully.");
                var buffer = new byte[8192];

                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer, stoppingToken);
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogInformation("Received message: {Message}", message);
                    var envelope = JsonSerializer.Deserialize<AisMessageEnvelope>(message);
                    if (envelope == null)
                    {
                        continue;
                    }

                    switch (envelope.MessageType)
                    {
                        case "PositionReport":
                            _logger.LogInformation("PositionReport received.");
                            break;

                        case "ShipStaticData":
                            _logger.LogInformation("ShipStaticData received.");
                            break;

                        default:
                            _logger.LogWarning("Unknown message type: {MessageType}", envelope.MessageType);
                            break;
                    }
                }



            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background service is stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection failed.");

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}