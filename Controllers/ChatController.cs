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
            var sessionId = Request.Headers["X-Session-Id"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(sessionId))
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var bearerToken = authHeader.Substring("Bearer ".Length).Trim();
                    sessionId = _sessionService.GetOrCreateSessionId(bearerToken);
                    _logger.LogInformation("[Chat] Auto-determined session ID from token: {SessionId}", sessionId);
                }
                else
                {
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
            
            try
            {
                bool isToolResponseCall = false;
                if (request.Messages is System.Text.Json.JsonElement messagesElement && 
                    messagesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    isToolResponseCall = messagesElement.EnumerateArray().Any(m => 
                        m.TryGetProperty("role", out var role) && role.GetString() == "tool");
                }
                
                if (!isToolResponseCall && 
                    request.Messages is System.Text.Json.JsonElement userMessagesElement && 
                    userMessagesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var messagesArray = userMessagesElement.EnumerateArray().ToList();
                    var lastUserMessage = messagesArray.LastOrDefault(m => 
                        m.TryGetProperty("role", out var role) && role.GetString() == "user");
                    
                    if (lastUserMessage.ValueKind != System.Text.Json.JsonValueKind.Undefined &&
                        lastUserMessage.TryGetProperty("content", out var userContent))
                    {
                        var userText = userContent.GetString();
                        if (!string.IsNullOrEmpty(userText))
                        {
                            _contextService.AddMessage(sessionId, new ConversationEntry
                            {
                                Role = "user",
                                Message = userText,
                                Timestamp = DateTime.UtcNow
                            });
                            _logger.LogDebug("[Chat] Saved user message for session {SessionId}", sessionId);
                        }
                    }
                }
                
                var responseJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
                if (responseJson.TryGetProperty("choices", out var choices) && 
                    choices.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var firstChoice = choices.EnumerateArray().FirstOrDefault();
                    if (firstChoice.ValueKind != System.Text.Json.JsonValueKind.Undefined &&
                        firstChoice.TryGetProperty("message", out var message))
                    {
                        if (message.TryGetProperty("content", out var assistantContent))
                        {
                            var assistantText = assistantContent.GetString();
                            if (!string.IsNullOrEmpty(assistantText))
                            {
                                _contextService.AddMessage(sessionId, new ConversationEntry
                                {
                                    Role = "assistant",
                                    Message = assistantText,
                                    Timestamp = DateTime.UtcNow
                                });
                                _logger.LogDebug("[Chat] Saved assistant response for session {SessionId}", sessionId);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Chat] Failed to save conversation messages for session {SessionId}", sessionId);
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

