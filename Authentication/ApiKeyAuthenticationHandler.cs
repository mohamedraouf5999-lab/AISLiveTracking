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

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
       IApiKeyRepository apiKeyRepository)
    : base(options, logger, encoder)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var rawApiKey = apiKeyHeader.ToString();

        if (string.IsNullOrWhiteSpace(rawApiKey))
        {
            return AuthenticateResult.Fail("API key is empty.");
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

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, apiKey.Id.ToString()),
            new Claim(ClaimTypes.Role, "ReadOnly")
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