using SpookysAutomod.Core.Logging;
using SpookysAutomod.Core.Models;
using SpookysAutomod.Papyrus.CliWrappers;

namespace SpookysAutomod.Papyrus.Services;

/// <summary>
/// High-level service for Papyrus script operations.
/// </summary>
public class PapyrusService
{
    private readonly IModLogger _logger;
    private readonly PapyrusCompilerWrapper _compiler;
    private readonly ChampollionWrapper _decompiler;

    public PapyrusService(IModLogger logger, string? toolsDir = null)
    {
        _logger = logger;
        _compiler = new PapyrusCompilerWrapper(logger, toolsDir);
        _decompiler = new ChampollionWrapper(logger, toolsDir);
    }

    /// <summary>
    /// Check if required tools are available.
    /// </summary>
    public ToolStatus GetToolStatus()
    {
        return new ToolStatus
        {
            CompilerAvailable = _compiler.IsAvailable(),
            CompilerPath = _compiler.GetToolPath(),
            DecompilerAvailable = _decompiler.IsAvailable(),
            DecompilerPath = _decompiler.GetToolPath()
        };
    }

    /// <summary>
    /// Download required tools.
    /// </summary>
    public async Task<Result> DownloadToolsAsync()
    {
        var compilerResult = await _compiler.DownloadAsync();
        if (!compilerResult.Success)
        {
            _logger.Warning($"Failed to download compiler: {compilerResult.Error}");
        }

        var decompilerResult = await _decompiler.DownloadAsync();
        if (!decompilerResult.Success)
        {
            _logger.Warning($"Failed to download decompiler: {decompilerResult.Error}");
        }

        if (!compilerResult.Success && !decompilerResult.Success)
        {
            return Result.Fail("Failed to download tools");
        }

        return Result.Ok("Tools downloaded successfully");
    }

    /// <summary>
    /// Compile Papyrus source files.
    /// </summary>
    public async Task<Result<CompileResult>> CompileAsync(
        string source,
        string outputDir,
        string headersDir,
        bool optimize = true)
    {
        return await _compiler.CompileAsync(source, outputDir, headersDir, null, optimize);
    }

    /// <summary>
    /// Decompile PEX files to source.
    /// </summary>
    public async Task<Result<DecompileResult>> DecompileAsync(
        string pexPath,
        string outputDir)
    {
        if (Directory.Exists(pexPath))
        {
            return await _decompiler.DecompileDirectoryAsync(pexPath, outputDir);
        }
        return await _decompiler.DecompileAsync(pexPath, outputDir);
    }

