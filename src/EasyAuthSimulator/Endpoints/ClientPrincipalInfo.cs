using System.Text.Json.Serialization;
using EasyAuthSimulator.ClientPrincipal;

namespace EasyAuthSimulator.Endpoints;

/// <summary>
/// The /.auth/me response shape. Learn no longer publishes this schema; this follows the
/// shape documented by the feature's PM and corroborating samples — treat as best-effort,
/// not byte-verified against a live deployment.
/// </summary>
public sealed record ClientPrincipalInfo(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("access_token_secret")] string? AccessTokenSecret,
    [property: JsonPropertyName("authentication_token")] string? AuthenticationToken,
    [property: JsonPropertyName("expires_on")] string? ExpiresOn,
    [property: JsonPropertyName("id_token")] string? IdToken,
    [property: JsonPropertyName("provider_name")] string ProviderName,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("user_claims")] IReadOnlyList<ClientPrincipalClaim> UserClaims,
    [property: JsonPropertyName("user_id")] string? UserId);
