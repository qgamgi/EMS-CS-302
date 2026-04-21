using EMS.API.DTOs;
using EMS.API.Models;
using MongoDB.Driver;

namespace EMS.API.Services;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<User> RegisterAsync(RegisterRequest request);
}

public class AuthService : IAuthService
{
    private readonly IMongoCollection<User> _users;
    private readonly ITokenService _tokenService;

    public AuthService(IMongoDatabase db, ITokenService tokenService)
    {
        _users = db.GetCollection<User>("users");
        _tokenService = tokenService;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _users
            .Find(u => u.Email == request.Email && u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        // Update lastSeenAt
        var update = Builders<User>.Update.Set(u => u.LastSeenAt, DateTime.UtcNow);
        await _users.UpdateOneAsync(u => u.Id == user.Id, update);

        var token = _tokenService.GenerateToken(user);
        return new AuthResponse(token, user.Id!, user.FullName, user.Email, user.Role.ToString());
    }

    public async Task<User> RegisterAsync(RegisterRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, out var role))
            throw new ArgumentException($"Invalid role: {request.Role}");

        var existing = await _users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
        if (existing != null)
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _users.InsertOneAsync(user);
        return user;
    }
}
