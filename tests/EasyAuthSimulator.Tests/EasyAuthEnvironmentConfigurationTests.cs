using EasyAuthSimulator.Configuration;
using Microsoft.Extensions.Configuration;

namespace EasyAuthSimulator.Tests;

public sealed class EasyAuthEnvironmentConfigurationTests
{
    [Fact]
    public void Apply_MapsFlatEasyAuthVariables_ToNestedConfigurationKeys()
    {
        using var scope = new EnvironmentVariableScope(
            ("EASYAUTH_UPSTREAM_URL", "http://upstream.local"),
            ("EASYAUTH_PROVIDER", "aad"),
            ("EASYAUTH_REQUIRE_AUTHENTICATION", "false"),
            ("EASYAUTH_API_PREFIX", "/custom-auth"));

        var configuration = Build();

        Assert.Equal("http://upstream.local", configuration["EasyAuth:UpstreamUrl"]);
        Assert.Equal("aad", configuration["EasyAuth:DefaultProvider"]);
        Assert.Equal("false", configuration["EasyAuth:RequireAuthentication"]);
        Assert.Equal("/custom-auth", configuration["EasyAuth:ApiPrefix"]);
    }

    [Fact]
    public void Apply_MapsAadProviderVariables()
    {
        using var scope = new EnvironmentVariableScope(
            ("EASYAUTH_AAD_TENANT_ID", "tenant-1"),
            ("EASYAUTH_AAD_CLIENT_ID", "client-1"),
            ("EASYAUTH_AAD_CLIENT_SECRET", "secret-1"),
            ("EASYAUTH_AAD_AUTHORITY_HOST", "login.microsoftonline.us"));

        var configuration = Build();

        Assert.Equal("tenant-1", configuration["EasyAuth:Aad:TenantId"]);
        Assert.Equal("client-1", configuration["EasyAuth:Aad:ClientId"]);
        Assert.Equal("secret-1", configuration["EasyAuth:Aad:ClientSecret"]);
        Assert.Equal("login.microsoftonline.us", configuration["EasyAuth:Aad:AuthorityHost"]);
    }

    [Fact]
    public void Apply_SplitsSemicolonDelimitedLists_IntoIndexedKeys_AndTrimsEntries()
    {
        using var scope = new EnvironmentVariableScope(("EASYAUTH_EXCLUDED_PATHS", " /public/* ; /health "));

        var configuration = Build();

        Assert.Equal("/public/*", configuration["EasyAuth:ExcludedPaths:0"]);
        Assert.Equal("/health", configuration["EasyAuth:ExcludedPaths:1"]);
        Assert.Null(configuration["EasyAuth:ExcludedPaths:2"]);
    }

    [Fact]
    public void Apply_DropsEmptyEntries_InDelimitedLists()
    {
        using var scope = new EnvironmentVariableScope(
            ("EASYAUTH_ALLOWED_EXTERNAL_REDIRECT_HOSTS", "partner.example.com;;other.example.com"));

        var configuration = Build();

        Assert.Equal("partner.example.com", configuration["EasyAuth:AllowedExternalRedirectHosts:0"]);
        Assert.Equal("other.example.com", configuration["EasyAuth:AllowedExternalRedirectHosts:1"]);
        Assert.Null(configuration["EasyAuth:AllowedExternalRedirectHosts:2"]);
    }

    [Fact]
    public void Apply_SplitsScopes_AsADelimitedList()
    {
        using var scope = new EnvironmentVariableScope(("EASYAUTH_AAD_SCOPES", "openid;profile;offline_access"));

        var configuration = Build();

        Assert.Equal("openid", configuration["EasyAuth:Aad:Scopes:0"]);
        Assert.Equal("profile", configuration["EasyAuth:Aad:Scopes:1"]);
        Assert.Equal("offline_access", configuration["EasyAuth:Aad:Scopes:2"]);
    }

    [Fact]
    public void Apply_IgnoresEnvironmentVariables_WithNoKnownAlias()
    {
        using var scope = new EnvironmentVariableScope(("EASYAUTH_NOT_A_REAL_SETTING", "value"));

        var configuration = Build();

        Assert.Null(configuration["EasyAuth:NotARealSetting"]);
    }

    [Fact]
    public void Apply_MatchesAliasKeys_CaseInsensitively()
    {
        using var scope = new EnvironmentVariableScope(("easyauth_upstream_url", "http://upstream.local"));

        var configuration = Build();

        Assert.Equal("http://upstream.local", configuration["EasyAuth:UpstreamUrl"]);
    }

    private static IConfigurationRoot Build()
    {
        var builder = new ConfigurationBuilder();
        EasyAuthEnvironmentConfiguration.Apply(builder);
        return builder.Build();
    }

    /// <summary>Sets the given process environment variables for a test and restores their prior values on dispose.</summary>
    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly (string Key, string? Previous)[] _previous;

        public EnvironmentVariableScope(params (string Key, string Value)[] variables)
        {
            _previous = variables.Select(v => (v.Key, Environment.GetEnvironmentVariable(v.Key))).ToArray();
            foreach (var (key, value) in variables)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public void Dispose()
        {
            foreach (var (key, previous) in _previous)
            {
                Environment.SetEnvironmentVariable(key, previous);
            }
        }
    }
}
