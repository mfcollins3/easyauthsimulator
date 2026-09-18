using System.Security.Claims;
using EasyAuthSimulator.Options;
using EasyAuthSimulator.Providers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace EasyAuthSimulator.Extensions;

public static class EntraIdServiceCollectionExtensions
{
    public static EasyAuthBuilder AddEntraId(this EasyAuthBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        services.AddOptions<AadProviderOptions>()
            .Bind(configuration.GetSection(AadProviderOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.TenantId) && !string.IsNullOrWhiteSpace(o.ClientId),
                "EASYAUTH_AAD_TENANT_ID and EASYAUTH_AAD_CLIENT_ID are required to enable Entra ID sign-in.")
            .ValidateOnStart();

        services.AddHttpClient(EntraIdProvider.HttpClientName);
        services.AddSingleton<IEasyAuthProvider, EntraIdProvider>();

        services.AddAuthentication().AddOpenIdConnect(EntraIdProvider.SchemeName, _ => { });

        // Configured via DI-resolved IOptions<T> rather than by reading IConfiguration
        // directly here: this delegate only runs the first time the "aad" scheme is actually
        // used (per request), by which point the host is fully built and every configuration
        // source (env vars, appsettings, and in tests, WebApplicationFactory's own overrides)
        // is visible. Capturing values read from IConfiguration at this registration call
        // site instead would freeze them at whatever IConfiguration held before Build().
        services.AddOptions<OpenIdConnectOptions>(EntraIdProvider.SchemeName)
            .Configure<IOptions<AadProviderOptions>, IOptions<EasyAuthOptions>>((o, aadOptionsAccessor, easyAuthOptionsAccessor) =>
            {
                var aad = aadOptionsAccessor.Value;
                var apiPrefix = easyAuthOptionsAccessor.Value.ApiPrefix;

                o.Authority = $"https://{aad.AuthorityHost}/{aad.TenantId}/v2.0";
                o.ClientId = aad.ClientId;
                o.ClientSecret = aad.ClientSecret;

                // Pure authorization-code flow with a confidential client: tokens are
                // exchanged over the back channel rather than returned in a front-channel
                // fragment, so there is no need for the older "code id_token" hybrid response
                // type just to get an id_token up front.
                o.ResponseType = OpenIdConnectResponseType.Code;
                o.UsePkce = true;
                o.SaveTokens = true;
                o.GetClaimsFromUserInfoEndpoint = false;

                // Keep claim types as the raw JWT short names ("sub", "oid", "roles", ...)
                // instead of ASP.NET Core's default long schema-URI mapping, since the
                // client-principal envelope's name_typ/role_typ are documented as short names.
                o.MapInboundClaims = false;
                o.TokenValidationParameters.NameClaimType = "name";
                o.TokenValidationParameters.RoleClaimType = "roles";

                o.CallbackPath = $"{apiPrefix}/login/aad/callback";
                o.SignedOutCallbackPath = $"{apiPrefix}/logout/callback";

                foreach (var scope in aad.Scopes)
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
                        identity.AddClaim(new Claim(EasyAuthClaimTypes.IdentityProvider, EntraIdProvider.ProviderName));
                    }

                    return Task.CompletedTask;
                };
            });

        return builder;
    }
}