    /// <summary>
    /// Setup Papyrus script headers by copying from a Skyrim installation.
    /// Auto-detects Skyrim path if not provided.
    /// </summary>
    public Result<HeaderSetupResult> SetupHeaders(string? skyrimPath = null, string? targetDir = null)
    {
        try
        {
            // Determine target directory (skyrim-script-headers/ next to tools/)
            var headersTarget = targetDir ?? Path.GetFullPath(
                Path.Combine(_compiler.GetToolPath(), "..", "..", "..", "..", "skyrim-script-headers"));

            // If no path provided, try to auto-detect
            if (string.IsNullOrEmpty(skyrimPath))
            {
                skyrimPath = AutoDetectSkyrimPath();
                if (skyrimPath == null)
                {
                    return Result<HeaderSetupResult>.Fail(
                        "Could not auto-detect Skyrim installation",
                        suggestions: new List<string>
                        {
                            "Provide the path manually: papyrus setup-headers --skyrim-path \"C:/...\"",
                            "Common locations:",
                            "  C:/Program Files (x86)/Steam/steamapps/common/Skyrim Special Edition",
                            "  C:/Program Files/Steam/steamapps/common/Skyrim Special Edition",
                            "  D:/SteamLibrary/steamapps/common/Skyrim Special Edition"
                        });
                }
                _logger.Info($"Auto-detected Skyrim at: {skyrimPath}");
            }

            // Validate the Skyrim path
            if (!Directory.Exists(skyrimPath))
            {
                return Result<HeaderSetupResult>.Fail(
                    $"Skyrim directory not found: {skyrimPath}");
            }

            // Find script source directory
            var sourceDir = FindScriptSourceDir(skyrimPath);
            if (sourceDir == null)
            {
                return Result<HeaderSetupResult>.Fail(
                    "Script headers not found in Skyrim installation",
                    $"Searched in: {skyrimPath}",
                    new List<string>
                    {
                        "Install Creation Kit from Steam to get script headers",
                        "Headers are typically in Data/Scripts/Source/ or Data/Source/Scripts/",
                        "After installing CK, run this command again"
                    });
            }

            _logger.Info($"Found script headers at: {sourceDir}");

            // Create target directory
            Directory.CreateDirectory(headersTarget);

            // Copy .psc files
            var pscFiles = Directory.GetFiles(sourceDir, "*.psc", SearchOption.TopDirectoryOnly);
            var copiedCount = 0;
            var skippedCount = 0;

            foreach (var pscFile in pscFiles)
            {
                var destFile = Path.Combine(headersTarget, Path.GetFileName(pscFile));
                File.Copy(pscFile, destFile, overwrite: true);
                copiedCount++;
            }

            // Copy .flg flags file if found
            var flgFiles = Directory.GetFiles(sourceDir, "*.flg", SearchOption.TopDirectoryOnly);
            foreach (var flgFile in flgFiles)
            {
                var destFile = Path.Combine(headersTarget, Path.GetFileName(flgFile));
                File.Copy(flgFile, destFile, overwrite: true);
                copiedCount++;
            }

            // Also check parent directory for .flg (sometimes it's in Data/Scripts/)
            var parentDir = Directory.GetParent(sourceDir)?.FullName;
            if (parentDir != null)
            {
                var parentFlg = Directory.GetFiles(parentDir, "*.flg", SearchOption.TopDirectoryOnly);
                foreach (var flgFile in parentFlg)
                {
                    var destFile = Path.Combine(headersTarget, Path.GetFileName(flgFile));
                    if (!File.Exists(destFile))
                    {
                        File.Copy(flgFile, destFile, overwrite: true);
                        copiedCount++;
                    }
                }
            }

            if (copiedCount == 0)
            {
                return Result<HeaderSetupResult>.Fail(
                    "No script files found to copy",
                    $"Source directory: {sourceDir}",
                    new List<string>
                    {
                        "The directory exists but contains no .psc files",
                        "Reinstall Creation Kit to restore script headers"
                    });
            }

            _logger.Info($"Copied {copiedCount} file(s) to {headersTarget}");

            return Result<HeaderSetupResult>.Ok(new HeaderSetupResult
            {
                TargetDirectory = headersTarget,
                CopiedCount = copiedCount,
                SkippedCount = skippedCount,
                SourceDirectory = sourceDir
            });
        }
        catch (Exception ex)
        {
            return Result<HeaderSetupResult>.Fail(
                "Failed to setup headers",
                ex.Message,
                new List<string>
                {
                    "Check file permissions on both source and target directories",
                    "Ensure no other process has the files locked"
                });
        }
    }

