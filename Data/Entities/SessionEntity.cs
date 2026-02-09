namespace McpVersionVer2.Data.Entities;

public class SessionEntity
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public string? BearerTokenHash { get; set; }
    public string? UserId { get; set; }
    public bool IsAnonymous { get; set; }
    public string? Metadata { get; set; }
}
