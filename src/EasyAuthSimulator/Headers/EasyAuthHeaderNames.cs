namespace EasyAuthSimulator.Headers;

public static class EasyAuthHeaderNames
{
    public const string ClientPrincipal = "X-MS-CLIENT-PRINCIPAL";
    public const string ClientPrincipalId = "X-MS-CLIENT-PRINCIPAL-ID";
    public const string ClientPrincipalName = "X-MS-CLIENT-PRINCIPAL-NAME";
    public const string ClientPrincipalIdp = "X-MS-CLIENT-PRINCIPAL-IDP";

    /// <summary>Shared prefix of all four X-MS-CLIENT-PRINCIPAL* headers, used to strip spoofed values.</summary>
    public const string ClientPrincipalPrefix = "X-MS-CLIENT-PRINCIPAL";

    public const string TokenHeaderPrefix = "X-MS-TOKEN-";
    public const string ZumoAuth = "X-ZUMO-AUTH";

    public static string TokenHeader(string providerInfix, string suffix) => $"{TokenHeaderPrefix}{providerInfix}-{suffix}";
}