    /// <summary>
    /// Auto-detect Skyrim SE installation path.
    /// </summary>
    private string? AutoDetectSkyrimPath()
    {
        // Common Steam library locations
        var candidates = new List<string>
        {
            @"C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition",
            @"C:\Program Files\Steam\steamapps\common\Skyrim Special Edition",
            @"D:\SteamLibrary\steamapps\common\Skyrim Special Edition",
            @"D:\Steam\steamapps\common\Skyrim Special Edition",
            @"E:\SteamLibrary\steamapps\common\Skyrim Special Edition",
            @"D:\Games\Steam\steamapps\common\Skyrim Special Edition",
            @"E:\Games\Steam\steamapps\common\Skyrim Special Edition",
        };

        // Try to read Steam's libraryfolders.vdf for additional paths
        try
        {
            var steamPaths = new[]
            {
                @"C:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam"
            };

            foreach (var steamPath in steamPaths)
            {
                var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    var content = File.ReadAllText(vdfPath);
                    // Simple extraction of "path" values from VDF
                    foreach (var line in content.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("\"path\""))
                        {
                            var parts = trimmed.Split('"');
                            if (parts.Length >= 4)
                            {
                                var libPath = parts[3].Replace("\\\\", "\\");
                                var skyrimInLib = Path.Combine(libPath, "steamapps", "common", "Skyrim Special Edition");
                                if (!candidates.Contains(skyrimInLib))
                                    candidates.Add(skyrimInLib);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore VDF parsing failures
        }

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "SkyrimSE.exe")))
            {
                _logger.Debug($"Found Skyrim at: {candidate}");
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Find the script source directory within a Skyrim installation.
    /// </summary>
    private string? FindScriptSourceDir(string skyrimPath)
    {
        // Skyrim SE (post-CK install) locations
        var candidates = new[]
        {
            Path.Combine(skyrimPath, "Data", "Scripts", "Source"),
            Path.Combine(skyrimPath, "Data", "Source", "Scripts"),
            // Some CK versions put them here
            Path.Combine(skyrimPath, "Data", "Scripts", "Source", "Base"),
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                var pscCount = Directory.GetFiles(candidate, "*.psc", SearchOption.TopDirectoryOnly).Length;
                if (pscCount > 0)
                {
                    _logger.Debug($"Found {pscCount} .psc files in: {candidate}");
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Validate a Papyrus source file (syntax check only).
    /// </summary>
    public Result<ValidationResult> ValidateScript(string pscPath)
    {
        if (!File.Exists(pscPath))
        {
            return Result<ValidationResult>.Fail($"File not found: {pscPath}");
        }

        string content;
        try
        {
            content = File.ReadAllText(pscPath);
        }
        catch (Exception ex)
        {
            return Result<ValidationResult>.Fail(
                $"Failed to read script: {ex.Message}",
                ex.StackTrace,
                new List<string> { "Ensure the file is readable and not locked by another process" });
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        // Basic validation checks
        var lines = content.Split('\n');
        var inFunction = false;
        var functionDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var lineNum = i + 1;

            // Check for scriptname
            if (i == 0 && !line.StartsWith("Scriptname", StringComparison.OrdinalIgnoreCase))
            {
                if (!line.StartsWith(";"))  // Allow comment at start
                    warnings.Add($"Line {lineNum}: Script should start with 'Scriptname'");
            }

            // Check function balance
            if (line.StartsWith("Function ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Event ", StringComparison.OrdinalIgnoreCase))
            {
                if (inFunction)
                    errors.Add($"Line {lineNum}: Nested function/event definition");
                inFunction = true;
                functionDepth++;
            }

            if (line.StartsWith("EndFunction", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("EndEvent", StringComparison.OrdinalIgnoreCase))
            {
                if (!inFunction)
                    errors.Add($"Line {lineNum}: EndFunction/EndEvent without matching start");
                inFunction = false;
                functionDepth--;
            }

            // Check for common issues
            if (line.Contains(";;"))
                warnings.Add($"Line {lineNum}: Double semicolon (might be typo)");

            if (line.EndsWith("\\") && !line.TrimEnd().EndsWith("\\\\"))
                warnings.Add($"Line {lineNum}: Line continuation not standard in Papyrus");
        }

        if (functionDepth != 0)
        {
            errors.Add($"Unbalanced function/event blocks (depth: {functionDepth})");
        }

        return Result<ValidationResult>.Ok(new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        });
    }
}

public class ToolStatus
{
    public bool CompilerAvailable { get; set; }
    public string CompilerPath { get; set; } = "";
    public bool DecompilerAvailable { get; set; }
    public string DecompilerPath { get; set; } = "";
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class HeaderSetupResult
{
    public string TargetDirectory { get; set; } = "";
    public string SourceDirectory { get; set; } = "";
    public int CopiedCount { get; set; }
    public int SkippedCount { get; set; }
}
