using System.CommandLine;
using SpookysAutomod.Core.Logging;
using SpookysAutomod.Core.Models;
using SpookysAutomod.Nif.CliWrappers;
using SpookysAutomod.Nif.Services;

namespace SpookysAutomod.Cli.Commands;

public static class NifCommands
{
    private static Option<bool> _jsonOption = null!;
    private static Option<bool> _verboseOption = null!;

    public static Command Create(Option<bool> jsonOption, Option<bool> verboseOption)
    {
        _jsonOption = jsonOption;
        _verboseOption = verboseOption;

        var nifCommand = new Command("nif", "NIF mesh file operations");

        nifCommand.AddCommand(CreateInfoCommand());
        nifCommand.AddCommand(CreateScaleCommand());
        nifCommand.AddCommand(CreateCopyCommand());

        // nif-tool commands
        nifCommand.AddCommand(CreateListTexturesCommand());
        nifCommand.AddCommand(CreateReplaceTexturesCommand());
        nifCommand.AddCommand(CreateListStringsCommand());
        nifCommand.AddCommand(CreateRenameStringsCommand());
        nifCommand.AddCommand(CreateShaderInfoCommand());
        nifCommand.AddCommand(CreateFixEyesCommand());
        nifCommand.AddCommand(CreateVerifyCommand());
        nifCommand.AddCommand(CreateRestoreCommand());

        return nifCommand;
    }

    private static IModLogger CreateLogger(bool json, bool verbose) =>
        json ? new SilentLogger() : new ConsoleLogger(verbose);

