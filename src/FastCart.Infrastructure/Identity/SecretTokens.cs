using System.Security.Cryptography;
using System.Text;

namespace FastCart.Infrastructure.Identity;

/// <summary>
/// Small crypto helpers for the Telegram link/reset flows (§4.4): URL-safe random tokens,
/// hashing (codes are stored only as hashes), and constant-time comparison.
/// </summary>
internal static class SecretTokens
{
    /// <summary>High-entropy, URL-safe (base64url, no padding) random token for links/change tokens.</summary>
    public static string NewUrlToken(int bytes = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>A random 6-digit numeric code (zero-padded), drawn from a CSPRNG.</summary>
    public static string NewNumericCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    /// <summary>
    /// HMAC-SHA256 of <paramref name="value"/> keyed by a server-side pepper. Used for the
    /// low-entropy reset code so a database-only leak can't reverse it to the code.
    /// </summary>
    public static string Hmac(string value, string pepper)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>SHA-256 (hex) of a high-entropy token (the change token) for at-rest storage.</summary>
    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Constant-time string comparison; false if either side is null/empty.</summary>
    public static bool FixedTimeEquals(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    }
}
