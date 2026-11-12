using Microsoft.Extensions.Hosting;
using System.Globalization;
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
        var reconnectDelay = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            var connectedAtUtc = DateTime.MinValue;

            try
            {
                using var socket = new ClientWebSocket();

                var url = _configuration["AisStream:WebSocketUrl"];

                _logger.LogInformation("Connecting to AIS Stream...");

                socket.Options.KeepAliveInterval =
                    TimeSpan.FromSeconds(20);

                await socket.ConnectAsync(
                    new Uri(url!),
                    stoppingToken);

                connectedAtUtc = DateTime.UtcNow;

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
                    using var messageBuffer = new MemoryStream();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await socket.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            stoppingToken);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

                        if (result.MessageType != WebSocketMessageType.Text &&
                            result.MessageType != WebSocketMessageType.Binary)
                        {
                            throw new InvalidDataException(
                                $"Unsupported WebSocket message type: {result.MessageType}.");
                        }

                        if (messageBuffer.Length + result.Count > 1024 * 1024)
                        {
                            throw new InvalidDataException(
                                "AIS message exceeded the 1 MiB limit.");
                        }

                        messageBuffer.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning(
                            "Server closed the connection.");

                        break;
                    }

                    var message = Encoding.UTF8.GetString(
                        messageBuffer.ToArray());

                    var envelope =
                        JsonSerializer.Deserialize<AisMessageEnvelope>(
                            message);

                    if (envelope == null)
                    {
                        continue;
                    }

                    switch (envelope.MessageType)
                    {
                        case "SubscriptionConfirmation":
                            _logger.LogInformation(
                                "AIS subscription confirmed.");
                            break;

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
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Background service is stopping.");

                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "AIS connection or message processing failed.");
            }

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            if (connectedAtUtc != DateTime.MinValue &&
                DateTime.UtcNow - connectedAtUtc >=
                TimeSpan.FromSeconds(30))
            {
                reconnectDelay = TimeSpan.FromSeconds(1);
            }

            _logger.LogWarning(
                "Reconnecting in {DelaySeconds} seconds.",
                reconnectDelay.TotalSeconds);

            await Task.Delay(reconnectDelay, stoppingToken);

            reconnectDelay = TimeSpan.FromSeconds(
                Math.Min(reconnectDelay.TotalSeconds * 2, 30));
        }
    }

    private async Task HandlePositionReport(
        AisMessageEnvelope envelope)
    {
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
            _logger.LogWarning("Invalid MMSI.");
            return;
        }

        if (report.UserID.ToString().Length != 9)
        {
            _logger.LogWarning("Invalid MMSI length.");
            return;
        }

        if (report.Latitude < -90 ||
            report.Latitude > 90)
        {
            _logger.LogWarning("Invalid Latitude.");
            return;
        }

        if (report.Longitude < -180 ||
            report.Longitude > 180)
        {
            _logger.LogWarning("Invalid Longitude.");
            return;
        }

        if (report.Latitude == 91 ||
            report.Longitude == 181)
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
            _logger.LogWarning("Missing timestamp.");
            return;
        }

        var timestamp = metaData.time_utc.Replace(" UTC", "");

        if (!DateTime.TryParse(
                timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out var messageTime))
        {
            _logger.LogWarning(
                "Invalid timestamp: {Timestamp}",
                metaData.time_utc);

            return;
        }

        if (messageTime > DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogWarning(
                "Position report timestamp is too far in the future: {Timestamp}",
                metaData.time_utc);

            return;
        }

        using var scope = _scopeFactory.CreateScope();

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

    private async Task HandleShipStaticData(
        AisMessageEnvelope envelope)
    {
        var staticElement =
            envelope.Message.GetProperty("ShipStaticData");

        var shipData =
            staticElement.Deserialize<ShipStaticData>();

        if (shipData == null)
        {
            _logger.LogWarning(
                "Could not deserialize ShipStaticData.");

            return;
        }

        if (shipData.UserID <= 0)
        {
            _logger.LogWarning(
                "Invalid MMSI in ShipStaticData.");

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
            scope.ServiceProvider
                .GetRequiredService<IVesselRepository>();

        var now = DateTime.UtcNow;
        int? validatedImo = null;

        if (shipData.ImoNumber > 0)
        {
            if (IsValidImo(shipData.ImoNumber))
            {
                validatedImo = shipData.ImoNumber;
            }
            else
            {
                _logger.LogWarning(
                    "Invalid IMO checksum for MMSI {Mmsi}. IMO will be stored as null.",
                    shipData.UserID);
            }
        }

        var vessel = new Vessel
        {
            Mmsi = shipData.UserID,
            Imo = validatedImo,
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

    private static bool IsValidImo(int imo)
    {
        if (imo < 1_000_000 || imo > 9_999_999)
        {
            return false;
        }

        var digits = imo.ToString(CultureInfo.InvariantCulture);
        var sum = 0;

        for (var i = 0; i < 6; i++)
        {
            sum += (digits[i] - '0') * (7 - i);
        }

        return sum % 10 == digits[6] - '0';
    }
}