using System.Security.Claims;
using EasyAuthSimulator.Options;
using EasyAuthSimulator.Providers;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace EasyAuthSimulator.Extensions;

public static class CustomOpenIdConnectServiceCollectionExtensions
{
    private const string SectionName = $"{EasyAuthOptions.SectionName}:CustomOpenIdConnect";

    /// <summary>
    /// Registers one <see cref="IEasyAuthProvider"/> plus OIDC authentication scheme per named
    /// entry under <c>EasyAuth:CustomOpenIdConnect:{name}</c>, mirroring Container Apps' "one or
    /// more" custom OpenID Connect providers, each identified by a unique alphanumeric name
    /// chosen at registration time rather than a fixed slug like "aad"
    /// (learn.microsoft.com/azure/container-apps/authentication-openid). Providers are
    /// discovered directly from configuration — rather than bound via a single IOptions&lt;T&gt;
    /// like AddEntraId() — because the set of names isn't known at compile time. A no-op when no
    /// custom providers are configured.
    /// </summary>
    public static EasyAuthBuilder AddCustomOpenIdConnect(this EasyAuthBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        var providerNames = configuration.GetSection(SectionName).GetChildren().Select(c => c.Key).ToArray();

        foreach (var name in providerNames)
        {
            if (string.Equals(name, EntraIdProvider.ProviderName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Custom OpenID Connect provider name '{name}' collides with the built-in '{EntraIdProvider.ProviderName}' provider — choose a different name.");
            }

            RegisterProvider(services, configuration, name);
        }

        return builder;
    }

    private static void RegisterProvider(IServiceCollection services, IConfiguration configuration, string name)
    {
        var upperName = name.ToUpperInvariant();

        services.AddOptions<CustomOpenIdConnectProviderOptions>(name)
            .Bind(configuration.GetSection($"{SectionName}:{name}"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId),
                $"EASYAUTH_OIDC_{upperName}_CLIENT_ID is required to enable the '{name}' custom OpenID Connect provider.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Authority) || !string.IsNullOrWhiteSpace(o.MetadataAddress),
                $"EASYAUTH_OIDC_{upperName}_AUTHORITY (or an explicit metadata address) is required to enable the '{name}' custom OpenID Connect provider.")
            .ValidateOnStart();

        services.AddHttpClient(CustomOpenIdConnectProvider.HttpClientName(name));

        services.AddSingleton<IEasyAuthProvider>(sp =>
        {
            var oidcOptions = sp.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(name);
            return new CustomOpenIdConnectProvider(
                name,
                sp.GetRequiredService<IOptionsMonitor<CustomOpenIdConnectProviderOptions>>().Get(name),
                oidcOptions.ConfigurationManager!,
                sp.GetRequiredService<IHttpClientFactory>());
        });

        services.AddAuthentication().AddOpenIdConnect(name, _ => { });

        // Configured via DI-resolved IOptions<T> rather than by reading IConfiguration directly
        // here, for the same reason as AddEntraId(): this delegate only runs the first time this
        // scheme is actually used, by which point every configuration source is visible.
        services.AddOptions<OpenIdConnectOptions>(name)
            .Configure<IOptionsMonitor<CustomOpenIdConnectProviderOptions>, IOptions<EasyAuthOptions>>((o, customOptionsMonitor, easyAuthOptionsAccessor) =>
            {
                var custom = customOptionsMonitor.Get(name);
                var apiPrefix = easyAuthOptionsAccessor.Value.ApiPrefix;

                if (!string.IsNullOrWhiteSpace(custom.MetadataAddress))
                {
                    o.MetadataAddress = custom.MetadataAddress;
                }
                else
                {
                    o.Authority = custom.Authority;
                }

                o.ClientId = custom.ClientId;
                o.ClientSecret = custom.ClientSecret;

                // Same reasoning as AddEntraId(): a confidential client exchanging the
                // authorization code over the back channel, so no hybrid response type is needed.
                o.ResponseType = OpenIdConnectResponseType.Code;
                o.UsePkce = true;
                o.SaveTokens = true;
                o.GetClaimsFromUserInfoEndpoint = false;

                o.MapInboundClaims = false;
                o.TokenValidationParameters.NameClaimType = custom.NameClaimType;
                o.TokenValidationParameters.RoleClaimType = custom.RoleClaimType;

                o.CallbackPath = $"{apiPrefix}/login/{name}/callback";
                o.SignedOutCallbackPath = $"{apiPrefix}/logout/callback";

                foreach (var scope in custom.Scopes)
                {
                    if (!o.Scope.Contains(scope))
                    {
                        o.Scope.Add(scope);
                    }
                }

                o.Events.OnTicketReceived = context =>
                {
                    if (context.Principal?.Identity is ClaimsIdentity identity)
                    {
                        identity.AddClaim(new Claim(EasyAuthClaimTypes.IdentityProvider, name));
                    }

                    return Task.CompletedTask;
                };
            });
    }
}
