using System.Collections;
using Microsoft.Extensions.Configuration;

namespace EasyAuthSimulator.Configuration;

/// <summary>
/// Maps the simulator's flat EASYAUTH_* environment variables onto the nested "EasyAuth:*"
/// configuration keys that <see cref="Options.EasyAuthOptions"/> and
/// <see cref="Options.AadProviderOptions"/> bind from, so Aspire (or a plain shell) can
/// configure the simulator with simple names instead of double-underscore nesting.
/// </summary>
public static class EasyAuthEnvironmentConfiguration
{
    private static readonly Dictionary<string, string> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EASYAUTH_UPSTREAM_URL"] = "EasyAuth:UpstreamUrl",
        ["EASYAUTH_PROVIDER"] = "EasyAuth:DefaultProvider",
        ["EASYAUTH_REQUIRE_AUTHENTICATION"] = "EasyAuth:RequireAuthentication",
        ["EASYAUTH_UNAUTHENTICATED_ACTION"] = "EasyAuth:UnauthenticatedAction",
        ["EASYAUTH_EXCLUDED_PATHS"] = "EasyAuth:ExcludedPaths",
        ["EASYAUTH_API_PREFIX"] = "EasyAuth:ApiPrefix",
        ["EASYAUTH_FORWARD_PROXY_CONVENTION"] = "EasyAuth:ForwardProxyConvention",
        ["EASYAUTH_FORWARD_ORIGINAL_HOST"] = "EasyAuth:ForwardOriginalHost",
        ["EASYAUTH_CUSTOM_PROTO_HEADER_NAME"] = "EasyAuth:CustomProtoHeaderName",
        ["EASYAUTH_CUSTOM_HOST_HEADER_NAME"] = "EasyAuth:CustomHostHeaderName",
        ["EASYAUTH_ALLOWED_EXTERNAL_REDIRECT_HOSTS"] = "EasyAuth:AllowedExternalRedirectHosts",
        ["EASYAUTH_AAD_TENANT_ID"] = "EasyAuth:Aad:TenantId",
        ["EASYAUTH_AAD_CLIENT_ID"] = "EasyAuth:Aad:ClientId",
        ["EASYAUTH_AAD_CLIENT_SECRET"] = "EasyAuth:Aad:ClientSecret",
        ["EASYAUTH_AAD_AUTHORITY_HOST"] = "EasyAuth:Aad:AuthorityHost",
        ["EASYAUTH_AAD_SCOPES"] = "EasyAuth:Aad:Scopes",
    };

    private static readonly HashSet<string> DelimitedListKeys =
        new(StringComparer.OrdinalIgnoreCase) { "EASYAUTH_EXCLUDED_PATHS", "EASYAUTH_ALLOWED_EXTERNAL_REDIRECT_HOSTS", "EASYAUTH_AAD_SCOPES" };

    public static void Apply(IConfigurationBuilder configurationBuilder)
    {
        var values = new Dictionary<string, string?>();

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = (string)entry.Key;
            if (!KeyAliases.TryGetValue(key, out var mappedKey) || entry.Value is not string value)
            {
                continue;
            }

            if (DelimitedListKeys.Contains(key))
            {
                var items = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (var i = 0; i < items.Length; i++)
                {
                    values[$"{mappedKey}:{i}"] = items[i];
                }

                continue;
            }

            values[mappedKey] = value;
        }

        configurationBuilder.AddInMemoryCollection(values);
    }
}
