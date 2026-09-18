using System.Text.Json.Serialization;

namespace EasyAuthSimulator.ClientPrincipal;

/// <summary>
/// The decoded shape of X-MS-CLIENT-PRINCIPAL, verbatim from Microsoft Learn. Property order
/// matters for fidelity — "claims" is genuinely second, not first.
/// </summary>
public sealed record ClientPrincipalEnvelope(
    [property: JsonPropertyName("auth_typ")] string AuthenticationType,
    [property: JsonPropertyName("claims")] IReadOnlyList<ClientPrincipalClaim> Claims,
    [property: JsonPropertyName("name_typ")] string NameClaimType,
    [property: JsonPropertyName("role_typ")] string RoleClaimType);

public sealed record ClientPrincipalClaim(
    [property: JsonPropertyName("typ")] string Type,
    [property: JsonPropertyName("val")] string Value);
