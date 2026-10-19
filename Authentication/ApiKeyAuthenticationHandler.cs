using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

using AISLiveTracking.API.Data.Interfaces;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AISLiveTracking.API.Authentication;

public class ApiKeyAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IApiClientRepository _apiClientRepository;
    private readonly IPlanRepository _planRepository;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyRepository apiKeyRepository,
        IApiClientRepository apiClientRepository,
        IPlanRepository planRepository)
        : base(options, logger, encoder)
    {
        _apiKeyRepository = apiKeyRepository;
        _apiClientRepository = apiClientRepository;
        _planRepository = planRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? rawApiKey = null;

        if (Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            rawApiKey = apiKeyHeader.ToString();
        }
        else if (Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            var authorization = authorizationHeader.ToString();

            if (authorization.StartsWith(
                    "ApiKey ",
                    StringComparison.OrdinalIgnoreCase))
            {
                rawApiKey = authorization["ApiKey ".Length..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(rawApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var keyHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(rawApiKey));

        var apiKey = await _apiKeyRepository.GetByHashAsync(keyHash);

        if (apiKey is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        if (!apiKey.IsActive || apiKey.RevokedUtc.HasValue)
        {
            return AuthenticateResult.Fail("API key has been revoked.");
        }

        var client =
            await _apiClientRepository.GetByIdAsync(apiKey.ClientId);

        if (client is null)
        {
            return AuthenticateResult.Fail("API client not found.");
        }

        var plan =
            await _planRepository.GetByIdAsync(client.PlanId);

        if (plan is null)
        {
            return AuthenticateResult.Fail("API client plan not found.");
        }

        var claims = new[]
{
    new Claim(
        ClaimTypes.NameIdentifier,
        apiKey.Id.ToString()),

    new Claim(
        ClaimTypes.Role,
        "ReadOnly"),

    new Claim(
        "client_id",
        client.Id.ToString()),

    new Claim(
        "plan_id",
        plan.Id.ToString()),

    new Claim(
        "analytics_enabled",
        plan.AnalyticsEnabled.ToString())
};

        var identity = new ClaimsIdentity(
            claims,
            Scheme.Name);

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(
            principal,
            Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

