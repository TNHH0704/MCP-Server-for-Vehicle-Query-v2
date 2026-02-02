using System.Security.Cryptography;
using System.Text;

namespace McpVersionVer2.Security;

/// <summary>
/// Utility for generating token hashes for rate limiting partition keys.
/// Uses SHA256 and stores only partial hash (first 16 hex chars) to avoid exposing full tokens.
/// </summary>
public static class TokenHashHelper
{
    /// <summary>
    /// Get hash of token for rate limiting using SHA256.
    /// Only returns partial hash (first 16 hex chars) to avoid exposing full token.
    /// </summary>
    public static string GetTokenHash(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "anonymous";
        }

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash)[..16];
    }
}
