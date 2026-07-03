using System.CommandLine;
using SpookysAutomod.Cli.Commands;

// Root command
var rootCommand = new RootCommand("Spooky's AutoMod Toolkit - LLM-friendly Skyrim mod creation");

// Add global options
var jsonOption = new Option<bool>(
    aliases: new[] { "--json", "-j" },
    description: "Output results as JSON for machine parsing");
rootCommand.AddGlobalOption(jsonOption);

var verboseOption = new Option<bool>(
    aliases: new[] { "--verbose", "-v" },
    description: "Enable verbose output");
rootCommand.AddGlobalOption(verboseOption);

// Add subcommands with shared options
rootCommand.AddCommand(EspCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(PapyrusCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(NifCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(ArchiveCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(McmCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(AudioCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(SkseCommands.Create(jsonOption, verboseOption));

// Run
// InvokeAsync returns System.CommandLine's own exit code (non-zero on parse errors or
// unhandled exceptions). On normal completion it returns 0, which would otherwise override
// any Environment.ExitCode a handler set to signal a failed operation (e.g. in --json mode).
// Honor the handler-set code when the invocation itself succeeded.
var invokeExitCode = await rootCommand.InvokeAsync(args);
return invokeExitCode != 0 ? invokeExitCode : Environment.ExitCode;
