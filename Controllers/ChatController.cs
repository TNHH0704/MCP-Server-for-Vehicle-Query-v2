using McpVersionVer2.Models;
using McpVersionVer2.Services;
using Microsoft.AspNetCore.Mvc;

namespace McpVersionVer2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatController> _logger;
    private readonly IConversationContextService _contextService;
    private readonly ISessionStorageService _sessionService;

    public ChatController(
        IHttpClientFactory httpClientFactory, 
        IConfiguration configuration, 
        ILogger<ChatController> logger,
        IConversationContextService contextService,
        ISessionStorageService sessionService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _contextService = contextService;
        _sessionService = sessionService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest request)
    {
        _logger.LogInformation("[Proxy] Received chat request...");

        try
        {
            // Extract session ID from request headers, or auto-generate from bearer token
            var sessionId = Request.Headers["X-Session-Id"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(sessionId))
            {
                // Get bearer token and auto-determine session
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var bearerToken = authHeader.Substring("Bearer ".Length).Trim();
                    sessionId = _sessionService.GetOrCreateSessionId(bearerToken);
                    _logger.LogInformation("[Chat] Auto-determined session ID from token: {SessionId}", sessionId);
                }
                else
                {
                    // Anonymous session
                    sessionId = _sessionService.CreateAnonymousSession();
                    _logger.LogInformation("[Chat] Created anonymous session: {SessionId}", sessionId);
                }
            }
            else
            {
                _logger.LogInformation("[Chat] Session ID from header: {SessionId}", sessionId);
            }
            
            string? secureToken = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            if (string.IsNullOrEmpty(secureToken) || secureToken == "YOUR_FALLBACK_TOKEN_IF_NEEDED")
            {
                _logger.LogError("[Error] OpenAI Token is missing in Configuration!");
                return Problem("Configuration Error: OpenAI Token is missing.");
            }

            var azureUrl = "https://models.inference.ai.azure.com/chat/completions";
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {secureToken}");

            _logger.LogInformation("[Proxy] Forwarding to Azure...");

            var response = await client.PostAsJsonAsync(azureUrl, request);

            _logger.LogInformation($"[Proxy] Azure Responded: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[Azure Error] {ErrorBody}", errorBody);
                
                // Parse and handle content filter errors
                try
                {
                    var errorJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(errorBody);
                    if (errorJson.TryGetProperty("error", out var error) && 
                        error.TryGetProperty("code", out var code) && 
                        code.GetString() == "content_filter")
                    {
                        var message = error.TryGetProperty("message", out var msg) 
                            ? msg.GetString() 
                            : "Your message was filtered by content policy.";
                        
                        return BadRequest(new { error = "content_filter", message });
                    }
                }
                catch { 

                }
                
                return StatusCode((int)response.StatusCode, new { error = "api_error", message = errorBody });
            }

            var content = await response.Content.ReadAsStringAsync();
            
            // Track conversation messages for summarization
            if (!string.IsNullOrEmpty(sessionId) && request.Messages != null)
            {
                _logger.LogInformation("[Chat] Tracking messages for session {SessionId}", sessionId);
                try
                {
                    // Messages is sent as object, need to cast to list
                    if (request.Messages is System.Text.Json.JsonElement messagesElement && 
                        messagesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var messageCount = 0;
                        foreach (var msgElement in messagesElement.EnumerateArray())
                        {
                            if (msgElement.TryGetProperty("role", out var roleElement) &&
                                msgElement.TryGetProperty("content", out var contentElement))
                            {
                                var role = roleElement.GetString();
                                var messageContent = contentElement.GetString();
                                
                                if ((role == "user" || role == "assistant") && !string.IsNullOrEmpty(messageContent))
                                {
                                    _contextService.AddMessage(sessionId, new ConversationEntry
                                    {
                                        Role = role,
                                        Message = messageContent,
                                        Timestamp = DateTime.UtcNow
                                    });
                                    messageCount++;
                                }
                            }
                        }
                        _logger.LogInformation("[Chat] Tracked {Count} messages for session {SessionId}", messageCount, sessionId);
                    }
                    else
                    {
                        _logger.LogWarning("[Chat] Messages is not a JSON array for session {SessionId}", sessionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to track conversation messages for session {SessionId}", sessionId);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(sessionId))
                    _logger.LogWarning("[Chat] No session ID provided in request");
                else if (request.Messages == null)
                    _logger.LogWarning("[Chat] No messages in request");
            }
            
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CRITICAL EXCEPTION] {Message}", ex.Message);
            return Problem(ex.Message);
        }
    }
}

