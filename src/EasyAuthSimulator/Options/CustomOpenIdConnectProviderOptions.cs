namespace EasyAuthSimulator.Options;

/// <summary>
/// Config for one custom OpenID Connect provider, bound from
/// <c>EasyAuth:CustomOpenIdConnect:{name}</c> — mirroring Container Apps' "one or more" custom
/// OIDC providers, each identified by a unique alphanumeric name chosen at registration time
/// rather than a fixed slug like "aad" (learn.microsoft.com/azure/container-apps/authentication-openid).
/// </summary>
public sealed class CustomOpenIdConnectProviderOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>The provider's issuer URL; ASP.NET Core discovers metadata at "{Authority}/.well-known/openid-configuration" unless <see cref="MetadataAddress"/> is set.</summary>
    public string Authority { get; set; } = "";

    /// <summary>Overrides the metadata document URL for a provider that doesn't follow the issuer + "/.well-known/openid-configuration" convention.</summary>
    public string? MetadataAddress { get; set; }

    public string[] Scopes { get; set; } = ["openid", "profile", "email"];

    /// <summary>The short claim type this provider's ID token uses for the display name.</summary>
    public string NameClaimType { get; set; } = "name";

    /// <summary>The short claim type this provider's ID token uses for roles.</summary>
    public string RoleClaimType { get; set; } = "roles";
}
