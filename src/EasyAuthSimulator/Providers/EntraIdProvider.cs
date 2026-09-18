using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using EasyAuthSimulator.Options;
using Microsoft.Extensions.Options;

namespace EasyAuthSimulator.Providers;

public sealed class EntraIdProvider(IOptions<AadProviderOptions> aadOptions, IHttpClientFactory httpClientFactory)
    : IEasyAuthProvider
{
    public const string ProviderName = "aad";
    public const string SchemeName = "aad";
    public const string HttpClientName = "EasyAuthSimulator.EntraId";

    public string Name => ProviderName;
    public string TokenHeaderInfix => "AAD";
    public string AuthenticationScheme => SchemeName;
    public string NameClaimType => "name";
    public string RoleClaimType => "roles";

    public string? ResolvePrincipalId(ClaimsPrincipal principal) =>
        // Not confirmed against a real Container Apps deployment which claim backs
        // X-MS-CLIENT-PRINCIPAL-ID for "aad" specifically (Learn only documents this for the
        // B2C provider, as the ID token's "sub"). Falling back to "oid" is a reasonable guess
        // for v2.0 tokens but should be verified.
        principal.FindFirst("sub")?.Value ?? principal.FindFirst("oid")?.Value;

    public string? ResolvePrincipalName(ClaimsPrincipal principal) =>
        principal.FindFirst("preferred_username")?.Value
        ?? principal.FindFirst(ClaimTypes.Email)?.Value
        ?? principal.FindFirst("name")?.Value;

    public IEnumerable<Claim> ProjectClaims(ClaimsPrincipal principal) =>
        principal.Claims.Where(c => c.Type != EasyAuthClaimTypes.IdentityProvider);

    public async Task<IReadOnlyDictionary<string, string>?> RefreshTokensAsync(
        IReadOnlyDictionary<string, string> currentTokens, CancellationToken cancellationToken)
    {
        if (!currentTokens.TryGetValue("refresh_token", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        var options = aadOptions.Value;
        var tokenEndpoint = $"https://{options.AuthorityHost}/{options.TenantId}/oauth2/v2.0/token";

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
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
