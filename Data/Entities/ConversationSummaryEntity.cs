namespace McpVersionVer2.Data.Entities;

public class ConversationSummaryEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int SummarySequence { get; set; } // 1=oldest, 2=latest
    public int MessageCount { get; set; }
    public int TokenCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
