using Microsoft.AspNetCore.Mvc;
using McpVersionVer2.Services;
using McpVersionVer2.Models.Dto;

namespace McpVersionVer2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestModel request)
    {
        _logger.LogInformation("Login endpoint called for username: {Username}", request.Username);

        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Login attempted with missing credentials");
                return BadRequest(new
                {
                    success = false,
                    error = "Username and password are required",
                    errorCode = "MISSING_CREDENTIALS"
                });
            }

            var result = await _authService.LoginAsync(request.Username, request.Password);

            if (result == null)
            {
                _logger.LogWarning("Login failed for username: {Username}", request.Username);
                return Unauthorized(new
                {
                    success = false,
                    error = "Login failed. Please check your credentials and try again.",
                    errorCode = "LOGIN_FAILED"
                });
            }

            _logger.LogInformation("Login successful for username: {Username}", request.Username);

            return Ok(new AuthResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresIn = result.ExpiresIn,
                TokenType = result.TokenType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login encountered an exception for username: {Username}", request.Username);
            return StatusCode(500, new
            {
                success = false,
                error = "An internal error occurred during login",
                errorCode = "INTERNAL_ERROR"
            });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        _logger.LogInformation("Refresh token endpoint called");

        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                _logger.LogWarning("Refresh token attempted with empty token");
                return BadRequest(new
                {
                    success = false,
                    error = "Refresh token is required",
                    errorCode = "MISSING_REFRESH_TOKEN"
                });
            }

            var result = await _authService.RefreshAccessTokenAsync(request.RefreshToken);

            if (result == null)
            {
                _logger.LogWarning("Refresh token failed");
                return Unauthorized(new
                {
                    success = false,
                    error = "Failed to refresh token. The refresh token may be invalid, expired, or revoked. Please log in again.",
                    errorCode = "REFRESH_FAILED"
                });
            }

            _logger.LogInformation("Token refreshed successfully");

            return Ok(new AuthResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresIn = result.ExpiresIn,
                TokenType = result.TokenType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh token encountered an exception");
            return StatusCode(500, new
            {
                success = false,
                error = "An internal error occurred during token refresh",
                errorCode = "INTERNAL_ERROR"
            });
        }
    }
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class LoginRequestModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
}
