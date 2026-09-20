using EasyAuthSimulator.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAuthSimulator.Tests;

public sealed class CustomOpenIdConnectServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCustomOpenIdConnect_Throws_WhenAProviderNameCollidesWithTheBuiltInAadProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EasyAuth:CustomOpenIdConnect:aad:ClientId"] = "client-1",
                ["EasyAuth:CustomOpenIdConnect:aad:Authority"] = "https://idp.example.com",
            })
            .Build();
        var builder = new EasyAuthBuilder(new ServiceCollection(), configuration);

        Assert.Throws<InvalidOperationException>(() => builder.AddCustomOpenIdConnect());
    }

    [Fact]
    public void AddCustomOpenIdConnect_IsANoOp_WhenNoCustomProvidersAreConfigured()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = new EasyAuthBuilder(services, configuration);

        builder.AddCustomOpenIdConnect();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(Providers.IEasyAuthProvider));
    }
}
