using System.Security.Cryptography;

namespace SpookysAutomod.Core.Utilities;

/// <summary>
/// File hashing helpers used to verify the integrity of downloaded tools before they are
/// extracted and executed.
/// </summary>
public static class FileHash
{
    /// <summary>Compute the lowercase hex SHA-256 of a file's contents.</summary>
    public static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    /// <summary>Case-insensitive comparison of a file's SHA-256 against an expected hex hash.</summary>
    public static bool VerifySha256(string path, string expectedHex) =>
        string.Equals(Sha256(path), expectedHex, StringComparison.OrdinalIgnoreCase);
}
