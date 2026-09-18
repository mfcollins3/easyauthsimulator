using EasyAuthSimulator.Providers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAuthSimulator.Tests.TestSupport;

/// <summary>
/// Hosts the real simulator (Program.cs, unmodified) against a fake upstream and a fake
/// identity provider, so tests exercise the actual header-injection/gating/endpoint code
/// instead of a stand-in.
/// </summary>
public sealed class SimulatorFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _configOverrides = new();

    public required string UpstreamUrl { get; init; }

    public SimulatorFactory WithConfig(string key, string? value)
    {
        _configOverrides[key] = value;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var overrides = new Dictionary<string, string?>(_configOverrides)
            {
                ["EasyAuth:UpstreamUrl"] = UpstreamUrl,
                // Satisfies AddEntraId()'s ValidateOnStart(); the OIDC handler itself is never
                // exercised by these tests, since the "Test" scheme replaces it as default.
                ["EasyAuth:Aad:TenantId"] = "test-tenant",
                ["EasyAuth:Aad:ClientId"] = "test-client",
                ["EasyAuth:Aad:ClientSecret"] = "test-secret",
            };
            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IEasyAuthProvider, TestEasyAuthProvider>();
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = TestAuthHandler.SchemeName;
                o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                o.DefaultSignInScheme = TestAuthHandler.SchemeName;
            });
        });
    }
}
