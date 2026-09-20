using System.Net;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", (HttpContext context) =>
{
    var principal = ReadClientPrincipal(context);
    var html = principal is null
        ? """
          <h1>Not signed in</h1>
          <p><a href="/.auth/login/aad">Sign in with Microsoft Entra ID</a></p>
          <p><a href="/.auth/login/demo">Sign in with the demo OpenID Connect provider</a></p>
          """
        : $"""
          <h1>Signed in as {WebUtility.HtmlEncode(principal.Value.Name)}</h1>
          <p>Provider: {WebUtility.HtmlEncode(principal.Value.Idp)}</p>
          <p>
            <a href="/whoami">/whoami</a> |
            <a href="/.auth/me">/.auth/me</a> |
            <a href="/.auth/logout">Sign out</a>
          </p>
          """;

    return Results.Content(html, "text/html");
});

app.MapGet("/whoami", (HttpContext context) =>
{
    var headers = context.Request.Headers
        .Where(h => h.Key.StartsWith("X-MS-", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(h => h.Key, h => h.Value.ToString());

    var principal = ReadClientPrincipal(context);
    return Results.Json(new { headers, principal = principal?.Envelope });
});

app.Run();

// This app never references EasyAuthSimulator's own decode helper: the header is the entire
// contract, and a real container app has to parse it itself exactly like this.
static (string Name, string Idp, JsonElement Envelope)? ReadClientPrincipal(HttpContext context)
{
    if (!context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var encoded) || string.IsNullOrEmpty(encoded))
    {
        return null;
    }

    var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.ToString()));
    var envelope = JsonDocument.Parse(json).RootElement;
    var name = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].ToString();
    var idp = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-IDP"].ToString();
    return (name, idp, envelope);
}
