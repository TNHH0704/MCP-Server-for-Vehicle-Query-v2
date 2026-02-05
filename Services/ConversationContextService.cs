using System.Collections.Concurrent;
using McpVersionVer2.Models;
using McpVersionVer2.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharpToken;

namespace McpVersionVer2.Services;

public interface IConversationContextService
{
    void AddMessage(string sessionId, ConversationEntry entry);
    List<ConversationEntry> GetRecentMessages(string sessionId, int? limit = null);
    void ClearSession(string sessionId);
    Task<string> GetFormattedContextAsync(string sessionId, string? systemPrompt = null);
    int GetMessageCount(string sessionId);
    int GetTokenCount(string sessionId);
    Task TriggerSummarizationIfNeededAsync(string sessionId);
}

public class InMemoryConversationContextService : IConversationContextService
{
    private readonly ConversationConfig _config;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ConversationEntry>> _sessions;
    private readonly ConcurrentDictionary<string, int> _sessionTokenCounts;
    private readonly GptEncoding _tokenizer;
    private readonly ILogger<InMemoryConversationContextService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<ConversationDbContext> _dbContextFactory;

    public InMemoryConversationContextService(
        IOptions<ConversationConfig> config,
        ILogger<InMemoryConversationContextService> logger,
        IServiceProvider serviceProvider,
        IDbContextFactory<ConversationDbContext> dbContextFactory)
    {
        _config = config.Value;
        _sessions = new ConcurrentDictionary<string, ConcurrentQueue<ConversationEntry>>();
        _sessionTokenCounts = new ConcurrentDictionary<string, int>();
        _tokenizer = GptEncoding.GetEncoding("cl100k_base");
        _logger = logger;
        _serviceProvider = serviceProvider;
        _dbContextFactory = dbContextFactory;
    }

    public void AddMessage(string sessionId, ConversationEntry entry)
    {
        entry.SessionId = sessionId;

        // Calculate token count for this message
        entry.TokenCount = CountTokens(entry.Message);

        var queue = _sessions.GetOrAdd(sessionId, _ => new ConcurrentQueue<ConversationEntry>());
        queue.Enqueue(entry);

        // Update running token count
        _sessionTokenCounts.AddOrUpdate(
            sessionId,
            entry.TokenCount,
            (_, currentCount) => currentCount + entry.TokenCount);

        while (queue.Count > _config.WindowSize)
        {
            if (queue.TryDequeue(out var dequeuedEntry))
            {
                // Subtract dequeued message tokens from running count
                _sessionTokenCounts.AddOrUpdate(
                    sessionId,
                    0,
                    (_, currentCount) => Math.Max(0, currentCount - dequeuedEntry.TokenCount));
            }
        }

        if (_config.MaxAge > TimeSpan.Zero)
        {
            PruneOldEntries(queue);
        }

        _logger.LogDebug("Added message to session {Session}, queue size: {Size}, tokens: {Tokens}",
            sessionId, queue.Count, entry.TokenCount);

        // Persist to database asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                // Ensure session exists
                if (!await dbContext.Sessions.AnyAsync(s => s.SessionId == sessionId))
                {
                    dbContext.Sessions.Add(new Data.Entities.SessionEntity
                    {
                        SessionId = sessionId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Add message entry with generated ID
                dbContext.ConversationEntries.Add(new Data.Entities.ConversationEntryEntity
                {
                    Id = entry.Id, // Use the ID from the ConversationEntry (already a GUID)
                    SessionId = sessionId,
                    Role = entry.Role,
                    Message = entry.Message,
                    ToolName = entry.ToolName,
                    TokenCount = entry.TokenCount,
                    Timestamp = entry.Timestamp
                });

                await dbContext.SaveChangesAsync();
                _logger.LogDebug("Persisted message to database for session {Session}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist message to database for session {Session}", sessionId);
            }
        });

        // Trigger summarization check asynchronously (fire-and-forget)
        _ = TriggerSummarizationIfNeededAsync(sessionId);
    }

    public List<ConversationEntry> GetRecentMessages(string sessionId, int? limit = null)
    {
        if (!_sessions.TryGetValue(sessionId, out var queue))
        {
            return new List<ConversationEntry>();
        }

        var count = Math.Min(limit ?? _config.WindowSize, queue.Count);
        return queue.TakeLast(count).Reverse().ToList();
    }

