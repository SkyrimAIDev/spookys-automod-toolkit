using SpookysAutomod.Core.Models;

namespace SpookysAutomod.Cli.Commands;

/// <summary>
/// Centralizes JSON output for CLI handlers so that a failed operation always writes the
/// serialized result AND sets a non-zero process exit code. Previously the --json branch of
/// each handler printed the result but never set Environment.ExitCode, so AI/script consumers
/// (the primary audience, which always passes --json) saw exit code 0 on failure.
/// </summary>
internal static class CliOutput
{
    /// <summary>Emit a non-generic result as JSON and set exit code 1 on failure.</summary>
    public static void EmitJson(Result result)
    {
        Console.WriteLine(result.ToJson(true));
        if (!result.Success) Environment.ExitCode = 1;
    }

    /// <summary>Emit a typed result as JSON and set exit code 1 on failure.</summary>
    public static void EmitJson<T>(Result<T> result)
    {
        Console.WriteLine(result.ToJson(true));
        if (!result.Success) Environment.ExitCode = 1;
    }

    /// <summary>
    /// Emit an anonymous/custom payload as JSON. Callers pass the success flag explicitly so the
    /// exit code reflects it (these payloads carry their own "success" property).
    /// </summary>
    public static void EmitJson(object payload, bool success)
    {
        Console.WriteLine(payload.ToJson());
        if (!success) Environment.ExitCode = 1;
    }
}
