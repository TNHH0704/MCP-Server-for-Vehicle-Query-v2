using Microsoft.Extensions.Options;
using McpVersionVer2.Models;

namespace McpVersionVer2.Services;

/// <summary>
/// Middleware that extracts Authorization bearer token and sets the RequestContextService token.
/// It ensures the token is cleared after the request. Auto-add of messages has been moved to an explicit controller endpoint.
/// </summary>
public class ConversationContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ConversationContextMiddleware> _logger;
    private readonly IConversationContextService _contextService;
    private readonly ConversationConfig _config;

    public ConversationContextMiddleware(RequestDelegate next, ILogger<ConversationContextMiddleware> logger, IConversationContextService contextService, IOptions<ConversationConfig> config)
    {
        _next = next;
        _logger = logger;
        _contextService = contextService;
        _config = config.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Read bearer token from Authorization header if present
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                RequestContextService.SetToken(token);
            }

            // NOTE: Auto-parsing of POST bodies and automatic context population was intentionally removed.
            // Clients should call the explicit API endpoint POST /api/ConversationContext/add to add user messages to context.

            await _next(context);
        }
        finally
        {
            RequestContextService.Clear();
        }
    }
}
