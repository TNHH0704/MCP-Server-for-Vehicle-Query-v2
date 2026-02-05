using McpVersionVer2.Data;
using McpVersionVer2.Data.Entities;
using McpVersionVer2.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace McpVersionVer2.Services;

public interface IConversationSummarizationService
{
    Task SummarizeAsync(string sessionId);
}

public class ConversationSummarizationService : IConversationSummarizationService
{
    private readonly IConversationContextService _contextService;
    private readonly IGitHubOpenAIService _aiService;
    private readonly ConversationDbContext _dbContext;
    private readonly ConversationConfig _config;
    private readonly ILogger<ConversationSummarizationService> _logger;

    public ConversationSummarizationService(
        IConversationContextService contextService,
        IGitHubOpenAIService aiService,
        ConversationDbContext dbContext,
        IOptions<ConversationConfig> config,
        ILogger<ConversationSummarizationService> logger)
    {
        _contextService = contextService;
        _aiService = aiService;
        _dbContext = dbContext;
        _config = config.Value;
        _logger = logger;
    }

    public async Task SummarizeAsync(string sessionId)
    {
        try
        {
            // Get messages to summarize (all but the last K messages)
            var allMessages = _contextService.GetRecentMessages(sessionId);
            var messagesToSummarize = allMessages
                .OrderBy(m => m.Timestamp)
                .Take(allMessages.Count - _config.SummaryPreserveLastK)
                .ToList();

            if (messagesToSummarize.Count == 0)
            {
                _logger.LogInformation("No messages to summarize for session {Session}", sessionId);
                return;
            }

            // Generate summary using AI
            var summaryText = await _aiService.SummarizeConversationAsync(messagesToSummarize);
            
            if (string.IsNullOrWhiteSpace(summaryText))
            {
                _logger.LogWarning("AI service returned empty summary for session {Session}", sessionId);
                return;
            }

            // Calculate total tokens in summarized messages
            var totalTokens = messagesToSummarize.Sum(m => m.TokenCount);
            
            // Get or create session entity
            var session = await _dbContext.Sessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);
            
            if (session == null)
            {
                // Create new session
                session = new SessionEntity
                {
                    SessionId = sessionId,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    IsAnonymous = true
                };
                _dbContext.Sessions.Add(session);
            }
            else
            {
                session.LastAccessedAt = DateTime.UtcNow;
            }

            // Check existing summaries
            var existingSummaries = await _dbContext.ConversationSummaries
                .Where(s => s.SessionId == sessionId)
                .OrderBy(s => s.SummarySequence)
                .ToListAsync();

            // If we already have max summaries, remove the oldest
            if (existingSummaries.Count >= _config.MaxSummariesPerSession)
            {
                var oldestSummary = existingSummaries.First();
                _dbContext.ConversationSummaries.Remove(oldestSummary);
                _logger.LogInformation("Removed oldest summary (sequence {Sequence}) for session {Session}",
                    oldestSummary.SummarySequence, sessionId);
                
                // Shift sequence numbers down
                foreach (var summary in existingSummaries.Skip(1))
                {
                    summary.SummarySequence--;
                }
            }

            // Add new summary with next sequence number
            var newSequence = existingSummaries.Any() 
                ? existingSummaries.Max(s => s.SummarySequence) + 1 
                : 1;
            
            var newSummary = new ConversationSummaryEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                SessionId = sessionId,
                Summary = summaryText,
                SummarySequence = newSequence,
                MessageCount = messagesToSummarize.Count,
                TokenCount = totalTokens,
                CreatedAt = DateTime.UtcNow
            };
            
            _dbContext.ConversationSummaries.Add(newSummary);
            
            // Save changes
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation(
                "Created summary for session {Session}: {MessageCount} messages, {TokenCount} tokens, sequence {Sequence}",
                sessionId, messagesToSummarize.Count, totalTokens, newSequence);

            // Remove summarized messages from in-memory queue
            // Note: This is a simplified approach - in production, you might want to keep them in DB
            // and only remove from the active working set
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize conversation for session {Session}", sessionId);
            throw;
        }
    }
}
