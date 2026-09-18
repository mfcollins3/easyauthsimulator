using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyAuthSimulator.Tests.TestSupport;

/// <summary>
/// A real HTTP server (YARP proxies to real addresses, not in-memory TestServer instances)
/// that echoes back the request path and headers it received, standing in for "the app under
/// development" in tests.
/// </summary>
public sealed class EchoUpstream : IAsyncDisposable
{
    private WebApplication? _app;

    public string Url { get; private set; } = "";

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();

        _app.Map("/{**catch-all}", async context =>
        {
            var headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { path = context.Request.Path.Value, headers }));
        });

        await _app.StartAsync();
        Url = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
