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
    private readonly ILogger _logger;
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

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();

                var url = _configuration["AisStream:WebSocketUrl"];

                _logger.LogInformation(
                    "Connecting to AIS Stream...");

                socket.Options.KeepAliveInterval =
                    TimeSpan.FromSeconds(20);

                await socket.ConnectAsync(
                    new Uri(url!),
                    stoppingToken);

                _logger.LogInformation(
                    "Connected successfully.");

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
                }
                };

                var json =
                    JsonSerializer.Serialize(subscription);

                _logger.LogInformation(
                    "Subscription JSON: {Json}",
                    json);

                var bytes =
                    Encoding.UTF8.GetBytes(json);

                await socket.SendAsync(
                    bytes,
                    WebSocketMessageType.Text,
                    true,
                    stoppingToken);

                _logger.LogInformation(
                    "Subscription sent successfully.");

                _logger.LogInformation(
                    "Socket state after subscription: {State}",
                    socket.State);

                _logger.LogInformation(
                    "Starting ReceiveAsync...");

                var buffer = new byte[8192];

                while (socket.State == WebSocketState.Open)
                {
                    _logger.LogInformation(
                        "Before ReceiveAsync. Socket state: {State}",
                        socket.State);

                    var result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        stoppingToken);

                    _logger.LogInformation(
                        "ReceiveAsync returned. Type={Type}, Count={Count}",
                        result.MessageType,
                        result.Count);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning("Server closed the connection.");
                        break;
                    }

                    var message = Encoding.UTF8.GetString(
                        buffer,
                        0,
                        result.Count);

                    _logger.LogInformation(
                        "Received message: {Message}",
                        message);

                    var envelope =
                        JsonSerializer.Deserialize<AisMessageEnvelope>(message);

                    if (envelope == null)
                        continue;

                    switch (envelope.MessageType)
                    {
                        case "PositionReport":
                            await HandlePositionReport(envelope);
                            break;

                        case "ShipStaticData":
                            await HandleShipStaticData(envelope);
                            break;

                        default:
                            _logger.LogWarning(
                                "Unknown message type: {MessageType}",
                                envelope.MessageType);
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(
                    "Background service is stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Connection failed.");

                await Task.Delay(
                    1000,
                    stoppingToken);
            }
        }
    }

    private async Task HandlePositionReport(
        AisMessageEnvelope envelope)
    {
        _logger.LogInformation(
            "HandlePositionReport called");

        var reportElement =
            envelope.Message.GetProperty("PositionReport");

        var report =
            reportElement.Deserialize<PositionReport>();

        if (report == null)
        {
            return;
        }

        if (report.UserID <= 0)
        {
            _logger.LogWarning(
                "Invalid MMSI.");

            return;
        }

        if (report.UserID.ToString().Length != 9)
        {
            _logger.LogWarning(
                "Invalid MMSI length.");

            return;
        }

        if (report.Latitude < -90 ||
            report.Latitude > 90)
        {
            _logger.LogWarning(
                "Invalid Latitude.");

            return;
        }

        if (report.Longitude < -180 ||
            report.Longitude > 180)
        {
            _logger.LogWarning(
                "Invalid Longitude.");

            return;
        }

        if (report.Latitude == 91 ||
            report.Longitude == 181)
        {
            _logger.LogWarning(
                "Invalid sentinel position.");

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
            _logger.LogWarning(
                "Position report is marked as invalid.");

            return;
        }

        var metaData =
            envelope.MetaData.Deserialize<MetaData>();

        if (metaData == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(metaData.time_utc))
        {
            _logger.LogWarning(
                "Missing timestamp.");

            return;
        }

        if (!DateTime.TryParse(
                metaData.time_utc.Replace(" UTC", ""),
                out var messageTime))
        {
            _logger.LogWarning(
                "Invalid timestamp: {Timestamp}",
                metaData.time_utc);

            return;
        }

        using var scope =
            _scopeFactory.CreateScope();

        var vesselRepository =
            scope.ServiceProvider
                .GetRequiredService<IVesselRepository>();

        var positionRepository =
            scope.ServiceProvider
                .GetRequiredService<IPositionRepository>();

        var vessel = new Vessel
        {
            Mmsi = report.UserID,
            VesselName = metaData.ShipName,
            FirstSeenUtc = DateTime.UtcNow,
            LastSeenUtc = DateTime.UtcNow
        };

        await vesselRepository.UpsertAsync(vessel);

        await positionRepository.InsertAsync(
            report,
            messageTime);
    }

    private async Task HandleShipStaticData(AisMessageEnvelope envelope)
    {
        _logger.LogInformation("HandleShipStaticData called");

        var staticElement = envelope.Message.GetProperty("ShipStaticData");

        var shipData = staticElement.Deserialize<ShipStaticData>();

        if (shipData == null)
        {
            _logger.LogWarning("Could not deserialize ShipStaticData.");
            return;
        }

        if (shipData.UserID <= 0)
        {
            _logger.LogWarning("Invalid MMSI in ShipStaticData.");
            return;
        }

        if (!shipData.Valid)
        {
            _logger.LogWarning(
                "ShipStaticData is marked as invalid for MMSI {Mmsi}.",
                shipData.UserID);

            return;
        }

        using var scope = _scopeFactory.CreateScope();

        var vesselRepository =
            scope.ServiceProvider.GetRequiredService<IVesselRepository>();

        var now = DateTime.UtcNow;

        var vessel = new Vessel
        {
            Mmsi = shipData.UserID,

            Imo = shipData.ImoNumber > 0
                ? shipData.ImoNumber
                : null,

            VesselName = shipData.Name,
            CallSign = shipData.CallSign,

            ShipType = shipData.Type > 0
                ? (short?)shipData.Type
                : null,

            DimToBow = shipData.Dimension?.A,
            DimToStern = shipData.Dimension?.B,
            DimToPort = shipData.Dimension?.C,
            DimToStarboard = shipData.Dimension?.D,

            Draught = shipData.MaximumStaticDraught > 0
                ? shipData.MaximumStaticDraught
                : null,

            Destination = shipData.Destination,

            FirstSeenUtc = now,
            LastSeenUtc = now
        };

        if (shipData.Eta != null &&
            shipData.Eta.Month >= 1 &&
            shipData.Eta.Month <= 12 &&
            shipData.Eta.Day >= 1 &&
            shipData.Eta.Day <= 31 &&
            shipData.Eta.Hour >= 0 &&
            shipData.Eta.Hour <= 23 &&
            shipData.Eta.Minute >= 0 &&
            shipData.Eta.Minute <= 59)
        {
            var year = DateTime.UtcNow.Year;

            try
            {
                vessel.Eta = new DateTime(
                    year,
                    shipData.Eta.Month,
                    shipData.Eta.Day,
                    shipData.Eta.Hour,
                    shipData.Eta.Minute,
                    0,
                    DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                vessel.Eta = null;
            }
        }

        await vesselRepository.UpsertAsync(vessel);

        _logger.LogInformation(
            "Ship static data saved. MMSI={Mmsi}, IMO={Imo}, Name={Name}",
            vessel.Mmsi,
            vessel.Imo,
            vessel.VesselName);
    }
}