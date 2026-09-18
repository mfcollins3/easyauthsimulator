using System.Security.Claims;
using EasyAuthSimulator.Providers;

namespace EasyAuthSimulator.Tests.TestSupport;

public sealed class TestEasyAuthProvider : IEasyAuthProvider
{
    public const string ProviderName = "test";

    public string Name => ProviderName;
    public string TokenHeaderInfix => "TEST";
    public string AuthenticationScheme => TestAuthHandler.SchemeName;
    public string NameClaimType => "name";
    public string RoleClaimType => "roles";

    public string? ResolvePrincipalId(ClaimsPrincipal principal) => principal.FindFirst("sub")?.Value;

    public string? ResolvePrincipalName(ClaimsPrincipal principal) => principal.FindFirst("preferred_username")?.Value;

    public IEnumerable<Claim> ProjectClaims(ClaimsPrincipal principal) =>
        principal.Claims.Where(c => c.Type != EasyAuthClaimTypes.IdentityProvider);

    public Task<IReadOnlyDictionary<string, string>?> RefreshTokensAsync(
        IReadOnlyDictionary<string, string> currentTokens, CancellationToken cancellationToken)
    {
        var refreshed = new Dictionary<string, string>
        {
            ["access_token"] = "refreshed-access-token",
            ["expires_at"] = DateTimeOffset.UtcNow.AddHours(2).ToString("o"),
        };
        return Task.FromResult<IReadOnlyDictionary<string, string>?>(refreshed);
    }
}
