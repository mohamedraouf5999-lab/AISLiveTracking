namespace AISLiveTracking.API.Services.Interfaces;

public interface IJwtTokenService
{
    string CreateToken(
        Guid userId,
        string username,
        string role);
}