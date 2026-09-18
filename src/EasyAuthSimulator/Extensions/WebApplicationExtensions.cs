using EasyAuthSimulator.Endpoints;
using EasyAuthSimulator.Middleware;
using EasyAuthSimulator.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace EasyAuthSimulator.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>Wires up the full simulator pipeline: forwarded headers, auth, the global gate, /.auth/*, then the proxy.</summary>
    public static WebApplication UseEasyAuth(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        var options = app.Services.GetRequiredService<IOptions<EasyAuthOptions>>().Value;

        // Populated here, after the host is fully built, rather than at DI-registration time
        // in AddEasyAuth() — by now IConfiguration reflects every source (env vars,
        // appsettings, and in tests, WebApplicationFactory's own overrides), which isn't
        // guaranteed earlier.
        app.Services.GetRequiredService<InMemoryConfigProvider>().Update(
            EasyAuthServiceCollectionExtensions.BuildRoutes(),
            EasyAuthServiceCollectionExtensions.BuildClusters(options.UpstreamUrl));

        switch (options.ForwardProxyConvention)
        {
            case ForwardProxyConvention.Standard:
                var forwardedHeaderOptions = new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
                };
                // Local dev only: Aspire's own dashboard/proxy sits in front of this process,
                // so the immediate hop is trusted unconditionally rather than restricted to a
                // known proxy list, which would be wrong in a production deployment.
                forwardedHeaderOptions.KnownIPNetworks.Clear();
                forwardedHeaderOptions.KnownProxies.Clear();
                app.UseForwardedHeaders(forwardedHeaderOptions);
                break;

            case ForwardProxyConvention.Custom:
                app.Use(async (context, next) =>
                {
                    if (options.CustomProtoHeaderName is { Length: > 0 } protoHeader
                        && context.Request.Headers.TryGetValue(protoHeader, out var proto))
                    {
                        context.Request.Scheme = proto.ToString();
                    }

                    if (options.CustomHostHeaderName is { Length: > 0 } hostHeader
                        && context.Request.Headers.TryGetValue(hostHeader, out var host))
                    {
                        context.Request.Host = HostString.FromUriComponent(host.ToString());
                    }

                    await next(context);
                });
                break;

            case ForwardProxyConvention.NoProxy:
            default:
                break;
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<GlobalValidationMiddleware>();
        app.UseAuthorization();

        app.MapEasyAuthEndpoints();
        app.MapReverseProxy();

        return app;
    }
}