    public void ClearSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var queue))
        {
            _logger.LogInformation("Cleared conversation context for session {Session}", sessionId);
        }
    }

    public async Task<string> GetFormattedContextAsync(string sessionId, string? systemPrompt = null)
    {
        var sb = new System.Text.StringBuilder();

        // 1. Always include system prompt first (if provided)
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            sb.AppendLine("## System Instructions");
            sb.AppendLine(systemPrompt);
            sb.AppendLine();
        }

        // 2. Fetch summaries from database (ordered by sequence)
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var summaries = await dbContext.ConversationSummaries
                .Where(s => s.SessionId == sessionId)
                .OrderBy(s => s.SummarySequence)
                .ToListAsync();

            if (summaries.Any())
            {
                sb.AppendLine("## Conversation Summary");
                foreach (var summary in summaries)
                {
                    sb.AppendLine($"**Summary {summary.SummarySequence}** ({summary.MessageCount} messages, {summary.CreatedAt:yyyy-MM-dd HH:mm})");
                    sb.AppendLine(summary.Summary);
                    sb.AppendLine();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch summaries for session {Session}", sessionId);
        }

        // 3. Include recent messages (last K messages as configured)
        var messages = GetRecentMessages(sessionId, _config.SummaryPreserveLastK);

        if (messages.Any())
        {
            sb.AppendLine("## Recent Messages");
            sb.AppendLine($"Last {messages.Count} messages:");
            sb.AppendLine();

            foreach (var msg in messages)
            {
                var timestamp = msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                var role = msg.Role.ToUpper();
                var toolInfo = !string.IsNullOrEmpty(msg.ToolName) ? $" [{msg.ToolName}]" : "";

                sb.AppendLine($"[{timestamp}] {role}{toolInfo}: {msg.Message}");

                if (msg.Metadata != null && msg.Metadata.Any())
                {
                    sb.AppendLine($"  Metadata: {System.Text.Json.JsonSerializer.Serialize(msg.Metadata)}");
                }
            }
        }
        else if (string.IsNullOrEmpty(systemPrompt))
        {
            return string.Empty;
        }

        return sb.ToString();
    }

    public int GetMessageCount(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var queue) ? queue.Count : 0;
    }

    public int GetTokenCount(string sessionId)
    {
        return _sessionTokenCounts.TryGetValue(sessionId, out var count) ? count : 0;
    }

    public async Task TriggerSummarizationIfNeededAsync(string sessionId)
    {
        if (!_config.SummaryEnabled)
        {
            return;
        }

        var messageCount = GetMessageCount(sessionId);
        var tokenCount = GetTokenCount(sessionId);

        // Check if either threshold is exceeded
        var shouldSummarize = messageCount >= _config.SummaryThreshold ||
                             tokenCount >= _config.TokenBudgetForSummary;

        if (!shouldSummarize)
        {
            return;
        }

        _logger.LogInformation(
            "Summarization triggered for session {Session}: {Messages} messages, {Tokens} tokens",
            sessionId, messageCount, tokenCount);

        try
        {
            // Use service locator pattern to avoid circular dependency
            using var scope = _serviceProvider.CreateScope();
            var summarizationService = scope.ServiceProvider.GetService<IConversationSummarizationService>();

            if (summarizationService != null)
            {
                await summarizationService.SummarizeAsync(sessionId);
            }
            else
            {
                _logger.LogWarning("Summarization service not registered");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during summarization for session {Session}", sessionId);
        }
    }

    private int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        try
        {
            return _tokenizer.Encode(text).Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error counting tokens, using fallback estimation");
            return text.Length / 4;
        }
    }

    private void PruneOldEntries(ConcurrentQueue<ConversationEntry> queue)
    {
        var cutoffTime = DateTime.UtcNow - _config.MaxAge;

        var tempQueue = new Queue<ConversationEntry>();
        while (queue.TryDequeue(out var entry))
        {
            if (entry.Timestamp >= cutoffTime)
            {
                tempQueue.Enqueue(entry);
            }
        }

        while (tempQueue.Count > 0)
        {
            queue.Enqueue(tempQueue.Dequeue());
        }
    }
}
