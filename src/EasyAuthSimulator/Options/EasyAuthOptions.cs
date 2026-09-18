namespace EasyAuthSimulator.Options;

public sealed class EasyAuthOptions
{
    public const string SectionName = "EasyAuth";
    public const string DefaultApiPrefixValue = "/.auth";

    /// <summary>The app to forward authenticated requests to. Required.</summary>
    public string UpstreamUrl { get; set; } = "";

    /// <summary>The provider slug used when redirecting an unauthenticated request to sign in.</summary>
    public string DefaultProvider { get; set; } = "aad";

    /// <summary>Mirrors <c>globalValidation.requireAuthentication</c>.</summary>
    public bool RequireAuthentication { get; set; } = true;

    /// <summary>Mirrors <c>unauthenticatedClientAction</c> (both ARM and file-config spellings accepted).</summary>
    public UnauthenticatedClientAction UnauthenticatedAction { get; set; } = UnauthenticatedClientAction.RedirectToLoginPage;

    /// <summary>Mirrors <c>globalValidation.excludedPaths</c>. Entries may end in <c>/*</c> for a prefix match.</summary>
    public string[] ExcludedPaths { get; set; } = [];

    /// <summary>Mirrors <c>httpSettings.routes.apiPrefix</c>.</summary>
    public string ApiPrefix { get; set; } = DefaultApiPrefixValue;

    /// <summary>Mirrors <c>httpSettings.forwardProxy.convention</c>.</summary>
    public ForwardProxyConvention ForwardProxyConvention { get; set; } = ForwardProxyConvention.Standard;

    /// <summary>Header name to trust for the original scheme when <see cref="ForwardProxyConvention"/> is <see cref="Options.ForwardProxyConvention.Custom"/>.</summary>
    public string? CustomProtoHeaderName { get; set; }

    /// <summary>Header name to trust for the original host when <see cref="ForwardProxyConvention"/> is <see cref="Options.ForwardProxyConvention.Custom"/>.</summary>
    public string? CustomHostHeaderName { get; set; }

    /// <summary>
    /// When true (the default), the upstream app sees the original ingress Host header rather
    /// than its own address — matching the real sidecar, where the app's self-links should
    /// point back through the authenticating front door.
    /// </summary>
    public bool ForwardOriginalHost { get; set; } = true;

    /// <summary>
    /// Hosts (no scheme) that <c>post_login_redirect_uri</c>/<c>post_logout_redirect_uri</c> may
    /// point at even when they don't match the current request's host. Mirrors
    /// <c>allowedExternalRedirectUrls</c>.
    /// </summary>
    public string[] AllowedExternalRedirectHosts { get; set; } = [];
}

/// <summary>
/// Mirrors the real service's <c>unauthenticatedClientAction</c>. The ARM schema spells the
/// rejection actions "Return401"/"Return403" while the file-based schema spells them
/// "RejectWith401"/"RejectWith404" — both spellings parse to the same value here so either
/// config source works without translation.
/// </summary>
public enum UnauthenticatedClientAction
{
    RedirectToLoginPage = 0,
    AllowAnonymous = 1,
    Return401 = 2,
    RejectWith401 = 2,
    Return403 = 3,
    Return404 = 4,
    RejectWith404 = 4,
}

public enum ForwardProxyConvention
{
    NoProxy,
    Standard,
    Custom,
}
