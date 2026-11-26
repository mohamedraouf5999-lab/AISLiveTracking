using AISLiveTracking.API.Models;
using AISLiveTracking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AISLiveTracking.API.Controllers;

[ApiController]
[Route("api/admin/api-keys")]
[Authorize(Roles = "Admin")]
public class AdminApiKeysController : ControllerBase
{
    private readonly ApiKeyService _apiKeyService;

    public AdminApiKeysController(ApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateApiKeyRequest request)
    {
        var result =
            await _apiKeyService.CreateAsync(request.ClientId);

        return Ok(new
        {
            id = result.Id,
            clientId = request.ClientId,
            keyPrefix = result.Prefix,
            apiKey = result.ApiKey
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var revoked = await _apiKeyService.RevokeAsync(id);

        if (!revoked)
        {
            return NotFound(new
            {
                message = "API key not found."
            });
        }

        return NoContent();
    }
}