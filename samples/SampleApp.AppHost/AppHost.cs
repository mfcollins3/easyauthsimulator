var builder = DistributedApplication.CreateBuilder(args);

var tenantId = builder.AddParameter("entra-tenant-id");
var clientId = builder.AddParameter("entra-client-id");
var clientSecret = builder.AddParameter("entra-client-secret", secret: true);

var api = builder.AddProject<Projects.SampleApp>("api");

// AuthenticationRequired defaults to false, so unauthenticated requests reach the app instead
// of being redirected straight into a provider — giving the app a chance to show a picker
// across both providers configured below.
builder.AddEasyAuthSimulator("auth")
    .WithUpstream(api)
    .WithEntraId(tenantId, clientId, clientSecret)
    .WithCustomOpenIdConnect(
        "demo",
        "https://demo.duendesoftware.com",
        builder.AddParameter("demo-client-id"),
        builder.AddParameter("demo-client-secret", secret: true)
    )
    .WithExternalHttpEndpoints();

builder.Build().Run();
