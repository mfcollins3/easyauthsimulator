using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Marks that an identity provider was registered on this resource by <c>WithEntraId()</c> or
/// <c>WithCustomOpenIdConnect()</c> — tracked so <see cref="EasyAuthSimulatorResourceBuilderExtensions.AddEasyAuthSimulator"/>
/// can infer <see cref="EasyAuthSimulatorOptions.DefaultProvider"/> when exactly one provider is
/// configured. Annotation additions are synchronous, so by the time the deferred environment
/// callback that reads these runs (after the whole AppHost program has finished building the
/// resource), every provider registered anywhere in the fluent chain is already present here —
/// regardless of whether WithEntraId()/WithCustomOpenIdConnect() were called before or after
/// AddEasyAuthSimulator() returned.
/// </summary>
file sealed record EasyAuthProviderAnnotation(string ProviderName) : IResourceAnnotation;

public static class EasyAuthSimulatorResourceBuilderExtensions
{
    /// <summary>Default image for the simulator; override with WithImage()/WithImageTag()/WithImageRegistry().</summary>
    private const string ContainerImage = "easyauthsimulator";

    private const string ContainerImageTag = "latest";

    /// <summary>
    /// Adds the EasyAuth simulator as a container resource. The AppHost needs a reference to this
    /// assembly to call this method, but not a build of EasyAuthSimulator itself — the app ships
    /// as the "easyauthsimulator" container image (build it from the Dockerfile at the repo root,
    /// or point at a published one via WithImageRegistry()/WithImageTag()).
    /// </summary>
    [AspireExport]
    public static IResourceBuilder<ContainerResource> AddEasyAuthSimulator(
        this IDistributedApplicationBuilder builder, [ResourceName] string name, EasyAuthSimulatorOptions? options = null)
    {
        options ??= new EasyAuthSimulatorOptions();

        // isProxied: false — a container resource can't have Aspire's own front-end proxy sit in
        // front when port and targetPort are the same value, and there should be no
        // port-translation hop in front of the exact port Entra's redirect URI is registered
        // against anyway.
        var resourceBuilder = builder.AddContainer(name, ContainerImage, ContainerImageTag)
            .WithHttpEndpoint(port: options.Port, targetPort: options.Port, name: "http", env: "EASYAUTH_PORT", isProxied: false);

        if (!options.AuthenticationRequired)
        {
            return resourceBuilder.WithEnvironment("EASYAUTH_UNAUTHENTICATED_ACTION", "AllowAnonymous");
        }

        if (options.DefaultProvider is { Length: > 0 } defaultProvider)
        {
            return resourceBuilder.WithEnvironment("EASYAUTH_PROVIDER", defaultProvider);
        }

        // WithEntraId()/WithCustomOpenIdConnect() are further calls chained onto this same
        // builder, so which providers end up configured isn't known yet here — resolved lazily
        // via a deferred environment callback, which Aspire only runs once the whole AppHost
        // program (and therefore every provider registration) has finished.
        return resourceBuilder.WithEnvironment(context =>
        {
            var providers = context.Resource.Annotations
                .OfType<EasyAuthProviderAnnotation>()
                .Select(a => a.ProviderName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (providers.Length != 1)
            {
                throw new InvalidOperationException(providers.Length == 0
                    ? $"'{name}' has AuthenticationRequired = true but no identity providers are configured — call WithEntraId() and/or WithCustomOpenIdConnect(), or set EasyAuthSimulatorOptions.DefaultProvider explicitly."
                    : $"'{name}' has AuthenticationRequired = true with multiple identity providers configured ({string.Join(", ", providers)}) — set EasyAuthSimulatorOptions.DefaultProvider to choose which one unauthenticated requests are redirected to.");
            }

            context.EnvironmentVariables["EASYAUTH_PROVIDER"] = providers[0];
        });
    }

    /// <summary>
    /// Points the simulator at the app it should forward authenticated requests to. Accepts any
    /// resource with an HTTP endpoint — the upstream doesn't have to be a .NET project; a Go
    /// executable, a Node app, or a container all satisfy this.
    /// </summary>
    [AspireExport]
    public static IResourceBuilder<ContainerResource> WithUpstream(
        this IResourceBuilder<ContainerResource> builder, IResourceBuilder<IResourceWithEndpoints> upstream)
    {
        return builder
            .WaitFor(upstream)
            .WithEnvironment("EASYAUTH_UPSTREAM_URL", upstream.GetEndpoint("http"));
    }

    /// <summary>Wires up the Microsoft Entra ID app registration the simulator should sign in against.</summary>
    [AspireExport]
    public static IResourceBuilder<ContainerResource> WithEntraId(
        this IResourceBuilder<ContainerResource> builder,
        IResourceBuilder<ParameterResource> tenantId,
        IResourceBuilder<ParameterResource> clientId,
        IResourceBuilder<ParameterResource> clientSecret)
    {
        return builder
            .WithAnnotation(new EasyAuthProviderAnnotation("aad"))
            .WithEnvironment("EASYAUTH_AAD_TENANT_ID", tenantId)
            .WithEnvironment("EASYAUTH_AAD_CLIENT_ID", clientId)
            .WithEnvironment("EASYAUTH_AAD_CLIENT_SECRET", clientSecret);
    }

    /// <summary>
    /// Wires up a custom OpenID Connect provider under the given unique name — any spec-compliant
    /// IDP (Google, GitHub, Auth0, ...), discovered via its "{authority}/.well-known/openid-configuration"
    /// metadata document rather than a provider-specific SDK
    /// (learn.microsoft.com/azure/container-apps/authentication-openid). Call once per provider
    /// with a distinct <paramref name="providerName"/> to enable more than one.
    /// </summary>
    [AspireExport]
    public static IResourceBuilder<ContainerResource> WithCustomOpenIdConnect(
        this IResourceBuilder<ContainerResource> builder,
        string providerName,
        string authority,
        IResourceBuilder<ParameterResource> clientId,
        IResourceBuilder<ParameterResource> clientSecret)
    {
        var upperName = providerName.ToUpperInvariant();
        return builder
            .WithAnnotation(new EasyAuthProviderAnnotation(providerName))
            .WithEnvironment($"EASYAUTH_OIDC_{upperName}_AUTHORITY", authority)
            .WithEnvironment($"EASYAUTH_OIDC_{upperName}_CLIENT_ID", clientId)
            .WithEnvironment($"EASYAUTH_OIDC_{upperName}_CLIENT_SECRET", clientSecret);
    }
}
