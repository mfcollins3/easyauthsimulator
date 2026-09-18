using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EasyAuthSimulator.Headers;
using EasyAuthSimulator.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EasyAuthSimulator.Tests;

public sealed class EasyAuthContractTests : IAsyncLifetime
{
    private readonly EchoUpstream _upstream = new();

    public Task InitializeAsync() => _upstream.StartAsync();

    public Task DisposeAsync() => _upstream.DisposeAsync().AsTask();

    private SimulatorFactory CreateFactory(params (string Key, string? Value)[] overrides)
    {
        var factory = new SimulatorFactory { UpstreamUrl = _upstream.Url };
        foreach (var (key, value) in overrides)
        {
            factory.WithConfig(key, value);
        }

        return factory;
    }

    [Fact]
    public async Task ClientPrincipalHeader_DecodesToDocumentedEnvelope()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/some-path");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var encoded = body.GetProperty("headers").GetProperty(EasyAuthHeaderNames.ClientPrincipal).GetString();

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded!));
        using var envelope = JsonDocument.Parse(json);
        var root = envelope.RootElement;

        var propertyNames = root.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(["auth_typ", "claims", "name_typ", "role_typ"], propertyNames);
        Assert.Equal("test", root.GetProperty("auth_typ").GetString());
        Assert.Equal("name", root.GetProperty("name_typ").GetString());
        Assert.Equal("roles", root.GetProperty("role_typ").GetString());

        var claims = root.GetProperty("claims").EnumerateArray()
            .Select(c => (Type: c.GetProperty("typ").GetString(), Value: c.GetProperty("val").GetString()))
            .ToArray();
        Assert.Contains(claims, c => c is { Type: "roles", Value: "editor" });
        Assert.Contains(claims, c => c is { Type: "name", Value: "Test User" });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpoofedHeaders_AreStrippedBeforeReachingUpstream(bool anonymous)
    {
        await using var factory = CreateFactory(("EasyAuth:RequireAuthentication", "false"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/some-path");
        request.Headers.TryAddWithoutValidation(EasyAuthHeaderNames.ClientPrincipal, "forged");
        request.Headers.TryAddWithoutValidation(EasyAuthHeaderNames.TokenHeader("AAD", "ACCESS-TOKEN"), "forged-token");
        request.Headers.TryAddWithoutValidation("X-ZUMO-AUTH", "forged-zumo");
        if (anonymous)
        {
            request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var headers = body.GetProperty("headers");

        Assert.False(headers.TryGetProperty(EasyAuthHeaderNames.ClientPrincipal, out var forgedPrincipal) && forgedPrincipal.GetString() == "forged");
        Assert.False(headers.TryGetProperty(EasyAuthHeaderNames.TokenHeader("AAD", "ACCESS-TOKEN"), out var forgedToken) && forgedToken.GetString() == "forged-token");
        Assert.False(headers.TryGetProperty("X-ZUMO-AUTH", out _));
    }

    [Fact]
    public async Task Me_ReturnsDocumentedArraySchema_WhenAuthenticated()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/.auth/me");
        response.EnsureSuccessStatusCode();
        var array = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Array, array.ValueKind);
        var entry = array[0];
        Assert.Equal("test", entry.GetProperty("provider_name").GetString());
        Assert.Equal("test-sub-123", entry.GetProperty("user_id").GetString());
        Assert.True(entry.TryGetProperty("user_claims", out _));
        Assert.Equal("test-id-token", entry.GetProperty("id_token").GetString());
    }

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/.auth/me");
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_UpdatesTokensViaProvider()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/.auth/refresh");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("AllowAnonymous", HttpStatusCode.OK)]
    [InlineData("Return401", HttpStatusCode.Unauthorized)]
    [InlineData("Return403", HttpStatusCode.Forbidden)]
    [InlineData("RejectWith404", HttpStatusCode.NotFound)]
    public async Task UnauthenticatedClientAction_AppliesConfiguredBehavior(string action, HttpStatusCode expected)
    {
        await using var factory = CreateFactory(("EasyAuth:UnauthenticatedAction", action));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");
        var response = await client.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedClientAction_RedirectsToLoginPage_ByDefault()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/.auth/login/aad", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ExcludedPaths_BypassGlobalValidation()
    {
        await using var factory = CreateFactory(("EasyAuth:ExcludedPaths:0", "/public/*"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/public/health");
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ApiPrefix_RelocatesAuthSurface()
    {
        await using var factory = CreateFactory(("EasyAuth:ApiPrefix", "/custom-auth"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var relocated = await client.GetAsync("/custom-auth/me");
        Assert.Equal(HttpStatusCode.OK, relocated.StatusCode);

        var original = await client.GetAsync("/.auth/me");
        var originalBody = await original.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(originalBody.TryGetProperty("provider_name", out _));
    }
}
