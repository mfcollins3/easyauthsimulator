using System.Net;
using System.Security.Claims;
using System.Text;
using EasyAuthSimulator.Options;
using EasyAuthSimulator.Providers;
using EasyAuthSimulator.Tests.TestSupport;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace EasyAuthSimulator.Tests;

public sealed class EntraIdProviderTests
{
    [Fact]
    public void ResolvePrincipalId_PrefersSubClaim_OverOid()
    {
        var provider = CreateProvider();
        var principal = CreatePrincipal(("sub", "sub-value"), ("oid", "oid-value"));

        Assert.Equal("sub-value", provider.ResolvePrincipalId(principal));
    }

    [Fact]
    public void ResolvePrincipalId_FallsBackToOid_WhenSubMissing()
    {
        var provider = CreateProvider();
        var principal = CreatePrincipal(("oid", "oid-value"));

        Assert.Equal("oid-value", provider.ResolvePrincipalId(principal));
    }

    [Fact]
    public void ResolvePrincipalId_ReturnsNull_WhenNeitherClaimPresent()
    {
        var provider = CreateProvider();
        var principal = CreatePrincipal();

        Assert.Null(provider.ResolvePrincipalId(principal));
    }

    [Fact]
    public void ResolvePrincipalName_PrefersPreferredUsername_OverEmailAndName()
    {
        var provider = CreateProvider();
        var principal = CreatePrincipal(
            ("preferred_username", "pu@example.com"),
            (ClaimTypes.Email, "email@example.com"),
            ("name", "Display Name"));

        Assert.Equal("pu@example.com", provider.ResolvePrincipalName(principal));
    }

    [Fact]
    public void ResolvePrincipalName_FallsBackToEmail_WhenPreferredUsernameMissing()
    {
        var provider = CreateProvider();
        var principal = CreatePrincipal((ClaimTypes.Email, "email@example.com"), ("name", "Display Name"));

        Assert.Equal("email@example.com", provider.ResolvePrincipalName(principal));
    }

    [Fact]
    public void ResolvePrincipalName_FallsBackToName_WhenPreferredUsernameAndEmailMissing()
    {
        var provider = CreateProvider();
        var principal = CreatePrincipal(("name", "Display Name"));

        Assert.Equal("Display Name", provider.ResolvePrincipalName(principal));
    }

    [Fact]
    public void ResolvePrincipalName_ReturnsNull_WhenNoNameClaimsPresent()
    {
        var provider = CreateProvider();
        var principal = CreatePrincipal();

        Assert.Null(provider.ResolvePrincipalName(principal));
    }

    [Fact]
    public void ProjectClaims_ExcludesTheInternalIdentityProviderClaim()
    {
        var provider = CreateProvider();
        var principal = CreatePrincipal(("sub", "sub-value"), (EasyAuthClaimTypes.IdentityProvider, EntraIdProvider.ProviderName));

        var projected = provider.ProjectClaims(principal).ToArray();

        Assert.Contains(projected, c => c.Type == "sub");
        Assert.DoesNotContain(projected, c => c.Type == EasyAuthClaimTypes.IdentityProvider);
    }

    [Fact]
    public async Task RefreshTokensAsync_ReturnsNull_WhenRefreshTokenMissing()
    {
        var provider = CreateProvider();

        var result = await provider.RefreshTokensAsync(new Dictionary<string, string>(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokensAsync_ReturnsNull_WhenRefreshTokenEmpty()
    {
        var provider = CreateProvider();

        var result = await provider.RefreshTokensAsync(
            new Dictionary<string, string> { ["refresh_token"] = "" }, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokensAsync_ReturnsNull_WhenTokenEndpointReturnsFailureStatus()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var provider = CreateProvider(handler);

        var result = await provider.RefreshTokensAsync(
            new Dictionary<string, string> { ["refresh_token"] = "old-refresh-token" }, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokensAsync_ReturnsNull_WhenPayloadIsNull()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json"),
        });
        var provider = CreateProvider(handler);

        var result = await provider.RefreshTokensAsync(
            new Dictionary<string, string> { ["refresh_token"] = "old-refresh-token" }, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokensAsync_PostsExpectedFormFields_ToTenantScopedTokenEndpoint_AndMergesTokens()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"new-access-token","expires_in":3600}""", Encoding.UTF8, "application/json"),
            };
        });
        var provider = CreateProvider(handler, scopes: ["openid", "offline_access"]);
        var currentTokens = new Dictionary<string, string>
        {
            ["refresh_token"] = "old-refresh-token",
            ["id_token"] = "old-id-token",
        };

        var result = await provider.RefreshTokensAsync(currentTokens, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://login.microsoftonline.com/test-tenant/oauth2/v2.0/token", capturedRequest.RequestUri!.ToString());
        Assert.Contains("client_id=test-client", capturedBody);
        Assert.Contains("grant_type=refresh_token", capturedBody);
        Assert.Contains("refresh_token=old-refresh-token", capturedBody);
        Assert.Contains("scope=openid+offline_access", capturedBody);

        Assert.NotNull(result);
        Assert.Equal("new-access-token", result!["access_token"]);
        // The fake token response omits id_token/refresh_token, so the caller's current values carry over.
        Assert.Equal("old-id-token", result["id_token"]);
        Assert.Equal("old-refresh-token", result["refresh_token"]);
        Assert.True(result.ContainsKey("expires_at"));
    }

    [Fact]
    public async Task RefreshTokensAsync_UsesAuthorityHostOverride_ForSovereignClouds()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"new-access-token","expires_in":3600}""", Encoding.UTF8, "application/json"),
            };
        });
        var provider = CreateProvider(handler, authorityHost: "login.microsoftonline.us");

        await provider.RefreshTokensAsync(
            new Dictionary<string, string> { ["refresh_token"] = "old-refresh-token" }, CancellationToken.None);

        Assert.Equal("https://login.microsoftonline.us/test-tenant/oauth2/v2.0/token", capturedRequest!.RequestUri!.ToString());
    }

    private static EntraIdProvider CreateProvider(
        HttpMessageHandler? handler = null, string authorityHost = "login.microsoftonline.com", string[]? scopes = null)
    {
        var options = OptionsFactory.Create(new AadProviderOptions
        {
            TenantId = "test-tenant",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            AuthorityHost = authorityHost,
            Scopes = scopes ?? ["openid", "profile", "email", "offline_access"],
        });
        var factory = new FakeHttpClientFactory(handler ?? new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)));
        return new EntraIdProvider(options, factory);
    }

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value))));
}
