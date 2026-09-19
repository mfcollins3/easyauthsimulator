using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

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
    ///
    /// The port is pinned rather than Aspire-allocated, because it must match the redirect URI
    /// registered with the identity provider — a random port would break sign-in on every run.
    /// </summary>
    [AspireExport]
    public static IResourceBuilder<ContainerResource> AddEasyAuthSimulator(
        this IDistributedApplicationBuilder builder, [ResourceName] string name, int port = 8080)
    {
        // isProxied: false — a container resource can't have Aspire's own front-end proxy sit in
        // front when port and targetPort are the same value, and there should be no
        // port-translation hop in front of the exact port Entra's redirect URI is registered
        // against anyway.
        return builder.AddContainer(name, ContainerImage, ContainerImageTag)
            .WithHttpEndpoint(port: port, targetPort: port, name: "http", env: "EASYAUTH_PORT", isProxied: false);
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
            .WithEnvironment($"EASYAUTH_OIDC_{upperName}_AUTHORITY", authority)
            .WithEnvironment($"EASYAUTH_OIDC_{upperName}_CLIENT_ID", clientId)
            .WithEnvironment($"EASYAUTH_OIDC_{upperName}_CLIENT_SECRET", clientSecret);
    }
}
