using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using EasyAuthSimulator.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace EasyAuthSimulator.Providers;

/// <summary>
/// A generic OpenID Connect provider registered under a user-chosen name, mirroring Container
/// Apps' custom OpenID Connect providers: any spec-compliant IDP, identified by well-known
/// metadata discovery rather than a provider-specific SDK
/// (learn.microsoft.com/azure/container-apps/authentication-openid). One instance is created
/// per configured provider name by <see cref="Extensions.CustomOpenIdConnectServiceCollectionExtensions"/>.
/// </summary>
public sealed class CustomOpenIdConnectProvider(
    string name,
    CustomOpenIdConnectProviderOptions options,
    IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
    IHttpClientFactory httpClientFactory)
    : IEasyAuthProvider
{
    public static string HttpClientName(string providerName) => $"EasyAuthSimulator.CustomOpenIdConnect.{providerName}";

    public string Name => name;
    public string TokenHeaderInfix => name.ToUpperInvariant();
    public string AuthenticationScheme => name;
    public string NameClaimType => options.NameClaimType;
    public string RoleClaimType => options.RoleClaimType;

    public string? ResolvePrincipalId(ClaimsPrincipal principal) => principal.FindFirst("sub")?.Value;

    public string? ResolvePrincipalName(ClaimsPrincipal principal) =>
        principal.FindFirst(options.NameClaimType)?.Value
        ?? principal.FindFirst(ClaimTypes.Email)?.Value
        ?? principal.FindFirst("sub")?.Value;

    public IEnumerable<Claim> ProjectClaims(ClaimsPrincipal principal) =>
        principal.Claims.Where(c => c.Type != EasyAuthClaimTypes.IdentityProvider);

    public async Task<IReadOnlyDictionary<string, string>?> RefreshTokensAsync(
        IReadOnlyDictionary<string, string> currentTokens, CancellationToken cancellationToken)
    {
        if (!currentTokens.TryGetValue("refresh_token", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        // Reuses the sign-in OIDC handler's own discovered metadata (and its caching/retry
        // behavior) instead of re-fetching the well-known document here.
        var configuration = await configurationManager.GetConfigurationAsync(cancellationToken);

        using var client = httpClientFactory.CreateClient(HttpClientName(name));
        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["scope"] = string.Join(' ', options.Scopes),
            }),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenRefreshResponse>(cancellationToken);
        if (payload is null)
        {
            return null;
        }

        return new Dictionary<string, string>
        {
            ["access_token"] = payload.AccessToken,
            ["id_token"] = payload.IdToken ?? currentTokens.GetValueOrDefault("id_token", ""),
            ["refresh_token"] = payload.RefreshToken ?? refreshToken,
            ["expires_at"] = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn).ToString("o"),
        };
    }

    private sealed record TokenRefreshResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
