using EasyAuthSimulator.Headers;

namespace EasyAuthSimulator.Tests;

public sealed class SpoofableHeaderStripperTests
{
    [Fact]
    public void Strip_RemovesAllClientPrincipalAndTokenAndZumoHeaders_CaseInsensitively_AndLeavesOthersIntact()
    {
        using var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation("x-ms-client-principal", "forged");
        request.Headers.TryAddWithoutValidation("X-MS-CLIENT-PRINCIPAL-ID", "forged-id");
        request.Headers.TryAddWithoutValidation("X-MS-CLIENT-PRINCIPAL-NAME", "forged-name");
        request.Headers.TryAddWithoutValidation("X-MS-CLIENT-PRINCIPAL-IDP", "forged-idp");
        request.Headers.TryAddWithoutValidation("x-ms-token-aad-access-token", "forged-token");
        request.Headers.TryAddWithoutValidation("X-MS-TOKEN-AAD-REFRESH-TOKEN", "forged-refresh");
        request.Headers.TryAddWithoutValidation("x-zumo-auth", "forged-zumo");
        request.Headers.TryAddWithoutValidation("X-Custom-Header", "keep-me");
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        SpoofableHeaderStripper.Strip(request.Headers);

        Assert.False(request.Headers.Contains("x-ms-client-principal"));
        Assert.False(request.Headers.Contains("X-MS-CLIENT-PRINCIPAL-ID"));
        Assert.False(request.Headers.Contains("X-MS-CLIENT-PRINCIPAL-NAME"));
        Assert.False(request.Headers.Contains("X-MS-CLIENT-PRINCIPAL-IDP"));
        Assert.False(request.Headers.Contains("x-ms-token-aad-access-token"));
        Assert.False(request.Headers.Contains("X-MS-TOKEN-AAD-REFRESH-TOKEN"));
        Assert.False(request.Headers.Contains("x-zumo-auth"));
        Assert.True(request.Headers.Contains("X-Custom-Header"));
        Assert.True(request.Headers.Contains("Accept"));
    }

    [Fact]
    public void Strip_IsNoOp_WhenNoSpoofableHeadersPresent()
    {
        using var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        SpoofableHeaderStripper.Strip(request.Headers);

        Assert.True(request.Headers.Contains("Accept"));
    }
}
