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
    private readonly IMongoCollection<Driver> _drivers;
    private readonly ITokenService _tokenService;
    private readonly ISessionService _sessionService;

    public AuthService(
        IMongoDatabase db,
        ITokenService tokenService,
        ISessionService sessionService)
    {
        _users          = db.GetCollection<User>("users");
        _drivers        = db.GetCollection<Driver>("drivers");
        _tokenService   = tokenService;
        _sessionService = sessionService;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _users
            .Find(u => u.Email == request.Email && u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        // Update lastSeenAt
        var userUpdate = Builders<User>.Update.Set(u => u.LastSeenAt, DateTime.UtcNow);
        await _users.UpdateOneAsync(u => u.Id == user.Id, userUpdate);

        // Auto-upsert a Driver document when a Driver-role user logs in
        if (user.Role == UserRole.Driver)
        {
            try
            {
                var driverFilter = Builders<Driver>.Filter.Eq(d => d.UserId, user.Id!);
                var driverUpdate = Builders<Driver>.Update
                    .SetOnInsert(d => d.UserId,    user.Id!)
                    .SetOnInsert(d => d.VehicleId, string.Empty)
                    .SetOnInsert(d => d.Status,    DriverStatus.Available)
                    .Set(d => d.UpdatedAt, DateTime.UtcNow);

                await _drivers.UpdateOneAsync(
                    driverFilter, driverUpdate,
                    new UpdateOptions { IsUpsert = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthService] Driver upsert failed (non-fatal): {ex.Message}");
            }
        }

        var (token, jti) = _tokenService.GenerateTokenWithJti(user);

        // Persist session to MongoDB (non-fatal if it fails)
        try
        {
            await _sessionService.CreateAsync(jti, user.Id!, user.FullName, user.Email, user.Role.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthService] Session create failed (non-fatal): {ex.Message}");
        }

        return new AuthResponse(token, user.Id!, user.FullName, user.Email, user.Role.ToString());
    }

    public async Task<User> RegisterAsync(RegisterRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, out var role))
            throw new ArgumentException($"Invalid role: {request.Role}");

        // Password strength: min 8 chars, at least 1 uppercase, at least 1 special character
        if (request.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");
        if (!request.Password.Any(char.IsUpper))
            throw new ArgumentException("Password must contain at least one uppercase letter.");
        if (!request.Password.Any(c => !char.IsLetterOrDigit(c)))
            throw new ArgumentException("Password must contain at least one special character (e.g. @, #, !, $).");

        var existing = await _users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
        if (existing != null)
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            FullName     = request.FullName,
            Email        = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role         = role,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
        };

        await _users.InsertOneAsync(user);

        // Auto-create a Driver document when registering a Driver-role user
        if (role == UserRole.Driver)
        {
            var driver = new Driver
            {
                UserId    = user.Id!,
                VehicleId = string.Empty,
                Status    = DriverStatus.Available,
                UpdatedAt = DateTime.UtcNow,
            };
            await _drivers.InsertOneAsync(driver);
        }

        return user;
    }
}
