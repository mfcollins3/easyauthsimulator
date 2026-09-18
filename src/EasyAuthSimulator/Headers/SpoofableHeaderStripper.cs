using System.Net.Http.Headers;

namespace EasyAuthSimulator.Headers;

/// <summary>
/// Real EasyAuth guarantees "external requests aren't allowed to set these headers" but never
/// publishes the strip list, so this removes every header a client could use to spoof identity:
/// all X-MS-CLIENT-PRINCIPAL* variants, all X-MS-TOKEN-* variants, and X-ZUMO-AUTH.
/// </summary>
public static class SpoofableHeaderStripper
{
    public static void Strip(HttpRequestHeaders headers)
    {
        var toRemove = headers
            .Where(h =>
                h.Key.StartsWith(EasyAuthHeaderNames.ClientPrincipalPrefix, StringComparison.OrdinalIgnoreCase) ||
                h.Key.StartsWith(EasyAuthHeaderNames.TokenHeaderPrefix, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(h.Key, EasyAuthHeaderNames.ZumoAuth, StringComparison.OrdinalIgnoreCase))
            .Select(h => h.Key)
            .ToList();

        foreach (var header in toRemove)
        {
            headers.Remove(header);
        }
    }
}
