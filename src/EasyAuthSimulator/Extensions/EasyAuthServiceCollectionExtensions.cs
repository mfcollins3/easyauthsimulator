using EasyAuthSimulator.ClientPrincipal;
using EasyAuthSimulator.Headers;
using EasyAuthSimulator.Options;
using EasyAuthSimulator.Providers;
using EasyAuthSimulator.Session;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace EasyAuthSimulator.Extensions;

public static class EasyAuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core simulator: options, the session-backed cookie scheme, the provider
    /// registry, and the YARP proxy with the header-injection/stripping transform. Call
    /// <c>.AddEntraId()</c> (or another provider's extension) afterwards to enable sign-in.
    /// </summary>
    public static EasyAuthBuilder AddEasyAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EasyAuthOptions>()
            .Bind(configuration.GetSection(EasyAuthOptions.SectionName))
            .PostConfigure(o => o.ApiPrefix = o.ApiPrefix.TrimEnd('/') is { Length: > 0 } trimmed ? trimmed : EasyAuthOptions.DefaultApiPrefixValue)
            .Validate(o => !string.IsNullOrWhiteSpace(o.UpstreamUrl),
                "EASYAUTH_UPSTREAM_URL is required — set it to the URL of the app to forward authenticated requests to.")
            .ValidateOnStart();

        services.AddSingleton<EasyAuthProviderRegistry>();
        services.AddSingleton<InMemoryTicketStore>();

        services.AddAuthentication(o =>
            {
                o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                o.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(o =>
            {
                o.Cookie.Name = "AppServiceAuthSession";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

        // Inject the DI-registered ticket store into the named cookie options without building
        // a second service provider — CookieAuthenticationOptions is constructed by the
        // options system, so it can't take the store as a constructor dependency directly.
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<InMemoryTicketStore>((cookieOptions, store) => cookieOptions.SessionStore = store);

        services.AddReverseProxy()
            // Loaded with an empty placeholder here and populated for real by UseEasyAuth()
            // once the app is built — the final UpstreamUrl (env vars, appsettings, and in
            // tests, WebApplicationFactory's own config overrides) isn't guaranteed to be
            // visible on IConfiguration yet at this DI-registration point.
            .LoadFromMemory([], [])
            .AddTransforms(transformBuilderContext =>
            {
                var easyAuthOptions = transformBuilderContext.Services.GetRequiredService<IOptions<EasyAuthOptions>>().Value;
                if (easyAuthOptions.ForwardOriginalHost)
                {
                    transformBuilderContext.AddOriginalHost(true);
                }

                transformBuilderContext.AddRequestTransform(async requestTransformContext =>
                {
                    // Unconditional and first: an unauthenticated request must never be able
                    // to smuggle a principal or token through, regardless of what happens below.
                    SpoofableHeaderStripper.Strip(requestTransformContext.ProxyRequest.Headers);

                    var httpContext = requestTransformContext.HttpContext;
                    if (httpContext.User.Identity?.IsAuthenticated != true)
                    {
                        return;
                    }

                    var idpName = httpContext.User.FindFirst(EasyAuthClaimTypes.IdentityProvider)?.Value;
                    var registry = httpContext.RequestServices.GetRequiredService<EasyAuthProviderRegistry>();
                    if (idpName is null || !registry.TryGet(idpName, out var provider))
                    {
                        return;
                    }

                    var headers = requestTransformContext.ProxyRequest.Headers;
                    headers.TryAddWithoutValidation(EasyAuthHeaderNames.ClientPrincipal, ClientPrincipalEncoder.Encode(provider, httpContext.User));
                    headers.TryAddWithoutValidation(EasyAuthHeaderNames.ClientPrincipalId, provider.ResolvePrincipalId(httpContext.User) ?? "");
                    headers.TryAddWithoutValidation(EasyAuthHeaderNames.ClientPrincipalName, provider.ResolvePrincipalName(httpContext.User) ?? "");
                    headers.TryAddWithoutValidation(EasyAuthHeaderNames.ClientPrincipalIdp, provider.Name);

                    void AddTokenHeaderIfPresent(string suffix, string? value)
                    {
                        if (!string.IsNullOrEmpty(value))
                        {
                            headers.TryAddWithoutValidation(EasyAuthHeaderNames.TokenHeader(provider.TokenHeaderInfix, suffix), value);
                        }
                    }

                    AddTokenHeaderIfPresent("ID-TOKEN", await httpContext.GetTokenAsync("id_token"));
                    AddTokenHeaderIfPresent("ACCESS-TOKEN", await httpContext.GetTokenAsync("access_token"));
                    AddTokenHeaderIfPresent("REFRESH-TOKEN", await httpContext.GetTokenAsync("refresh_token"));
                    AddTokenHeaderIfPresent("EXPIRES-ON", await httpContext.GetTokenAsync("expires_at"));
                });
            });

        return new EasyAuthBuilder(services, configuration);
    }

    internal static IReadOnlyList<RouteConfig> BuildRoutes() =>
    [
        new RouteConfig
        {
            RouteId = "app",
            ClusterId = "app",
            // Gating is owned entirely by GlobalValidationMiddleware; the route itself stays
            // open so that middleware can implement all four unauthenticatedClientAction
            // behaviors instead of the binary allow/challenge an authorization policy gives.
            AuthorizationPolicy = "anonymous",
            Match = new RouteMatch { Path = "/{**catch-all}" },
        }
    ];

    internal static IReadOnlyList<ClusterConfig> BuildClusters(string upstreamUrl) =>
    [
        new ClusterConfig
        {
            ClusterId = "app",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["upstream"] = new DestinationConfig { Address = upstreamUrl },
            },
        }
    ];
}
