using DeerFlow.WPF.Services;
using Xunit;

namespace DeerFlow.WPF.Tests.Services;

public class DpapiSecretStoreTests
{
    private readonly DpapiSecretStore _store = new();

    [Fact]
    public void Protect_Then_Unprotect_ReturnsOriginal()
    {
        const string plain = "sk-test-1234567890abcdef";

        var cipher = _store.Protect(plain);
        var result = _store.Unprotect(cipher);

        Assert.Equal(plain, result);
    }

    [Fact]
    public void Protect_DoesNotLeakPlainText()
    {
        const string plain = "super-secret-token";

        var cipher = _store.Protect(plain);

        Assert.DoesNotContain(plain, cipher);
        Assert.True(_store.IsProtected(cipher));
    }

    [Fact]
    public void Protect_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _store.Protect(string.Empty));
        Assert.Equal(string.Empty, _store.Unprotect(string.Empty));
    }

    [Fact]
    public void Unprotect_LegacyPlainText_ReturnsAsIs()
    {
        const string legacy = "plain-text-from-old-config";

        Assert.Equal(legacy, _store.Unprotect(legacy));
    }

    [Fact]
    public void IsProtected_ReturnsFalse_ForPlainText()
    {
        Assert.False(_store.IsProtected("plain"));
        Assert.False(_store.IsProtected(string.Empty));
    }

    [Fact]
    public void Protect_IsNonDeterministic_AcrossCalls()
    {
        const string plain = "same-input";
        Assert.NotEqual(_store.Protect(plain), _store.Protect(plain));
    }
}