    private static Command CreateInfoCommand()
    {
        var nifArg = new Argument<string>("nif", "Path to the NIF file");

        var cmd = new Command("info", "Get information about a NIF file")
        {
            nifArg
        };

        cmd.SetHandler((nif, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new NifService(logger);

            var result = service.GetInfo(nif);

            if (json)
            {
                if (result.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            fileName = result.Value!.FileName,
                            fileSize = result.Value.FileSize,
                            version = result.Value.Version,
                            headerString = result.Value.HeaderString
                        }
                    }.ToJson());
                }
                else
                {
                    CliOutput.EmitJson(Result.Fail(result.Error!));
                }
            }
            else if (result.Success)
            {
                var info = result.Value!;
                Console.WriteLine($"NIF File: {info.FileName}");
                Console.WriteLine($"Size: {info.FileSize:N0} bytes");
                Console.WriteLine($"Header: {info.HeaderString}");
                if (!string.IsNullOrEmpty(info.Version))
                    Console.WriteLine($"Version: {info.Version}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                Environment.ExitCode = 1;
            }
        }, nifArg, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateScaleCommand()
    {
        var nifArg = new Argument<string>("nif", "Path to the NIF file");
        var factorArg = new Argument<float>("factor", "Scale factor (e.g., 1.5 for 150%)");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path (defaults to overwriting input)");

        var cmd = new Command("scale", "Scale a NIF mesh uniformly")
        {
            nifArg,
            factorArg,
            outputOption
        };

        cmd.SetHandler((nif, factor, output, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new NifService(logger);

            var outputPath = output ?? nif;
            var result = service.Scale(nif, factor, outputPath);

            if (json)
            {
                if (result.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            outputPath = result.Value,
                            scaleFactor = factor
                        }
                    }.ToJson());
                }
                else
                {
                    CliOutput.EmitJson(Result.Fail(result.Error!));
                }
            }
            else if (result.Success)
            {
                Console.WriteLine($"Scaled NIF by {factor}x: {result.Value}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                Environment.ExitCode = 1;
            }
        }, nifArg, factorArg, outputOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateCopyCommand()
    {
        var nifArg = new Argument<string>("nif", "Path to the source NIF file");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path") { IsRequired = true };

        var cmd = new Command("copy", "Copy a NIF file (validates format)")
        {
            nifArg,
            outputOption
        };

        cmd.SetHandler((nif, output, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new NifService(logger);

            var result = service.Copy(nif, output);

            if (json)
            {
                if (result.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            outputPath = result.Value
                        }
                    }.ToJson());
                }
                else
                {
                    CliOutput.EmitJson(Result.Fail(result.Error!));
                }
            }
            else if (result.Success)
            {
                Console.WriteLine($"Copied NIF to: {result.Value}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                Environment.ExitCode = 1;
            }
        }, nifArg, outputOption, _jsonOption, _verboseOption);

        return cmd;
    }

    #region nif-tool Commands

    private static NifService CreateNifToolService(bool json, bool verbose)
    {
        var logger = CreateLogger(json, verbose);
        return new NifService(logger, new NifToolWrapper(logger));
    }

    private static void HandleNifToolResult(Result<NifToolOutput> result, bool json)
    {
        if (json)
        {
            if (result.Success)
                Console.WriteLine(Result<object>.Ok(new { output = result.Value!.Output, dryRun = result.Value.DryRun }).ToJson(true));
            else
                CliOutput.EmitJson(Result.Fail(result.Error!, result.ErrorContext, result.Suggestions));
        }
        else if (result.Success)
        {
            Console.WriteLine(result.Value!.Output);
        }
        else
        {
            Console.Error.WriteLine($"Error: {result.Error}");
            if (!string.IsNullOrEmpty(result.ErrorContext))
                Console.Error.WriteLine(result.ErrorContext);
            Environment.ExitCode = 1;
        }
    }

    private static Command CreateListTexturesCommand()
    {
        var pathArg = new Argument<string>("path", "Path to NIF file or folder (recursive)");

        var cmd = new Command("list-textures", "List texture paths in NIF files (uses nif-tool)")
        {
            pathArg
        };

        cmd.SetHandler(async (path, json, verbose) =>
        {
            var service = CreateNifToolService(json, verbose);
            var result = await service.ListTexturesFromToolAsync(path);
            HandleNifToolResult(result, json);
        }, pathArg, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateReplaceTexturesCommand()
    {
        var pathArg = new Argument<string>("path", "Path to NIF file or folder (recursive)");
        var oldOption = new Option<string>("--old", "Substring to find (case-insensitive)") { IsRequired = true };
        var newOption = new Option<string>("--new", "Replacement string") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without writing files");
        var backupOption = new Option<bool>("--backup", () => true, "Create .nif.bak before overwriting");

        var cmd = new Command("replace-textures", "Replace texture path substrings in NIF files (uses nif-tool)")
        {
            pathArg, oldOption, newOption, dryRunOption, backupOption
        };

        cmd.SetHandler(async (path, oldStr, newStr, dryRun, backup, json, verbose) =>
        {
            var service = CreateNifToolService(json, verbose);
            var result = await service.ReplaceTexturesAsync(path, oldStr, newStr, dryRun, backup);
            HandleNifToolResult(result, json);
        }, pathArg, oldOption, newOption, dryRunOption, backupOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateListStringsCommand()
    {
        var pathArg = new Argument<string>("path", "Path to NIF file or folder (recursive)");

        var cmd = new Command("list-strings", "List string table entries in NIF files (uses nif-tool)")
        {
            pathArg
        };

        cmd.SetHandler(async (path, json, verbose) =>
        {
            var service = CreateNifToolService(json, verbose);
            var result = await service.ListStringsFromToolAsync(path);
            HandleNifToolResult(result, json);
        }, pathArg, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateRenameStringsCommand()
    {
        var pathArg = new Argument<string>("path", "Path to NIF file or folder (recursive)");
        var oldOption = new Option<string>("--old", "Substring to find (case-insensitive)") { IsRequired = true };
        var newOption = new Option<string>("--new", "Replacement string") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without writing files");
        var backupOption = new Option<bool>("--backup", () => true, "Create .nif.bak before overwriting");

        var cmd = new Command("rename-strings", "Rename string table entries in NIF files (uses nif-tool)")
        {
            pathArg, oldOption, newOption, dryRunOption, backupOption
        };

        cmd.SetHandler(async (path, oldStr, newStr, dryRun, backup, json, verbose) =>
        {
            var service = CreateNifToolService(json, verbose);
            var result = await service.RenameStringsAsync(path, oldStr, newStr, dryRun, backup);
            HandleNifToolResult(result, json);
        }, pathArg, oldOption, newOption, dryRunOption, backupOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateShaderInfoCommand()
    {
        var pathArg = new Argument<string>("path", "Path to NIF file or folder (recursive)");

        var cmd = new Command("shader-info", "Show shader flags on BSLightingShaderProperty blocks (uses nif-tool)")
        {
            pathArg
        };

        cmd.SetHandler(async (path, json, verbose) =>
        {
            var service = CreateNifToolService(json, verbose);
            var result = await service.GetShaderInfoAsync(path);
            HandleNifToolResult(result, json);
        }, pathArg, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateFixEyesCommand()
    {
        var pathArg = new Argument<string>("path", "Path to NIF file or folder (recursive)");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without writing files");
        var backupOption = new Option<bool>("--backup", () => true, "Create .nif.bak before overwriting");

        var cmd = new Command("fix-eyes", "Fix eye ghosting bug in FaceGen NIFs (uses nif-tool)")
        {
            pathArg, dryRunOption, backupOption
        };

        cmd.SetHandler(async (path, dryRun, backup, json, verbose) =>
        {
            var service = CreateNifToolService(json, verbose);
            var result = await service.FixEyesAsync(path, dryRun, backup);
            HandleNifToolResult(result, json);
        }, pathArg, dryRunOption, backupOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateVerifyCommand()
    {
        var pathArg = new Argument<string>("path", "Path to NIF file or folder (recursive)");

        var cmd = new Command("verify", "Verify byte-perfect roundtrip of NIF files (uses nif-tool)")
        {
            pathArg
        };

        cmd.SetHandler(async (path, json, verbose) =>
        {
            var service = CreateNifToolService(json, verbose);
            var result = await service.VerifyAsync(path);
            HandleNifToolResult(result, json);
        }, pathArg, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateRestoreCommand()
    {
        var pathArg = new Argument<string>("path", "Path to folder to restore .nif.bak files (recursive)");

        var cmd = new Command("restore", "Restore NIF files from .nif.bak backups (uses nif-tool)")
        {
            pathArg
        };

        cmd.SetHandler(async (path, json, verbose) =>
        {
            var service = CreateNifToolService(json, verbose);
            var result = await service.RestoreAsync(path);
            HandleNifToolResult(result, json);
        }, pathArg, _jsonOption, _verboseOption);

        return cmd;
    }

    #endregion
}
