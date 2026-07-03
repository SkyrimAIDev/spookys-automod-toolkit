using SpookysAutomod.Core.Utilities;

namespace SpookysAutomod.Tests.Core;

public class PathValidationTests
{
    [Theory]
    [InlineData(@"C:\Games\Steam\steamapps\common\Skyrim Special Edition\Data\Scripts\Source")]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Skyrim")]
    [InlineData(@"C:\Games\Skyrim & Friends\Source")]   // '&' is legal in a path and safe inside cmd quotes
    [InlineData(@"C:\path with (parens) and %vars%")]   // '%' is safe inside cmd double quotes
    [InlineData("")]
    public void ContainsCommandLineUnsafeChars_SafePaths_ReturnsFalse(string path)
    {
        Assert.False(PathValidation.ContainsCommandLineUnsafeChars(path));
    }

    [Theory]
    [InlineData("C:\\Games\" & calc.exe & echo \"")]    // double-quote breaks out of the quoted arg
    [InlineData("C:\\Games\r\nshutdown /s")]            // CR/LF could inject a second command
    [InlineData("C:\\Games\tx")]                        // other control chars
    [InlineData("C:\\Games\0x")]
    public void ContainsCommandLineUnsafeChars_UnsafePaths_ReturnsTrue(string path)
    {
        Assert.True(PathValidation.ContainsCommandLineUnsafeChars(path));
    }
}
