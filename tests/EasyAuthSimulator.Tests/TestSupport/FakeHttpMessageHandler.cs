namespace EasyAuthSimulator.Tests.TestSupport;

/// <summary>An <see cref="HttpMessageHandler"/> stand-in for an outbound call (e.g. Entra's token endpoint) that never leaves the process.</summary>
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(responder(request, cancellationToken));
}

public sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
