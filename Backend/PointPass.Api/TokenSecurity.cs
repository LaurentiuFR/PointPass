using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PointPass.Api;

public static partial class TokenSecurity
{
    public static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Hash(string? value)
    {
        if (value is null || !TokenPattern().IsMatch(value)) throw ApiFault.Invalid();
        return SHA256.HashData(Encoding.ASCII.GetBytes(value));
    }

    public static bool SecretEquals(string? supplied, string expected)
    {
        if (supplied is null || supplied.Length > 128) return false;
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }

    public static string DisplayName(string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value)) return "Guest";
        if (value.Length is < 2 or > 24 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c != ' ' && c != '_' && c != '-'))
            throw ApiFault.Invalid();
        return value;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
