using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Microsoft.AspNetCore.Identity;

namespace AISLiveTracking.API.Services;

public class AdminBootstrapService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AdminBootstrapService(
        IUserRepository userRepository,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    public async Task EnsureAdminAsync()
    {
        var username =
            _configuration["AdminBootstrap:Username"];

        var password =
            _configuration["AdminBootstrap:Password"];

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var existingUser =
            await _userRepository.GetByUsernameAsync(username);

        if (existingUser is not null)
        {
            return;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Role = "Admin",
            CreatedUtc = DateTime.UtcNow
        };

        user.PasswordHash =
            _passwordHasher.HashPassword(
                user,
                password);

        await _userRepository.CreateAsync(
            user.Username,
            user.PasswordHash,
            user.Role);
    }
}