using System.Collections;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace EasyAuthSimulator.Configuration;

/// <summary>
/// Maps the simulator's flat EASYAUTH_* environment variables onto the nested "EasyAuth:*"
/// configuration keys that <see cref="Options.EasyAuthOptions"/>, <see cref="Options.AadProviderOptions"/>
/// and <see cref="Options.CustomOpenIdConnectProviderOptions"/> bind from, so Aspire (or a plain
/// shell) can configure the simulator with simple names instead of double-underscore nesting.
/// </summary>
public static partial class EasyAuthEnvironmentConfiguration
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

    // Custom OIDC provider names aren't known at compile time (unlike the fixed "aad" slug), so
    // they're matched by pattern instead of a fixed KeyAliases entry: EASYAUTH_OIDC_<NAME>_<FIELD>.
    [GeneratedRegex(
        "^EASYAUTH_OIDC_(?<name>[A-Za-z0-9]+)_(?<field>CLIENT_ID|CLIENT_SECRET|AUTHORITY|METADATA_ADDRESS|SCOPES|NAME_CLAIM_TYPE|ROLE_CLAIM_TYPE)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex CustomOidcKeyPattern();

    private static readonly Dictionary<string, string> CustomOidcFieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CLIENT_ID"] = "ClientId",
        ["CLIENT_SECRET"] = "ClientSecret",
        ["AUTHORITY"] = "Authority",
        ["METADATA_ADDRESS"] = "MetadataAddress",
        ["SCOPES"] = "Scopes",
        ["NAME_CLAIM_TYPE"] = "NameClaimType",
        ["ROLE_CLAIM_TYPE"] = "RoleClaimType",
    };

    public static void Apply(IConfigurationBuilder configurationBuilder)
    {
        var values = new Dictionary<string, string?>();

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = (string)entry.Key;
            if (entry.Value is not string value)
            {
                continue;
            }

            if (KeyAliases.TryGetValue(key, out var mappedKey))
            {
                if (DelimitedListKeys.Contains(key))
                {
                    AddDelimitedList(values, mappedKey, value);
                }
                else
                {
                    values[mappedKey] = value;
                }

                continue;
            }

            var customOidcMatch = CustomOidcKeyPattern().Match(key);
            if (customOidcMatch.Success)
            {
                var providerName = customOidcMatch.Groups["name"].Value;
                var field = CustomOidcFieldAliases[customOidcMatch.Groups["field"].Value];
                var customMappedKey = $"EasyAuth:CustomOpenIdConnect:{providerName}:{field}";

                if (field == "Scopes")
                {
                    AddDelimitedList(values, customMappedKey, value);
                }
                else
                {
                    values[customMappedKey] = value;
                }
            }
        }

        configurationBuilder.AddInMemoryCollection(values);
    }

    private static void AddDelimitedList(Dictionary<string, string?> values, string mappedKey, string value)
    {
        var items = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < items.Length; i++)
        {
            values[$"{mappedKey}:{i}"] = items[i];
        }
    }
}
