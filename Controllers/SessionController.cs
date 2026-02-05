using McpVersionVer2.Services;
using Microsoft.AspNetCore.Mvc;

namespace McpVersionVer2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly ISessionStorageService _sessionService;
    private readonly ILogger<SessionController> _logger;

    public SessionController(
        ISessionStorageService sessionService,
        ILogger<SessionController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Get or create a session for the current authenticated user
    /// </summary>
    [HttpGet("current")]
    public IActionResult GetCurrentSession()
    {
        try
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized(new { error = "Bearer token required" });
            }

            var bearerToken = authHeader.Substring("Bearer ".Length).Trim();
            var sessionId = _sessionService.GetOrCreateSessionId(bearerToken);

            _logger.LogInformation("Retrieved session {SessionId} for user", sessionId);

            return Ok(new
            {
                sessionId,
                message = "Session retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session");
            return StatusCode(500, new { error = "Failed to retrieve session" });
        }
    }

    /// <summary>
    /// Clear the current user's session
    /// </summary>
    [HttpDelete("current")]
    public IActionResult ClearCurrentSession()
    {
        try
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized(new { error = "Bearer token required" });
            }

            var bearerToken = authHeader.Substring("Bearer ".Length).Trim();
            _sessionService.ClearSession(bearerToken);

            _logger.LogInformation("Cleared session for user");

            return Ok(new { message = "Session cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing session");
            return StatusCode(500, new { error = "Failed to clear session" });
        }
    }
}
