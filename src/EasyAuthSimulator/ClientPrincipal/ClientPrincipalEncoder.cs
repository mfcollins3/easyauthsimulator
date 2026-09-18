using System.Security.Claims;
using System.Text;
using System.Text.Json;
using EasyAuthSimulator.Providers;

namespace EasyAuthSimulator.ClientPrincipal;

public static class ClientPrincipalEncoder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Builds the X-MS-CLIENT-PRINCIPAL header value: standard (padded) base64 of the JSON
    /// envelope — confirmed against Learn's own C# decode sample, which uses
    /// Convert.FromBase64String rather than a base64url decoder.
    /// </summary>
    public static string Encode(IEasyAuthProvider provider, ClaimsPrincipal principal)
    {
        var claims = provider.ProjectClaims(principal)
            .Select(c => new ClientPrincipalClaim(c.Type, c.Value))
            .ToArray();

        var envelope = new ClientPrincipalEnvelope(provider.Name, claims, provider.NameClaimType, provider.RoleClaimType);
        var json = JsonSerializer.Serialize(envelope, SerializerOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
}
