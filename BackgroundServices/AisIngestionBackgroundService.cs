using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Text;

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

                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection failed.");

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}