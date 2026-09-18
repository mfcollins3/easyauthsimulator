using EasyAuthSimulator.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace EasyAuthSimulator.Middleware;

/// <summary>
/// The single source of truth for "does this request need to be authenticated", mirroring
/// globalValidation.requireAuthentication + unauthenticatedClientAction + excludedPaths. This
/// lives in middleware rather than an ASP.NET Core [Authorize] policy because the four
/// unauthenticatedClientAction behaviors plus wildcard excludedPaths don't map cleanly onto
/// policy-based authorization — the proxied route itself uses the "anonymous" policy and
/// leaves the actual gating decision entirely to this middleware.
/// </summary>
public sealed class GlobalValidationMiddleware(RequestDelegate next, IOptions<EasyAuthOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var opts = options.Value;

        if (!opts.RequireAuthentication
            || context.GetEndpoint()?.Metadata.GetMetadata<EasyAuthEndpointMarker>() is not null
            || context.Request.Path.StartsWithSegments(opts.ApiPrefix, StringComparison.OrdinalIgnoreCase)
            || IsExcluded(context.Request.Path, opts.ExcludedPaths)
            || context.User.Identity?.IsAuthenticated == true)
        {
            await next(context);
            return;
        }

        switch (opts.UnauthenticatedAction)
        {
            case UnauthenticatedClientAction.AllowAnonymous:
                await next(context);
                return;

            case UnauthenticatedClientAction.Return401:
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;

            case UnauthenticatedClientAction.Return403:
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;

            case UnauthenticatedClientAction.Return404:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;

            case UnauthenticatedClientAction.RedirectToLoginPage:
            default:
                var originalUrl = context.Request.Path + context.Request.QueryString;
                var loginUrl = $"{opts.ApiPrefix}/login/{opts.DefaultProvider}?post_login_redirect_uri={Uri.EscapeDataString(originalUrl)}";
                context.Response.Redirect(loginUrl);
                return;
        }
    }

    private static bool IsExcluded(PathString path, IReadOnlyList<string> excludedPaths)
    {
        var value = path.Value ?? "/";
        foreach (var pattern in excludedPaths)
        {
            if (pattern.EndsWith("/*", StringComparison.Ordinal))
            {
                var prefix = pattern[..^1];
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
