// TypeScript equivalent of ../SampleApp.AppHost/AppHost.cs — same resources, same simulator,
// authored in TypeScript instead of C#. Requires no .NET code of its own beyond referencing the
// EasyAuthSimulator.Hosting assembly (see aspire.config.json's "packages" entry) for typed
// bindings; the simulator itself runs as the "easyauthsimulator" container image. The app it
// fronts here happens to be the .NET SampleApp, but addProject() below could equally be
// addContainer()/addExecutable() pointing at a Go binary or a Node app.
//
// For more information, see: https://aspire.dev

import { createBuilder } from './.aspire/modules/aspire.mjs';

const builder = await createBuilder();

const tenantId = builder.addParameter('entra-tenant-id');
const clientId = builder.addParameter('entra-client-id');
const clientSecret = builder.addParameter('entra-client-secret', { secret: true });

const api = await builder.addProject('api', '../SampleApp');

// authenticationRequired defaults to false, so unauthenticated requests reach the app instead
// of being redirected straight into a provider — giving the app a chance to show a picker
// across both providers configured below.
await builder
  .addEasyAuthSimulator('auth')
  .withUpstream(api)
  .withEntraId(tenantId, clientId, clientSecret)
  .withCustomOpenIdConnect(
    "demo",
    "https://demo.duendesoftware.com",
    builder.addParameter("demo-client-id"),
    builder.addParameter("demo-client-secret", { secret: true })
  )
  .withExternalHttpEndpoints();

await builder.build().run();
