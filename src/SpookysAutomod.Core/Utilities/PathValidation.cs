namespace SpookysAutomod.Core.Utilities;

/// <summary>
/// Helpers for validating that user-supplied paths are safe to embed in external command lines.
/// </summary>
public static class PathValidation
{
    /// <summary>
    /// Returns true if the path contains characters that could break out of a double-quoted
    /// argument on a cmd.exe command line: a double-quote closes the quoted argument (letting the
    /// remainder be parsed as additional commands), and a control character such as a newline
    /// could start a new command. Inside double quotes cmd treats metacharacters like &amp;, |,
    /// &lt;, &gt;, ^ and % literally, so those (which are legal in Windows paths) are NOT rejected.
    /// A double-quote and control characters are illegal in Windows paths, so a real directory
    /// path never contains them.
    /// </summary>
    public static bool ContainsCommandLineUnsafeChars(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        foreach (var c in path)
        {
            if (c == '"' || char.IsControl(c))
                return true;
        }
        return false;
    }
}
