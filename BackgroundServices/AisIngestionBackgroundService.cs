using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AISLiveTracking.API.Models;
using AISLiveTracking.API.Data.Interfaces;




namespace AISLiveTracking.API.BackgroundServices;

public class AisIngestionBackgroundService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AisIngestionBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public AisIngestionBackgroundService(
    IConfiguration configuration,
    ILogger<AisIngestionBackgroundService> logger,
    IServiceScopeFactory scopeFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
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

                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

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
                _logger.LogInformation("Subscription JSON: {Json}", json);
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
                    var result = await socket.ReceiveAsync(
    new ArraySegment<byte>(buffer),
    stoppingToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning("Server closed the connection.");
                        break;
                    }

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
                            await HandlePositionReport(envelope);
                            break;

                        case "ShipStaticData":
                            await HandleShipStaticData(envelope);
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

    private async Task HandlePositionReport(AisMessageEnvelope envelope)
    {
        _logger.LogInformation("HandlePositionReport called");
        var reportElement = envelope.Message.GetProperty("PositionReport");

        var report = reportElement.Deserialize<PositionReport>();

        if (report == null)
        {
            return;
        }

        if (report.UserID <= 0)
        {
            _logger.LogWarning("Invalid MMSI.");

            return;
        }
        if (report.UserID.ToString().Length != 9)
        {
            _logger.LogWarning("Invalid MMSI length.");

            return;
        }
        if (report.Latitude < -90 || report.Latitude > 90)
        {
            _logger.LogWarning("Invalid Latitude.");

            return;
        }
        if (report.Longitude < -180 || report.Longitude > 180)
        {
            _logger.LogWarning("Invalid Longitude.");

            return;
        }
        if (report.Latitude == 91 || report.Longitude == 181)
        {
            _logger.LogWarning("Invalid sentinel position.");

            return;
        }
        if (report.Sog == 102.3)
        {
            report.Sog = null;
        }

        if (report.Cog == 360)
        {
            report.Cog = null;
        }

        if (report.TrueHeading == 511)
        {
            report.TrueHeading = null;
        }

        if (!report.Valid)
        {
            _logger.LogWarning("Position report is marked as invalid.");

            return;
        }

        var metaData = envelope.MetaData.Deserialize<MetaData>();

        if (metaData == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(metaData.time_utc))
        {
            _logger.LogWarning("Missing timestamp.");

            return;
        }

        if (!DateTime.TryParse(
         metaData.time_utc.Replace(" UTC", ""),
         out var messageTime))
        {
            _logger.LogWarning("Invalid timestamp: {Timestamp}", metaData.time_utc);
            return;
        }
        // if (messageTime > DateTime.UtcNow.AddMinutes(5))
        // {
        //   _logger.LogWarning("Timestamp is too far in the future.");

        //     return;
        //    }
        using var scope = _scopeFactory.CreateScope();

        var vesselRepository =
            scope.ServiceProvider.GetRequiredService<IVesselRepository>();

        var positionRepository =
            scope.ServiceProvider.GetRequiredService<IPositionRepository>();

        var vessel = new Vessel
        {
            Mmsi = report.UserID,
            VesselName = metaData.ShipName,
            FirstSeenUtc = DateTime.UtcNow,
            LastSeenUtc = DateTime.UtcNow
        };

        await vesselRepository.UpsertAsync(vessel);

        await positionRepository.InsertAsync(report, messageTime);

        return;

    }

    private Task HandleShipStaticData(AisMessageEnvelope envelope)
    {
        return Task.CompletedTask;
    }
}