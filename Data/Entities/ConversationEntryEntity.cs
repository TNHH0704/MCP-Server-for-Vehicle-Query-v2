namespace McpVersionVer2.Data.Entities;

public class ConversationEntryEntity
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? ToolName { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public int TokenCount { get; set; }
}
