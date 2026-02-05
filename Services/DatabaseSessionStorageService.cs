using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using McpVersionVer2.Data;
using McpVersionVer2.Data.Entities;
using McpVersionVer2.Models.Dto;
using Microsoft.EntityFrameworkCore;

namespace McpVersionVer2.Services;

/// <summary>
/// Database-backed session storage service that persists session mappings
/// </summary>
public class DatabaseSessionStorageService : ISessionStorageService
{
    private readonly IDbContextFactory<ConversationDbContext> _dbContextFactory;
    private readonly ILogger<DatabaseSessionStorageService> _logger;
    
    // Cache for performance (avoid DB lookup on every request)
    private readonly ConcurrentDictionary<string, string> _tokenToSessionCache = new();
    private readonly ConcurrentDictionary<string, CachedTokenPair> _sessionTokens = new();
    
    public DatabaseSessionStorageService(
        IDbContextFactory<ConversationDbContext> dbContextFactory,
        ILogger<DatabaseSessionStorageService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }
    
    public string GetOrCreateSessionId(string bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return "anonymous";
        }
        
        // Extract stable user ID from JWT token
        var userId = JwtHelper.ExtractUserId(bearerToken);
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Failed to extract user ID from JWT token, using token hash as fallback");
            userId = ComputeTokenHash(bearerToken);
        }
        
        // Check cache first (cache by user ID, not token)
        if (_tokenToSessionCache.TryGetValue(userId, out var cachedSessionId))
        {
            _logger.LogDebug("Retrieved session {SessionId} from cache for user {UserId}", cachedSessionId, userId);
            return cachedSessionId;
        }
        
        // Look up in database by UserId (not token hash)
        using var dbContext = _dbContextFactory.CreateDbContext();
        
        var session = dbContext.Sessions
            .FirstOrDefault(s => s.UserId == userId && !s.IsAnonymous);
        
        if (session != null)
        {
            // Update last accessed time
            session.LastAccessedAt = DateTime.UtcNow;
            dbContext.SaveChanges();
            
            // Cache for future requests (cache by user ID)
            _tokenToSessionCache[userId] = session.SessionId;
            
            _logger.LogDebug("Restored session {SessionId} for user {UserId}", session.SessionId, userId);
            return session.SessionId;
        }
        
        // Create new session for this user
        var newSessionId = GenerateSessionId();
        var newSession = new SessionEntity
        {
            SessionId = newSessionId,
            UserId = userId,
            BearerTokenHash = ComputeTokenHash(bearerToken), // Keep hash for reference
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            IsAnonymous = false
        };
        
        dbContext.Sessions.Add(newSession);
        dbContext.SaveChanges();
        
        // Cache for future requests (cache by user ID)
        _tokenToSessionCache[userId] = newSessionId;
        
        _logger.LogInformation("Created new session {SessionId} for user {UserId}", newSessionId, userId);
        return newSessionId;
    }
    
    public string? GetSessionId(string bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return null;
        }
        
        // Extract stable user ID from JWT token
        var userId = JwtHelper.ExtractUserId(bearerToken);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }
        
        // Check cache first (cache by user ID)
        if (_tokenToSessionCache.TryGetValue(userId, out var cachedSessionId))
        {
            return cachedSessionId;
        }
        
        // Look up in database by UserId
        using var dbContext = _dbContextFactory.CreateDbContext();
        
        var session = dbContext.Sessions
            .FirstOrDefault(s => s.UserId == userId && !s.IsAnonymous);
        
        if (session != null)
        {
            // Update last accessed time
            session.LastAccessedAt = DateTime.UtcNow;
            dbContext.SaveChanges();
            
            // Cache for future requests (cache by user ID)
            _tokenToSessionCache[userId] = session.SessionId;
            
            return session.SessionId;
        }
        
        return null;
    }
    
    public void ClearSession(string bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return;
        }
        
        // Extract stable user ID from JWT token
        var userId = JwtHelper.ExtractUserId(bearerToken);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }
        
        // Remove from cache (cache by user ID)
        _tokenToSessionCache.TryRemove(userId, out _);
        
        // Remove from database
        using var dbContext = _dbContextFactory.CreateDbContext();
        
        var session = dbContext.Sessions
            .FirstOrDefault(s => s.UserId == userId && !s.IsAnonymous);
        
        if (session != null)
        {
            dbContext.Sessions.Remove(session);
            dbContext.SaveChanges();
            
            _logger.LogInformation("Cleared session {SessionId} for user {UserId}", session.SessionId, userId);
        }
    }
    
    public IEnumerable<string> GetAllSessionIds()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.Sessions.Select(s => s.SessionId).ToList();
    }
    
    public void StoreSessionTokens(string sessionId, CachedTokenPair tokens)
    {
        _sessionTokens[sessionId] = tokens;
    }
    
    public CachedTokenPair? GetSessionTokens(string sessionId)
    {
        return _sessionTokens.TryGetValue(sessionId, out var tokens) ? tokens : null;
    }
    
    public void RemoveSessionTokens(string sessionId)
    {
        _sessionTokens.TryRemove(sessionId, out _);
    }
    
    public string CreateAnonymousSession()
    {
        var sessionId = GenerateSessionId();
        
        using var dbContext = _dbContextFactory.CreateDbContext();
        
        var session = new SessionEntity
        {
            SessionId = sessionId,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            IsAnonymous = true
        };
        
        dbContext.Sessions.Add(session);
        dbContext.SaveChanges();
        
        _logger.LogInformation("Created anonymous session {SessionId}", sessionId);
        return sessionId;
    }
    
    private static string GenerateSessionId()
    {
        return $"session_{Guid.NewGuid():N}";
    }
    
    private static string ComputeTokenHash(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
