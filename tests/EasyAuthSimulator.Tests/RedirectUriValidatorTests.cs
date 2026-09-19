using EasyAuthSimulator.RedirectValidation;
using Microsoft.AspNetCore.Http;

namespace EasyAuthSimulator.Tests;

public sealed class RedirectUriValidatorTests
{
    [Theory]
    [InlineData("/relative/path", true, "/relative/path")]
    [InlineData("/relative/path?foo=bar", true, "/relative/path?foo=bar")]
    [InlineData("https://myapp.example.com/ok", true, "https://myapp.example.com/ok")]
    [InlineData("https://evil.example.com/steal", false, "/")]
    [InlineData("//evil.example.com/steal", false, "/")]
    [InlineData("/\\evil.example.com/steal", false, "/")]
    [InlineData("not a uri", false, "/")]
    [InlineData("ftp://myapp.example.com/ok", false, "/")]
    [InlineData("", false, "/")]
    [InlineData(null, false, "/")]
    public void TryValidate_OnlyAcceptsRelativePathsOrSameHostRedirects(string? candidate, bool expectedValid, string expectedUri)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("myapp.example.com");

        var isValid = RedirectUriValidator.TryValidate(candidate, context.Request, [], out var safeUri);

        Assert.Equal(expectedValid, isValid);
        Assert.Equal(expectedUri, safeUri);
    }

    [Fact]
    public void TryValidate_AllowsExplicitlyAllowlistedExternalHost()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("myapp.example.com");

        var isValid = RedirectUriValidator.TryValidate(
            "https://partner.example.com/return", context.Request, ["partner.example.com"], out var safeUri);

        Assert.True(isValid);
        Assert.Equal("https://partner.example.com/return", safeUri);
    }

    [Fact]
    public void TryValidate_MatchesAllowlistedExternalHost_CaseInsensitively()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("myapp.example.com");

        var isValid = RedirectUriValidator.TryValidate(
            "https://PARTNER.example.com/return", context.Request, ["partner.example.com"], out var safeUri);

        Assert.True(isValid);
        // Uri.ToString() normalizes the host to lowercase.
        Assert.Equal("https://partner.example.com/return", safeUri);
    }

    [Fact]
    public void TryValidate_RejectsExternalHost_NotOnTheAllowlist()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("myapp.example.com");

        var isValid = RedirectUriValidator.TryValidate(
            "https://not-allowed.example.com/return", context.Request, ["partner.example.com"], out var safeUri);

        Assert.False(isValid);
        Assert.Equal("/", safeUri);
    }
}
