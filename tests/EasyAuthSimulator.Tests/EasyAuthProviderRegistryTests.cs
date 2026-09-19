using EasyAuthSimulator.Providers;
using EasyAuthSimulator.Tests.TestSupport;

namespace EasyAuthSimulator.Tests;

public sealed class EasyAuthProviderRegistryTests
{
    [Fact]
    public void TryGet_FindsRegisteredProvider_CaseInsensitively()
    {
        var registry = new EasyAuthProviderRegistry([new TestEasyAuthProvider()]);

        Assert.True(registry.TryGet("TEST", out var provider));
        Assert.Equal(TestEasyAuthProvider.ProviderName, provider!.Name);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnknownProvider()
    {
        var registry = new EasyAuthProviderRegistry([new TestEasyAuthProvider()]);

        Assert.False(registry.TryGet("unknown", out var provider));
        Assert.Null(provider);
    }
}
