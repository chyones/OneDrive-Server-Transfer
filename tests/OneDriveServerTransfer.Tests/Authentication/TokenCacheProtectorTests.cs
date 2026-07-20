using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Tests.Authentication;

public class TokenCacheProtectorTests
{
    private readonly DpapiTokenCacheProtector _protector = new();

    [Fact]
    public void RoundTripRestoresBytesOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // DPAPI is Windows-only; CI executes this check there.
        }

        var original = "cache-bytes"u8.ToArray();

        var protectedBytes = _protector.Protect(original);
        var restored = _protector.Unprotect(protectedBytes);

        Assert.NotEqual(original, protectedBytes);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void TamperedBytesThrowCorruptionOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // DPAPI is Windows-only; CI executes this check there.
        }

        var protectedBytes = _protector.Protect("cache-bytes"u8.ToArray());
        protectedBytes[0] ^= 0xFF;
        protectedBytes[^1] ^= 0xFF;

        Assert.Throws<TokenCacheCorruptionException>(() => _protector.Unprotect(protectedBytes));
    }

    [Fact]
    public void GarbageBytesThrowCorruptionOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // DPAPI is Windows-only; CI executes this check there.
        }

        Assert.Throws<TokenCacheCorruptionException>(() => _protector.Unprotect([1, 2, 3, 4]));
    }
}
