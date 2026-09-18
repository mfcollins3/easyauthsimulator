using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyAuthSimulator.Tests.TestSupport;

/// <summary>
/// Stands in for a real OIDC round-trip: authenticates every request as a fixed fake user,
/// unless the caller sends <see cref="AnonymousHeader"/>. This lets tests exercise the
/// simulator's header injection, /.auth/* endpoints and global validation gate without a real
/// identity provider — and doubles as proof that a provider other than Entra ID can plug into
/// the same pipeline.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder), IAuthenticationSignInHandler
{
    public const string SchemeName = "Test";
    public const string AnonymousHeader = "X-Test-Anonymous";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey(AnonymousHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "test-sub-123"),
            new Claim("preferred_username", "test.user@example.com"),
            new Claim("name", "Test User"),
            new Claim("roles", "editor"),
            new Claim(EasyAuthClaimTypes.IdentityProvider, TestEasyAuthProvider.ProviderName),
        ], SchemeName, "name", "roles");

        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "id_token", Value = "test-id-token" },
            new AuthenticationToken { Name = "access_token", Value = "test-access-token" },
            new AuthenticationToken { Name = "refresh_token", Value = "test-refresh-token" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddHours(1).ToString("o") },
        ]);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    // No real session to write to — each request re-authenticates via HandleAuthenticateAsync.
    public Task SignOutAsync(AuthenticationProperties? properties) => Task.CompletedTask;

    public Task SignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties) => Task.CompletedTask;
}
