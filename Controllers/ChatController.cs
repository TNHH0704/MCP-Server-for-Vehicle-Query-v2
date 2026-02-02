using McpVersionVer2.Models;
using Microsoft.AspNetCore.Mvc;

namespace McpVersionVer2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ChatController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest request)
    {
        _logger.LogInformation("[Proxy] Received chat request...");

        try
        {
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
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CRITICAL EXCEPTION] {Message}", ex.Message);
            return Problem(ex.Message);
        }
    }
}

