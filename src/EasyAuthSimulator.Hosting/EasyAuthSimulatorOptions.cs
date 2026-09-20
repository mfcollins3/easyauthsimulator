namespace Aspire.Hosting;

/// <summary>Configuration for <see cref="EasyAuthSimulatorResourceBuilderExtensions.AddEasyAuthSimulator"/>.</summary>
[AspireDto]
public sealed class EasyAuthSimulatorOptions
{
    /// <summary>
    /// The port to listen on. Pinned rather than Aspire-allocated, because it must match the
    /// redirect URI registered with the identity provider — a random port would break sign-in
    /// on every run.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Mirrors <c>globalValidation.requireAuthentication</c>: when true, unauthenticated
    /// requests are redirected to sign in against <see cref="DefaultProvider"/> instead of
    /// reaching the app. Defaults to false (sets <c>EASYAUTH_UNAUTHENTICATED_ACTION=AllowAnonymous</c>),
    /// so the app itself decides what an anonymous visitor sees — e.g. a sign-in picker across
    /// multiple providers.
    /// </summary>
    public bool AuthenticationRequired { get; set; }

    /// <summary>
    /// The provider slug (mirrors <c>EASYAUTH_PROVIDER</c>) unauthenticated requests are
    /// redirected to when <see cref="AuthenticationRequired"/> is true. If left unset, it's
    /// inferred from whichever single provider was registered via
    /// <c>WithEntraId()</c>/<c>WithCustomOpenIdConnect()</c> on this resource — leaving it unset
    /// with more than one provider configured fails fast when the app model is built, since
    /// there'd be no way to know which one to redirect to.
    /// </summary>
    public string? DefaultProvider { get; set; }
}
