namespace McpVersionVer2.Models.Domain.Conversation;

public class ConversationConfig
{
    public int WindowSize { get; set; } = 5;
    public int MaxTokens { get; set; } = 8000;
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(1);
    public bool AutoProvideContext { get; set; } = true;
    
    // Summarization configuration
    public bool SummaryEnabled { get; set; } = true;
    public int SummaryThreshold { get; set; } = 20;
    public int SummaryPreserveLastK { get; set; } = 10;
    public int SummaryMaxTokens { get; set; } = 512;
    public int TokenBudgetForSummary { get; set; } = 6000;
    public int MaxSummariesPerSession { get; set; } = 2;
}
