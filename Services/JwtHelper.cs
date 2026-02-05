using System.Text.Json;

namespace McpVersionVer2.Services;

/// <summary>
/// Helper for extracting claims from JWT tokens without full validation
/// </summary>
public static class JwtHelper
{
    /// <summary>
    /// Extracts the user identifier from a JWT token payload
    /// Looks for common claim names: sub, user_id, userId, username
    /// </summary>
    public static string? ExtractUserId(string bearerToken)
    {
        try
        {
            // JWT format: header.payload.signature
            var parts = bearerToken.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }

            var payload = parts[1];
            
            // Base64Url decode
            var base64 = payload.Replace('-', '+').Replace('_', '/');
            var padding = (4 - (base64.Length % 4)) % 4;
            base64 += new string('=', padding);
            
            var bytes = Convert.FromBase64String(base64);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            
            var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (claims == null) return null;
            
            string[] userClaimNames = { "sub", "user_id", "userId", "username", "name", "email" };
            
            foreach (var claimName in userClaimNames)
            {
                if (claims.TryGetValue(claimName, out var value))
                {
                    if (value.ValueKind == JsonValueKind.String)
                    {
                        var userId = value.GetString();
                        if (!string.IsNullOrWhiteSpace(userId))
                        {
                            return userId;
                        }
                    }
                    else if (value.ValueKind == JsonValueKind.Number)
                    {
                        return value.ToString();
                    }
                }
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }
}
