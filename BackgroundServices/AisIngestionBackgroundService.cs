using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AISLiveTracking.API.BackgroundServices;

public class AisIngestionBackgroundService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private long _receivedMessages;
    private long _storedPositions;
    private long _upsertedVessels;
    private long _rejectedMessages;

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
        using var summaryTimer = new Timer(
            _ => LogPeriodicSummary(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));

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

                    var rawPayload = Encoding.UTF8.GetString(
                        messageBuffer.ToArray());

                    Interlocked.Increment(ref _receivedMessages);

                    AisMessageEnvelope? envelope;

                    try
                    {
                        envelope =
                            JsonSerializer.Deserialize<AisMessageEnvelope>(
                                rawPayload);
                    }
                    catch (JsonException ex)
                    {
                        await RejectMessageAsync(
                            rawPayload,
                            $"Invalid JSON: {ex.Message}",
                            stoppingToken);
                        continue;
                    }

                    if (envelope == null)
                    {
                        await RejectMessageAsync(
                            rawPayload,
                            "Message envelope was empty or invalid.",
                            stoppingToken);
                        continue;
                    }

                    switch (envelope.MessageType)
                    {
                        case "SubscriptionConfirmation":
                            _logger.LogInformation(
                                "AIS subscription confirmed.");
                            break;

                        case "PositionReport":
                            await HandlePositionReport(
                                envelope,
                                rawPayload,
                                stoppingToken);
                            break;

                        case "ShipStaticData":
                            await HandleShipStaticData(
                                envelope,
                                rawPayload,
                                stoppingToken);
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
        AisMessageEnvelope envelope,
        string rawPayload,
        CancellationToken cancellationToken)
    {
        if (envelope.Message.ValueKind != JsonValueKind.Object ||
            !envelope.Message.TryGetProperty(
                "PositionReport",
                out var reportElement))
        {
            await RejectMessageAsync(
                rawPayload,
                "PositionReport payload is missing.",
                cancellationToken);
            return;
        }

        PositionReport? report;

        try
        {
            report = reportElement.Deserialize<PositionReport>();
        }
        catch (JsonException ex)
        {
            await RejectMessageAsync(
                rawPayload,
                $"PositionReport payload is invalid: {ex.Message}",
                cancellationToken);
            return;
        }

        if (report == null)
        {
            await RejectMessageAsync(
                rawPayload,
                "PositionReport payload could not be read.",
                cancellationToken);
            return;
        }

        if (report.UserID < 100_000_000 ||
            report.UserID > 999_999_999)
        {
            await RejectMessageAsync(
                rawPayload,
                "MMSI must contain exactly 9 digits.",
                cancellationToken);
            return;
        }

        if (report.Latitude == 91 ||
            report.Longitude == 181)
        {
            await RejectMessageAsync(
                rawPayload,
                "Position contains an unavailable AIS sentinel fix.",
                cancellationToken);
            return;
        }

        if (report.Latitude < -90 ||
            report.Latitude > 90)
        {
            await RejectMessageAsync(
                rawPayload,
                "Latitude is outside the valid range.",
                cancellationToken);
            return;
        }

        if (report.Longitude < -180 ||
            report.Longitude > 180)
        {
            await RejectMessageAsync(
                rawPayload,
                "Longitude is outside the valid range.",
                cancellationToken);
            return;
        }

        if (!report.Valid)
        {
            await RejectMessageAsync(
                rawPayload,
                "Position report is marked invalid by the provider.",
                cancellationToken);
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

        MetaData? metaData;

        try
        {
            if (envelope.MetaData.ValueKind != JsonValueKind.Object)
            {
                await RejectMessageAsync(
                    rawPayload,
                    "Message metadata is missing.",
                    cancellationToken);
                return;
            }

            metaData = envelope.MetaData.Deserialize<MetaData>();
        }
        catch (JsonException ex)
        {
            await RejectMessageAsync(
                rawPayload,
                $"Message metadata is invalid: {ex.Message}",
                cancellationToken);
            return;
        }

        if (metaData == null ||
            string.IsNullOrWhiteSpace(metaData.time_utc))
        {
            await RejectMessageAsync(
                rawPayload,
                "Message timestamp is missing.",
                cancellationToken);
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
            await RejectMessageAsync(
                rawPayload,
                "Message timestamp could not be parsed.",
                cancellationToken);
            return;
        }

        if (messageTime > DateTime.UtcNow.AddMinutes(5))
        {
            await RejectMessageAsync(
                rawPayload,
                "Message timestamp is more than 5 minutes in the future.",
                cancellationToken);
            return;
        }

        using var scope = _scopeFactory.CreateScope();

        var vesselRepository =
            scope.ServiceProvider
                .GetRequiredService<IVesselRepository>();

        var positionRepository =
            scope.ServiceProvider
                .GetRequiredService<IPositionRepository>();

        var now = DateTime.UtcNow;

        var vessel = new Vessel
        {
            Mmsi = report.UserID,
            VesselName = metaData.ShipName,
            FirstSeenUtc = now,
            LastSeenUtc = now
        };

        await vesselRepository.UpsertAsync(vessel);
        Interlocked.Increment(ref _upsertedVessels);

        await positionRepository.InsertAsync(
            report,
            messageTime);
        Interlocked.Increment(ref _storedPositions);
    }

    private async Task HandleShipStaticData(
        AisMessageEnvelope envelope,
        string rawPayload,
        CancellationToken cancellationToken)
    {
        if (envelope.Message.ValueKind != JsonValueKind.Object ||
            !envelope.Message.TryGetProperty(
                "ShipStaticData",
                out var staticElement))
        {
            await RejectMessageAsync(
                rawPayload,
                "ShipStaticData payload is missing.",
                cancellationToken);
            return;
        }

        ShipStaticData? shipData;

        try
        {
            shipData = staticElement.Deserialize<ShipStaticData>();
        }
        catch (JsonException ex)
        {
            await RejectMessageAsync(
                rawPayload,
                $"ShipStaticData payload is invalid: {ex.Message}",
                cancellationToken);
            return;
        }

        if (shipData == null)
        {
            await RejectMessageAsync(
                rawPayload,
                "ShipStaticData payload could not be read.",
                cancellationToken);
            return;
        }

        if (shipData.UserID < 100_000_000 ||
            shipData.UserID > 999_999_999)
        {
            await RejectMessageAsync(
                rawPayload,
                "MMSI in ShipStaticData must contain exactly 9 digits.",
                cancellationToken);
            return;
        }

        if (!shipData.Valid)
        {
            await RejectMessageAsync(
                rawPayload,
                "ShipStaticData is marked invalid by the provider.",
                cancellationToken);
            return;
        }

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
                    "Invalid IMO checksum for MMSI {Mmsi}; IMO will be stored as null.",
                    shipData.UserID);
            }
        }

        using var scope = _scopeFactory.CreateScope();

        var vesselRepository =
            scope.ServiceProvider
                .GetRequiredService<IVesselRepository>();

        var now = DateTime.UtcNow;

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
            try
            {
                vessel.Eta = new DateTime(
                    now.Year,
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
        Interlocked.Increment(ref _upsertedVessels);

        _logger.LogInformation(
            "Ship static data saved. MMSI={Mmsi}, IMO={Imo}, Name={Name}",
            vessel.Mmsi,
            vessel.Imo,
            vessel.VesselName);
    }

    private async Task RejectMessageAsync(
        string rawPayload,
        string reason,
        CancellationToken cancellationToken)
    {
        var safeReason = reason.Length <= 256
            ? reason
            : reason[..256];

        using var scope = _scopeFactory.CreateScope();

        var deadLetterRepository =
            scope.ServiceProvider
                .GetRequiredService<IIngestionDeadLetterRepository>();

        await deadLetterRepository.SaveAsync(
            rawPayload,
            safeReason,
            cancellationToken);

        Interlocked.Increment(ref _rejectedMessages);

        _logger.LogWarning(
            "AIS message rejected and saved to dead-letter table: {Reason}",
            safeReason);
    }

    private void LogPeriodicSummary()
    {
        var received = Interlocked.Exchange(ref _receivedMessages, 0);
        var positions = Interlocked.Exchange(ref _storedPositions, 0);
        var vessels = Interlocked.Exchange(ref _upsertedVessels, 0);
        var rejected = Interlocked.Exchange(ref _rejectedMessages, 0);

        _logger.LogInformation(
            "AIS ingestion summary (last 30 seconds): received {ReceivedMessages}, stored {StoredPositions} positions, upserted {UpsertedVessels} vessels, rejected {RejectedMessages} messages.",
            received,
            positions,
            vessels,
            rejected);
    }

    private static bool IsValidImo(int imo)
    {
        if (imo < 1_000_000 ||
            imo > 9_999_999)
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