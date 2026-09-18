using System.Security.Claims;

namespace EasyAuthSimulator.Providers;

/// <summary>
/// The seam a future identity provider (Google, GitHub, a generic OIDC provider, ...)
/// implements to plug into the simulator. Registering the authentication handler itself
/// (cookie scheme, OIDC/OAuth handler, callback paths) happens once at startup in a
/// provider-specific <c>AddXxx()</c> extension method; this interface covers the parts that
/// run per request or per <c>/.auth/*</c> call.
/// </summary>
public interface IEasyAuthProvider
{
    /// <summary>The provider slug: the "aad" in <c>/.auth/login/aad</c> and <c>X-MS-CLIENT-PRINCIPAL-IDP</c>.</summary>
    string Name { get; }

    /// <summary>The infix in <c>X-MS-TOKEN-{infix}-ID-TOKEN</c> etc. — "AAD" for Entra ID.</summary>
    string TokenHeaderInfix { get; }

    /// <summary>The ASP.NET Core authentication scheme name to challenge/sign out for this provider.</summary>
    string AuthenticationScheme { get; }

    /// <summary>The short claim type emitted as <c>name_typ</c> in the client-principal envelope.</summary>
    string NameClaimType { get; }

    /// <summary>The short claim type emitted as <c>role_typ</c> in the client-principal envelope.</summary>
    string RoleClaimType { get; }

    /// <summary>The value for <c>X-MS-CLIENT-PRINCIPAL-ID</c> and the client-principal's <c>user_id</c>.</summary>
    string? ResolvePrincipalId(ClaimsPrincipal principal);

    /// <summary>The value for <c>X-MS-CLIENT-PRINCIPAL-NAME</c>.</summary>
    string? ResolvePrincipalName(ClaimsPrincipal principal);

    /// <summary>Projects the principal's claims into the <c>{typ,val}</c> pairs the client-principal envelope expects.</summary>
    IEnumerable<Claim> ProjectClaims(ClaimsPrincipal principal);

    /// <summary>
    /// Refreshes tokens for <c>/.auth/refresh</c> using the current session's stored tokens
    /// (keyed "access_token"/"id_token"/"refresh_token"/"expires_at"). Returns the tokens to
    /// merge back into the session, or null if the refresh failed.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>?> RefreshTokensAsync(
        IReadOnlyDictionary<string, string> currentTokens, CancellationToken cancellationToken);
}
