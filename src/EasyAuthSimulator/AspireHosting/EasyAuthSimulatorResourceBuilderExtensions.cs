using System.Reflection;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

public static class EasyAuthSimulatorResourceBuilderExtensions
{
    /// <summary>
    /// Adds the EasyAuth simulator, launched directly from its own compiled DLL rather than
    /// via Aspire's AddProject&lt;T&gt;() — the AppHost needs a reference to this assembly to
    /// call this method at all, but not a second, Aspire-recognized project reference just to
    /// resolve where the simulator's binary lives.
    ///
    /// The port is pinned rather than Aspire-allocated, because it must match the redirect URI
    /// registered with the identity provider — a random port would break sign-in on every run.
    /// </summary>
    [AspireExport]
    public static IResourceBuilder<ExecutableResource> AddEasyAuthSimulator(
        this IDistributedApplicationBuilder builder, [ResourceName] string name, int port = 8080)
    {
        var dllPath = ResolveSimulatorDllPath();
        var workingDirectory = Path.GetDirectoryName(dllPath)!;

        // isProxied: false — an executable resource can't have Aspire's own front-end proxy
        // sit in front when port and targetPort are the same value, and there should be no
        // port-translation hop in front of the exact port Entra's redirect URI is registered
        // against anyway.
        return builder.AddExecutable(name, "dotnet", workingDirectory, [dllPath])
            .WithHttpEndpoint(port: port, targetPort: port, name: "http", env: "EASYAUTH_PORT", isProxied: false);
    }

    private static string ResolveSimulatorDllPath()
    {
        // Escape hatch for anything the mechanisms below can't resolve — the simulator shipped
        // as a standalone build outside this solution entirely, for instance.
        var overridePath = Environment.GetEnvironmentVariable("EASYAUTH_SIMULATOR_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        // Baked in at build time (see the AssemblyMetadata item in EasyAuthSimulator.csproj)
        // rather than trusting Assembly.Location: a polyglot AppHost's bridge (e.g. TypeScript)
        // loads this assembly from an isolated staging copy that carries only the DLL, not the
        // .runtimeconfig.json/.deps.json a "dotnet <dll>" launch needs, so Assembly.Location
        // would point at that unusable staged copy instead of the real build output.
        var buildOutputPath = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "EasyAuthSimulator.BuildOutputPath")?.Value;
        if (!string.IsNullOrEmpty(buildOutputPath) && File.Exists(buildOutputPath))
        {
            return buildOutputPath;
        }

        // Fallback for anything that doesn't carry the embedded metadata (e.g. an assembly
        // built by an older version of this project).
        var location = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(location))
        {
            throw new InvalidOperationException(
                "Could not locate a launchable copy of EasyAuthSimulator.dll. Set "
                + "EASYAUTH_SIMULATOR_PATH to its absolute path (the build output directory "
                + "containing EasyAuthSimulator.runtimeconfig.json).");
        }

        return location;
    }

    /// <summary>
    /// Points the simulator at the app it should forward authenticated requests to. Accepts any
    /// resource with an HTTP endpoint — the upstream doesn't have to be a .NET project; a Go
    /// executable, a Node app, or a container all satisfy this.
    /// </summary>
    [AspireExport]
    public static IResourceBuilder<ExecutableResource> WithUpstream(
        this IResourceBuilder<ExecutableResource> builder, IResourceBuilder<IResourceWithEndpoints> upstream)
    {
        return builder
            .WaitFor(upstream)
            .WithEnvironment("EASYAUTH_UPSTREAM_URL", upstream.GetEndpoint("http"));
    }

    /// <summary>Wires up the Microsoft Entra ID app registration the simulator should sign in against.</summary>
    [AspireExport]
    public static IResourceBuilder<ExecutableResource> WithEntraId(
        this IResourceBuilder<ExecutableResource> builder,
        IResourceBuilder<ParameterResource> tenantId,
        IResourceBuilder<ParameterResource> clientId,
        IResourceBuilder<ParameterResource> clientSecret)
    {
        return builder
            .WithEnvironment("EASYAUTH_AAD_TENANT_ID", tenantId)
            .WithEnvironment("EASYAUTH_AAD_CLIENT_ID", clientId)
            .WithEnvironment("EASYAUTH_AAD_CLIENT_SECRET", clientSecret);
    }
}
