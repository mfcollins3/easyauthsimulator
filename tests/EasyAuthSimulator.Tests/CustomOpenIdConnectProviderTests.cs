using System.Net;
using System.Security.Claims;
using System.Text;
using EasyAuthSimulator.Options;
using EasyAuthSimulator.Providers;
using EasyAuthSimulator.Tests.TestSupport;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace EasyAuthSimulator.Tests;

public sealed class CustomOpenIdConnectProviderTests
{
    [Fact]
    public void ResolvePrincipalId_ReturnsSubClaim()
    {
        var provider = CreateProvider();
        var principal = CreatePrincipal(("sub", "sub-value"));

        Assert.Equal("sub-value", provider.ResolvePrincipalId(principal));
    }

    [Fact]
    public void ResolvePrincipalId_ReturnsNull_WhenSubMissing()
    {
        var provider = CreateProvider();
        var principal = CreatePrincipal();

        Assert.Null(provider.ResolvePrincipalId(principal));
    }

    [Fact]
    public void ResolvePrincipalName_PrefersConfiguredNameClaimType_OverEmailAndSub()
    {
        var provider = CreateProvider(nameClaimType: "nickname");
        var principal = CreatePrincipal(("nickname", "nick"), (ClaimTypes.Email, "email@example.com"), ("sub", "sub-value"));

        Assert.Equal("nick", provider.ResolvePrincipalName(principal));
    }

    [Fact]
    public void ResolvePrincipalName_FallsBackToEmail_WhenNameClaimTypeMissing()
    {
        var provider = CreateProvider(nameClaimType: "nickname");
        var principal = CreatePrincipal((ClaimTypes.Email, "email@example.com"), ("sub", "sub-value"));

        Assert.Equal("email@example.com", provider.ResolvePrincipalName(principal));
    }

    [Fact]
    public void ResolvePrincipalName_FallsBackToSub_WhenNameClaimTypeAndEmailMissing()
    {
        var provider = CreateProvider(nameClaimType: "nickname");
        var principal = CreatePrincipal(("sub", "sub-value"));

        Assert.Equal("sub-value", provider.ResolvePrincipalName(principal));
    }

    [Fact]
    public void Name_TokenHeaderInfix_AndAuthenticationScheme_AreDerivedFromTheConfiguredProviderName()
    {
        var provider = CreateProvider(name: "github");

        Assert.Equal("github", provider.Name);
        Assert.Equal("GITHUB", provider.TokenHeaderInfix);
        Assert.Equal("github", provider.AuthenticationScheme);
    }

    [Fact]
    public void ProjectClaims_ExcludesTheInternalIdentityProviderClaim()
    {
        var provider = CreateProvider();
        var principal = CreatePrincipal(("sub", "sub-value"), (EasyAuthClaimTypes.IdentityProvider, "github"));

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
    public async Task RefreshTokensAsync_ReturnsNull_WhenTokenEndpointReturnsFailureStatus()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var provider = CreateProvider(handler);

        var result = await provider.RefreshTokensAsync(
            new Dictionary<string, string> { ["refresh_token"] = "old-refresh-token" }, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokensAsync_PostsToTheDiscoveredTokenEndpoint_AndMergesTokens()
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
        Assert.Equal("https://idp.example.com/token", capturedRequest.RequestUri!.ToString());
        Assert.Contains("client_id=test-client", capturedBody);
        Assert.Contains("grant_type=refresh_token", capturedBody);
        Assert.Contains("refresh_token=old-refresh-token", capturedBody);
        Assert.Contains("scope=openid+offline_access", capturedBody);

        Assert.NotNull(result);
        Assert.Equal("new-access-token", result!["access_token"]);
        Assert.Equal("old-id-token", result["id_token"]);
        Assert.Equal("old-refresh-token", result["refresh_token"]);
        Assert.True(result.ContainsKey("expires_at"));
    }

    private static CustomOpenIdConnectProvider CreateProvider(
        HttpMessageHandler? handler = null, string name = "test-oidc", string nameClaimType = "name", string[]? scopes = null)
    {
        var options = new CustomOpenIdConnectProviderOptions
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Authority = "https://idp.example.com",
            Scopes = scopes ?? ["openid", "profile", "email"],
            NameClaimType = nameClaimType,
        };
        var configuration = new OpenIdConnectConfiguration { TokenEndpoint = "https://idp.example.com/token" };
        var configurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        var factory = new FakeHttpClientFactory(handler ?? new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)));
        return new CustomOpenIdConnectProvider(name, options, configurationManager, factory);
    }

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value))));
}
