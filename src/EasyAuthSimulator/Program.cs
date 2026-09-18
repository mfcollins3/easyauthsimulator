using EasyAuthSimulator.Configuration;
using EasyAuthSimulator.Extensions;

// EASYAUTH_PORT wins over ASPNETCORE_HTTP_PORTS / ASPNETCORE_URLS when set, because the
// simulator's listen port must be stable: it's baked into the Entra app registration's
// redirect URI, and Aspire's usual random-port allocation would break sign-in on every run.
var port = Environment.GetEnvironmentVariable("EASYAUTH_PORT");

var builder = WebApplication.CreateBuilder(args);

if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

EasyAuthEnvironmentConfiguration.Apply(builder.Configuration);

builder.Services
    .AddEasyAuth(builder.Configuration)
    .AddEntraId();

var app = builder.Build();

app.UseEasyAuth();

app.Run();

// Exposed so EasyAuthSimulator.Tests can host this app via WebApplicationFactory<Program>.
public partial class Program;
