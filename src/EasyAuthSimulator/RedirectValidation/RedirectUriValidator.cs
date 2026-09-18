using Microsoft.AspNetCore.Http;

namespace EasyAuthSimulator.RedirectValidation;

/// <summary>
/// Guards against post_login_redirect_uri/post_logout_redirect_uri open-redirect abuse:
/// accepts a relative path or an absolute URI on the current host or an explicitly
/// allow-listed external host (mirroring real EasyAuth's allowedExternalRedirectUrls);
/// rejects everything else rather than silently falling back to it.
/// </summary>
public static class RedirectUriValidator
{
    public static bool TryValidate(
        string? candidate, HttpRequest currentRequest, IReadOnlyCollection<string> allowedExternalHosts, out string safeUri)
    {
        safeUri = "/";

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        // A same-document relative path ("/foo") is always fine. Reject "//host/path" and
        // "/\host/path" forms, which browsers can interpret as protocol-relative absolute URIs.
        if (candidate.StartsWith('/') && !candidate.StartsWith("//") && !candidate.StartsWith("/\\"))
        {
            safeUri = candidate;
            return true;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var isSameHost = string.Equals(uri.Host, currentRequest.Host.Host, StringComparison.OrdinalIgnoreCase);
        var isAllowedExternal = allowedExternalHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
        if (!isSameHost && !isAllowedExternal)
        {
            return false;
        }

        safeUri = uri.ToString();
        return true;
    }
}
