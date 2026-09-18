var builder = DistributedApplication.CreateBuilder(args);

var tenantId = builder.AddParameter("entra-tenant-id");
var clientId = builder.AddParameter("entra-client-id");
var clientSecret = builder.AddParameter("entra-client-secret", secret: true);

var api = builder.AddProject<Projects.SampleApp>("api");

builder.AddEasyAuthSimulator("auth", port: 8080)
    .WithUpstream(api)
    .WithEntraId(tenantId, clientId, clientSecret)
    .WithExternalHttpEndpoints();

builder.Build().Run();
