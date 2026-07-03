using SpookysAutomod.Core.Utilities;

namespace SpookysAutomod.Tests.Core;

public class FileHashTests
{
    [Fact]
    public void Sha256_MatchesKnownVector()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "hello"); // UTF-8, no BOM -> the 5 bytes "hello"
            // Well-known SHA-256 of "hello".
            Assert.Equal(
                "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
                FileHash.Sha256(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void VerifySha256_IsCaseInsensitive_AndDetectsMismatch()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "hello");
            Assert.True(FileHash.VerifySha256(path,
                "2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824"));
            Assert.False(FileHash.VerifySha256(path,
                "0000000000000000000000000000000000000000000000000000000000000000"));
        }
        finally { File.Delete(path); }
    }
}
