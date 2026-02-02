namespace McpVersionVer2.Services;

/// <summary>
/// Middleware to populate Mcp-Session-Id header from the sessionId query parameter
/// if the header is missing. This helps clients that cannot send custom headers
/// on EventSource (SSE) connections by passing sessionId as a query parameter.
/// </summary>
public class SessionHeaderMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionHeaderMiddleware> _logger;
    private readonly ISessionStorageService _sessionStorage;

    public SessionHeaderMiddleware(RequestDelegate next, ILogger<SessionHeaderMiddleware> logger, ISessionStorageService sessionStorage)
    {
        _next = next;
        _logger = logger;
        _sessionStorage = sessionStorage;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            if (context.Request?.Headers is { } headers && !headers.ContainsKey("Mcp-Session-Id"))
            {
                var qs = context.Request.Query["sessionId"].FirstOrDefault();
                if (!string.IsNullOrEmpty(qs))
                {
                    headers["Mcp-Session-Id"] = qs;
                    _logger.LogInformation("Injected Mcp-Session-Id header from query string: {SessionId}", qs);
                }
                // Remove the auto-generation for /sse endpoint
                // The MCP library handles session creation via POST requests
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SessionHeaderMiddleware encountered an error while attempting to inject Mcp-Session-Id header.");
        }

        await _next(context);
    }
}
