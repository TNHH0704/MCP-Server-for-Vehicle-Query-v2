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
    private readonly IDbContextFactory<ConversationDbContext> _dbContextFactory;
    private readonly ConversationConfig _config;
    private readonly ILogger<ConversationSummarizationService> _logger;

    public ConversationSummarizationService(
        IConversationContextService contextService,
        IGitHubOpenAIService aiService,
        IDbContextFactory<ConversationDbContext> dbContextFactory,
        IOptions<ConversationConfig> config,
        ILogger<ConversationSummarizationService> logger)
    {
        _contextService = contextService;
        _aiService = aiService;
        _dbContextFactory = dbContextFactory;
        _config = config.Value;
        _logger = logger;
    }

    public async Task SummarizeAsync(string sessionId)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var allMessagesFromDb = await dbContext.ConversationEntries
                .Where(e => e.SessionId == sessionId)
                .OrderBy(e => e.Timestamp)
                .Select(e => new ConversationEntry
                {
                    Id = e.Id,
                    SessionId = e.SessionId,
                    Timestamp = e.Timestamp,
                    Role = e.Role,
                    ToolName = e.ToolName!,
                    Message = e.Message,
                    TokenCount = e.TokenCount
                })
                .ToListAsync();

            var messagesToSummarize = allMessagesFromDb
                .Take(allMessagesFromDb.Count - _config.SummaryPreserveLastK)
                .ToList();

            if (messagesToSummarize.Count == 0)
            {
                _logger.LogInformation("No messages to summarize for session {Session} (total: {Total}, preserving: {Preserve})", 
                    sessionId, allMessagesFromDb.Count, _config.SummaryPreserveLastK);
                return;
            }

            _logger.LogInformation("Summarizing {Count} messages for session {Session}", messagesToSummarize.Count, sessionId);

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
            var session = await dbContext.Sessions
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
                dbContext.Sessions.Add(session);
            }
            else
            {
                session.LastAccessedAt = DateTime.UtcNow;
            }

            // Check existing summaries
            var existingSummaries = await dbContext.ConversationSummaries
                .Where(s => s.SessionId == sessionId)
                .OrderBy(s => s.SummarySequence)
                .ToListAsync();

            // If we already have max summaries, remove the oldest
            if (existingSummaries.Count >= _config.MaxSummariesPerSession)
            {
                var oldestSummary = existingSummaries.First();
                dbContext.ConversationSummaries.Remove(oldestSummary);
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
            
            dbContext.ConversationSummaries.Add(newSummary);
            
            await dbContext.SaveChangesAsync();
            
            _logger.LogInformation(
                "Created summary for session {Session}: {MessageCount} messages, {TokenCount} tokens, sequence {Sequence}",
                sessionId, messagesToSummarize.Count, totalTokens, newSequence);
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize conversation for session {Session}", sessionId);
            throw;
        }
    }
}
