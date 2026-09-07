using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Dapper;

namespace AISLiveTracking.API.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DatabaseConnection _database;

    public UserRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        const string sql = """
            SELECT
                id AS Id,
                username AS Username,
                password_hash AS PasswordHash,
                role AS Role,
                created_utc AS CreatedUtc
            FROM users
            WHERE username = @Username
        """;

        using var connection = _database.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<User>(
            sql,
            new { Username = username });
    }

    public async Task CreateAsync(
        string username,
        string passwordHash,
        string role)
    {
        const string sql = """
            INSERT INTO users
            (
                username,
                password_hash,
                role
            )
            VALUES
            (
                @Username,
                @PasswordHash,
                @Role
            )
        """;

        using var connection = _database.CreateConnection();

        await connection.ExecuteAsync(
            sql,
            new
            {
                Username = username,
                PasswordHash = passwordHash,
                Role = role
            });
    }
}