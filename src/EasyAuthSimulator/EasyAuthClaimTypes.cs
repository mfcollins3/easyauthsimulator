namespace EasyAuthSimulator;

/// <summary>
/// Claim types the simulator adds to the signed-in principal itself, distinct from claims
/// sourced from the identity provider's token.
/// </summary>
public static class EasyAuthClaimTypes
{
    /// <summary>
    /// Records which <see cref="Providers.IEasyAuthProvider.Name"/> authenticated the current
    /// session, so /.auth/me, /.auth/refresh and /.auth/logout know which provider to consult
    /// without guessing from token contents.
    /// </summary>
    public const string IdentityProvider = "easyauth:idp";
}
