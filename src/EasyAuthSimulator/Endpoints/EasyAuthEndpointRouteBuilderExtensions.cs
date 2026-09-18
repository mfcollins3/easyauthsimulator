using EasyAuthSimulator.ClientPrincipal;
using EasyAuthSimulator.Middleware;
using EasyAuthSimulator.Options;
using EasyAuthSimulator.Providers;
using EasyAuthSimulator.RedirectValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EasyAuthSimulator.Endpoints;

public static class EasyAuthEndpointRouteBuilderExtensions
{
    /// <summary>Maps the /.auth/* surface (login, logout, me, refresh) under the configured ApiPrefix.</summary>
    public static IEndpointRouteBuilder MapEasyAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<EasyAuthOptions>>().Value;

        var group = endpoints.MapGroup(options.ApiPrefix).WithMetadata(new EasyAuthEndpointMarker());

        group.MapGet("/login/{provider}", LoginHandler);
        group.MapGet("/logout", LogoutHandler);
        group.MapGet("/logout/done", () => Results.Content("Signed out.", "text/plain"));
        group.MapGet("/logout/complete", () => Results.Content("Signed out.", "text/plain"));
        group.MapGet("/me", MeHandler);
        group.MapGet("/refresh", RefreshHandler);

        return endpoints;
    }

    private static IResult LoginHandler(
        string provider,
        string? post_login_redirect_uri,
        HttpContext context,
        IOptions<EasyAuthOptions> optionsAccessor,
        EasyAuthProviderRegistry registry)
    {
        if (!registry.TryGet(provider, out var providerImpl))
        {
            return Results.NotFound($"Unknown identity provider '{provider}'.");
        }

        RedirectUriValidator.TryValidate(
            post_login_redirect_uri, context.Request, optionsAccessor.Value.AllowedExternalRedirectHosts, out var redirectUri);

        var properties = new AuthenticationProperties { RedirectUri = redirectUri };
        return Results.Challenge(properties, [providerImpl.AuthenticationScheme]);
    }

    private static IResult LogoutHandler(
        string? post_logout_redirect_uri,
        HttpContext context,
        IOptions<EasyAuthOptions> optionsAccessor,
        EasyAuthProviderRegistry registry)
    {
        var options = optionsAccessor.Value;
        var redirectUri = $"{options.ApiPrefix}/logout/done";
        if (!string.IsNullOrEmpty(post_logout_redirect_uri)
            && RedirectUriValidator.TryValidate(post_logout_redirect_uri, context.Request, options.AllowedExternalRedirectHosts, out var validatedUri))
        {
            redirectUri = validatedUri;
        }

        var properties = new AuthenticationProperties { RedirectUri = redirectUri };

        var schemes = new List<string> { CookieAuthenticationDefaults.AuthenticationScheme };
        var idpName = context.User.FindFirst(EasyAuthClaimTypes.IdentityProvider)?.Value;
        if (idpName is not null && registry.TryGet(idpName, out var provider))
        {
            schemes.Add(provider.AuthenticationScheme);
        }

        return Results.SignOut(properties, schemes);
    }

    private static async Task<IResult> MeHandler(HttpContext context, EasyAuthProviderRegistry registry)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var idpName = context.User.FindFirst(EasyAuthClaimTypes.IdentityProvider)?.Value;
        if (idpName is null || !registry.TryGet(idpName, out var provider))
        {
            return Results.Unauthorized();
        }

        var authenticateResult = await context.AuthenticateAsync();
        var tokens = authenticateResult.Properties?.GetTokens().ToList() ?? [];
        string? Token(string name) => tokens.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal))?.Value;

        var info = new ClientPrincipalInfo(
            AccessToken: Token("access_token"),
            AccessTokenSecret: null,
            AuthenticationToken: null,
            ExpiresOn: Token("expires_at"),
            IdToken: Token("id_token"),
            ProviderName: provider.Name,
            RefreshToken: Token("refresh_token"),
            UserClaims: provider.ProjectClaims(context.User).Select(c => new ClientPrincipalClaim(c.Type, c.Value)).ToArray(),
            UserId: provider.ResolvePrincipalId(context.User));

        return Results.Json(new[] { info });
    }

    private static async Task<IResult> RefreshHandler(HttpContext context, EasyAuthProviderRegistry registry)
    {
        var authenticateResult = await context.AuthenticateAsync();
        if (!authenticateResult.Succeeded || authenticateResult.Ticket is null || authenticateResult.Properties is null)
        {
            return Results.Unauthorized();
        }

        var idpName = authenticateResult.Principal!.FindFirst(EasyAuthClaimTypes.IdentityProvider)?.Value;
        if (idpName is null || !registry.TryGet(idpName, out var provider))
        {
            return Results.Unauthorized();
        }

        var currentTokens = authenticateResult.Properties.GetTokens()
            .ToDictionary(t => t.Name, t => t.Value, StringComparer.Ordinal);

        var refreshed = await provider.RefreshTokensAsync(currentTokens, context.RequestAborted);
        if (refreshed is null)
        {
            return Results.Unauthorized();
        }

        foreach (var (key, value) in refreshed)
        {
            authenticateResult.Properties.UpdateTokenValue(key, value);
        }

        await context.SignInAsync(authenticateResult.Principal, authenticateResult.Properties);
        return Results.Ok();
    }
}
