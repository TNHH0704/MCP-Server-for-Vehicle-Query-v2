using System.ComponentModel;
using McpVersionVer2.Services;
using McpVersionVer2.Utils;
using ModelContextProtocol.Server;
using static McpVersionVer2.Services.AppJsonSerializerOptions;

namespace McpVersionVer2.Tools;

/// <summary>
/// MCP tools for authentication operations
/// </summary>
[McpServerToolType]
public class AuthTools
{
    private readonly AuthService _authService;
    private readonly SecurityValidationService _securityService;
    private readonly ILogger<AuthTools> _logger;

    public AuthTools(AuthService authService, SecurityValidationService securityService, ILogger<AuthTools> logger)
    {
        _authService = authService;
        _securityService = securityService;
        _logger = logger;
    }

    [McpServerTool, Description("AUTH ONLY: Refresh an expired JWT access token using a refresh token. Returns new access token and refresh token. REJECT: non-auth queries.")]
    public async Task<string> RefreshToken(
        [Description("Refresh token received during login or previous refresh")] string refreshToken)
    {
        _logger.LogInformation("RefreshToken tool called; token provided: {HasToken}", !string.IsNullOrEmpty(refreshToken));
        var queryContext = "refresh token auth";
        var validation = await _securityService.ValidateQueryAsync(queryContext, "auth", "auth_tool");
        if (!validation.IsValid)
        {
            _logger.LogWarning("RefreshToken validation failed: {Reason}", validation.ToJsonResponse());
            return validation.ToJsonResponse();
        }

        try
        {
            if (!_securityService.IsValidBearerToken(refreshToken))
            {
                _logger.LogWarning("RefreshToken rejected invalid format");
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid refresh token format",
                    errorCode = "INVALID_TOKEN_FORMAT"
                }, Default);
            }

            var result = await _authService.RefreshAccessTokenAsync(refreshToken);

            if (result == null)
            {
                _logger.LogWarning("RefreshToken failed: refresh service returned null");
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Failed to refresh token. The refresh token may be invalid, expired, or revoked. Please log in again.",
                    errorCode = "REFRESH_FAILED"
                }, Default);
            }

            _logger.LogInformation("RefreshToken succeeded; issuing new access token (masked): {Masked}", MaskToken(result.AccessToken));

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    accessToken = result.AccessToken,
                    refreshToken = result.RefreshToken,
                    expiresIn = result.ExpiresIn,
                    tokenType = result.TokenType,
                    expiresAt = DateUtils.FormatForApiUtc(DateTime.UtcNow.AddSeconds(result.ExpiresIn))
                },
                message = "Token refreshed successfully"
            }, Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RefreshToken encountered an exception");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                errorCode = "INTERNAL_ERROR"
            }, Default);
        }
    }

    [McpServerTool, Description("AUTH ONLY: Login with username and password to get JWT bearer token. Returns access and refresh tokens. REJECT: non-auth queries.")]
    public async Task<string> Login(
        [Description("Username or phone number for login")] string username,
        [Description("Password for login")] string password)
    {
        _logger.LogInformation("Login tool called for username: {Username}", username);
        var queryContext = "user login authentication";
        var validation = await _securityService.ValidateQueryAsync(queryContext, "auth", "login_tool");
        if (!validation.IsValid)
        {
            _logger.LogWarning("Login validation failed: {Reason}", validation.ToJsonResponse());
            return validation.ToJsonResponse();
        }

        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Login attempted with missing credentials for username: {Username}", username);
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Username and password are required",
                    errorCode = "MISSING_CREDENTIALS"
                }, Default);
            }

            var result = await _authService.LoginAsync(username, password);

            if (result == null)
            {
                _logger.LogWarning("Login failed for username: {Username} - AuthService returned null", username);
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Login failed. Please check your credentials and try again.",
                    errorCode = "LOGIN_FAILED"
                }, Default);
            }

            // Log success with masked token
            _logger.LogInformation("Login successful for username: {Username}; issuing tokens (masked access={AccessMasked}, refresh={RefreshMasked})",
                username, MaskToken(result.AccessToken), MaskToken(result.RefreshToken));

            // Return minimal payload with only accessToken and refreshToken as requested
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken
            }, Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login encountered an exception for username: {Username}", username);
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                errorCode = "INTERNAL_ERROR"
            }, Default);
        }
    }

    private static string MaskToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return "(none)";
        if (token.Length <= 10) return token;
        return token.Substring(0, 6) + "..." + token.Substring(token.Length - 4);
    }
}
