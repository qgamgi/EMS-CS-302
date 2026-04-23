using EMS.API.Models;
using MongoDB.Driver;

namespace EMS.API.Services;

public interface ISessionService
{
    Task<Session> CreateAsync(string jti, string userId, string fullName, string email, string role);
    Task EndAsync(string jti);
    Task<List<Session>> GetActiveAsync();
}

public class SessionService : ISessionService
{
    private readonly IMongoCollection<Session> _sessions;

    public SessionService(IMongoDatabase db)
    {
        _sessions = db.GetCollection<Session>("sessions");
    }

    public async Task<Session> CreateAsync(
        string jti, string userId, string fullName, string email, string role)
    {
        var session = new Session
        {
            Jti      = jti,
            UserId   = userId,
            FullName = fullName,
            Email    = email,
            Role     = role,
            LoginAt  = DateTime.UtcNow,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
        };

        await _sessions.InsertOneAsync(session);
        return session;
    }

    public async Task EndAsync(string jti)
    {
        var update = Builders<Session>.Update
            .Set(s => s.IsActive, false)
            .Set(s => s.LogoutAt, DateTime.UtcNow);

        await _sessions.UpdateOneAsync(s => s.Jti == jti && s.IsActive, update);
    }

    public async Task<List<Session>> GetActiveAsync()
    {
        return await _sessions
            .Find(s => s.IsActive)
            .SortByDescending(s => s.LoginAt)
            .ToListAsync();
    }
}
