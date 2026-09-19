using System.Security.Claims;
using System.Text;
using System.Text.Json;
using EasyAuthSimulator.ClientPrincipal;
using EasyAuthSimulator.Tests.TestSupport;

namespace EasyAuthSimulator.Tests;

public sealed class ClientPrincipalEncoderTests
{
    [Fact]
    public void Encode_ProducesBase64EnvelopeWithDocumentedPropertyOrder_AndProviderProjectedClaims()
    {
        var provider = new TestEasyAuthProvider();
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "sub-value"),
            new Claim("roles", "editor"),
            new Claim(EasyAuthClaimTypes.IdentityProvider, TestEasyAuthProvider.ProviderName),
        ]);
        var principal = new ClaimsPrincipal(identity);

        var encoded = ClientPrincipalEncoder.Encode(provider, principal);

        var root = Decode(encoded);
        Assert.Equal(["auth_typ", "claims", "name_typ", "role_typ"], root.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(TestEasyAuthProvider.ProviderName, root.GetProperty("auth_typ").GetString());
        Assert.Equal("name", root.GetProperty("name_typ").GetString());
        Assert.Equal("roles", root.GetProperty("role_typ").GetString());

        var claims = root.GetProperty("claims").EnumerateArray()
            .Select(c => (Type: c.GetProperty("typ").GetString(), Value: c.GetProperty("val").GetString()))
            .ToArray();
        Assert.Equal(2, claims.Length);
        Assert.Contains(claims, c => c is { Type: "sub", Value: "sub-value" });
        Assert.Contains(claims, c => c is { Type: "roles", Value: "editor" });
        Assert.DoesNotContain(claims, c => c.Type == EasyAuthClaimTypes.IdentityProvider);
    }

    [Fact]
    public void Encode_ProducesEmptyClaimsArray_WhenPrincipalHasNoProjectableClaims()
    {
        var provider = new TestEasyAuthProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var encoded = ClientPrincipalEncoder.Encode(provider, principal);

        var root = Decode(encoded);
        Assert.Empty(root.GetProperty("claims").EnumerateArray());
    }

    [Fact]
    public void Encode_IsStandardPaddedBase64_NotBase64Url()
    {
        // A claim value chosen so the JSON payload's length forces standard base64 padding,
        // guarding against a switch to an unpadded/URL-safe encoder.
        var provider = new TestEasyAuthProvider();
        var identity = new ClaimsIdentity([new Claim("sub", "x")]);
        var principal = new ClaimsPrincipal(identity);

        var encoded = ClientPrincipalEncoder.Encode(provider, principal);

        // Throws FormatException if this isn't valid standard base64.
        Convert.FromBase64String(encoded);
        Assert.DoesNotContain('-', encoded);
        Assert.DoesNotContain('_', encoded);
    }

    private static JsonElement Decode(string encoded)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        return JsonDocument.Parse(json).RootElement;
    }
}
