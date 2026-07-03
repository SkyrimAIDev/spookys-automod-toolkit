using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using SpookysAutomod.Core.Logging;
using SpookysAutomod.Core.Models;
using SpookysAutomod.Core.Utilities;

namespace SpookysAutomod.Papyrus.CliWrappers;

/// <summary>
/// Downloads external CLI tools from GitHub releases.
/// </summary>
public class ToolDownloader
{
    private readonly IModLogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string _toolsDir;

    public ToolDownloader(IModLogger logger, string? toolsDir = null)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5) // Reasonable timeout for downloads
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SpookysAutomod");

        // Default tools directory is next to the executable
        _toolsDir = toolsDir ?? Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "tools"
        );
    }

    public string ToolsDirectory => Path.GetFullPath(_toolsDir);

    /// <summary>
    /// Download a tool from GitHub releases.
    /// </summary>
    public async Task<Result<string>> DownloadFromGitHubAsync(
        string owner,
        string repo,
        string assetPattern,
        string targetFolder,
        string tag,
        string expectedSha256)
    {
        try
        {
            var targetDir = Path.Combine(ToolsDirectory, targetFolder);

            // Check if already downloaded
            if (Directory.Exists(targetDir) && Directory.GetFiles(targetDir).Length > 0)
            {
                _logger.Debug($"Tool already exists: {targetDir}");
                return Result<string>.Ok(targetDir);
            }

            _logger.Info($"Downloading {owner}/{repo}...");

            // Get the pinned release (by tag, not "latest") so the asset can't change out from under us.
            var releaseUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(releaseUrl);

            if (release?.Assets == null || release.Assets.Length == 0)
            {
                return Result<string>.Fail(
                    $"No releases found for {owner}/{repo}",
                    suggestions: new List<string>
                    {
                        "Check that the repository exists",
                        "Verify the repository has releases"
                    });
            }

            // Find matching asset
            var asset = release.Assets.FirstOrDefault(a =>
                MatchesPattern(a.Name, assetPattern));

            if (asset == null)
            {
                var available = string.Join(", ", release.Assets.Select(a => a.Name));
                return Result<string>.Fail(
                    $"No asset matching '{assetPattern}' found",
                    $"Available assets: {available}",
                    new List<string> { "Check the asset pattern matches available releases" });
            }

            // Download
            _logger.Info($"Downloading {asset.Name}...");
            var downloadPath = Path.Combine(Path.GetTempPath(), asset.Name);

            using (var response = await _httpClient.GetAsync(asset.BrowserDownloadUrl))
            {
                response.EnsureSuccessStatusCode();
                await using var fs = File.Create(downloadPath);
                await response.Content.CopyToAsync(fs);
            }

            // Verify integrity BEFORE extracting/executing the tool. Fail closed on any mismatch.
            if (!FileHash.VerifySha256(downloadPath, expectedSha256))
            {
                var actual = FileHash.Sha256(downloadPath);
                try { File.Delete(downloadPath); } catch { /* best effort */ }
                return Result<string>.Fail(
                    $"Integrity check failed for {asset.Name}",
                    $"Expected SHA-256 {expectedSha256}, got {actual}",
                    new List<string>
                    {
                        "The downloaded file does not match the pinned hash and was discarded.",
                        "The upstream release may have changed, or the download was corrupted/tampered with.",
                        "If the release was intentionally updated, bump the pinned tag and hash in PinnedTools."
                    });
            }

            // Extract
            Directory.CreateDirectory(targetDir);

            if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(downloadPath, targetDir, overwriteFiles: true);
            }
            else
            {
                // Single executable
                File.Copy(downloadPath, Path.Combine(targetDir, asset.Name), overwrite: true);
            }

            File.Delete(downloadPath);
            _logger.Info($"Downloaded to: {targetDir}");

            return Result<string>.Ok(targetDir);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(
                $"Failed to download tool: {ex.Message}",
                ex.StackTrace);
        }
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        // Simple pattern matching with * wildcard
        if (pattern.Contains('*'))
        {
            var parts = pattern.Split('*');
            var idx = 0;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                var found = name.IndexOf(part, idx, StringComparison.OrdinalIgnoreCase);
                if (found < idx) return false;
                idx = found + part.Length;
            }
            return true;
        }
        return name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get the appropriate asset pattern for the current OS.
    /// </summary>
    public static string GetPlatformAssetPattern(string baseName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"*{baseName}*win*.zip";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"*{baseName}*linux*.zip";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"*{baseName}*macos*.zip";

        return $"*{baseName}*.zip";
    }
}

internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("assets")]
    public GitHubAsset[]? Assets { get; set; }
}

internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}
