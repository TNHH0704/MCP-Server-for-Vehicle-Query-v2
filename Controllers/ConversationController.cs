using McpVersionVer2.Data;
using McpVersionVer2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace McpVersionVer2.Controllers;

[ApiController]
[Route("api/conversation")]
[EnableRateLimiting("conversationApi")]
public class ConversationController : ControllerBase
{
    private readonly IConversationContextService _contextService;
    private readonly IConversationSummarizationService _summarizationService;
    private readonly ConversationDbContext _dbContext;
    private readonly ILogger<ConversationController> _logger;

    public ConversationController(
        IConversationContextService contextService,
        IConversationSummarizationService summarizationService,
        ConversationDbContext dbContext,
        ILogger<ConversationController> logger)
    {
        _contextService = contextService;
        _summarizationService = summarizationService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get the latest conversation summary for the current session
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "sessionId is required" });
        }

        try
        {
            var latestSummary = await _dbContext.ConversationSummaries
                .Where(s => s.SessionId == sessionId)
                .OrderByDescending(s => s.SummarySequence)
                .FirstOrDefaultAsync();

            if (latestSummary == null)
            {
                return Ok(new { hasSummary = false });
            }

            return Ok(new
            {
                hasSummary = true,
                summary = latestSummary.Summary,
                messageCount = latestSummary.MessageCount,
                tokenCount = latestSummary.TokenCount,
                createdAt = latestSummary.CreatedAt,
                sequence = latestSummary.SummarySequence
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get summary for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to retrieve summary" });
        }
    }

    /// <summary>
    /// Manually trigger conversation summarization
    /// </summary>
    [HttpPost("summarize")]
    public async Task<IActionResult> TriggerSummarization([FromBody] SummarizeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            return BadRequest(new { error = "sessionId is required" });
        }

        try
        {
            var messageCount = _contextService.GetMessageCount(request.SessionId);
            var tokenCount = _contextService.GetTokenCount(request.SessionId);

            if (messageCount == 0)
            {
                return BadRequest(new { error = "No messages to summarize" });
            }

            await _summarizationService.SummarizeAsync(request.SessionId);

            return Ok(new
            {
                success = true,
                message = "Summarization completed",
                messageCount,
                tokenCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger summarization for session {SessionId}", request.SessionId);
            return StatusCode(500, new { error = "Summarization failed" });
        }
    }

    /// <summary>
    /// Get conversation history with optional limit
    /// </summary>
    [HttpGet("history")]
    public IActionResult GetHistory([FromQuery] string sessionId, [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "sessionId is required" });
        }

        if (limit < 1 || limit > 20)
        {
            return BadRequest(new { error = "limit must be between 1 and 20" });
        }

        try
        {
            var messages = _contextService.GetRecentMessages(sessionId, limit);
            var tokenCount = _contextService.GetTokenCount(sessionId);

            return Ok(new
            {
                sessionId,
                messageCount = messages.Count,
                tokenCount,
                messages = messages.Select(m => new
                {
                    m.Id,
                    m.Timestamp,
                    m.Role,
                    m.ToolName,
                    m.Message,
                    m.TokenCount
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get history for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to retrieve history" });
        }
    }

    /// <summary>
    /// Get formatted context (system prompt + summaries + recent messages)
    /// </summary>
    [HttpGet("context")]
    public async Task<IActionResult> GetFormattedContext([FromQuery] string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "sessionId is required" });
        }

        try
        {
            var systemPrompt = "You are a helpful assistant for a vehicle fleet management system.";
            var context = await _contextService.GetFormattedContextAsync(sessionId, systemPrompt);

            return Ok(new
            {
                sessionId,
                context
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get formatted context for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to retrieve context" });
        }
    }
}

public record SummarizeRequest(string SessionId);
