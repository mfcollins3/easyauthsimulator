# EasyAuth Simulator

A local stand-in for [Azure Container Apps' built-in authentication ("EasyAuth")](https://learn.microsoft.com/en-us/azure/container-apps/authentication), for developing and debugging container apps locally with real, authenticated users.

It's a YARP reverse proxy that performs a genuine OIDC sign-in against Microsoft Entra ID, then injects the same `X-MS-CLIENT-PRINCIPAL*` / `X-MS-TOKEN-*` headers and exposes the same `/.auth/*` endpoints that Container Apps EasyAuth does in production, before forwarding the request to your app. Your app needs zero auth-related code changes between local development and Azure.

## Project layout

- `src/EasyAuthSimulator` — the proxy, distributed as a container image (see the `Dockerfile`).
- `src/EasyAuthSimulator.Hosting` — an [Aspire](https://aspire.dev) hosting integration (`AddEasyAuthSimulator()` etc.) that runs that image as a container resource; a tiny library with no dependency on the proxy's own build output.
- `samples/SampleApp` — a minimal API that reads the injected headers.
- `samples/SampleApp.AppHost` — runs the sample via a C# AppHost.
- `samples/SampleApp.AppHost.TypeScript` — the same sample, wired up via a [TypeScript AppHost](https://aspire.dev/app-host/typescript-apphost/) instead.
- `tests/EasyAuthSimulator.Tests` — contract tests against the documented EasyAuth header/endpoint behavior.

## Prerequisites: register an app in Microsoft Entra ID

1. Register an app in your tenant (Entra admin center → App registrations → New registration).
2. Add a **Redirect URI** (platform: Web) of `http://localhost:8080/.auth/login/aad/callback` — adjust the port if you configure a different `EASYAUTH_PORT`. Entra only permits plain `http://` for `localhost`.
3. Create a **client secret** (Certificates & secrets).
4. Enable ID token issuance, which Container Apps' own docs call out as required for EasyAuth:
   ```
   az ad app update --id <app-id> --enable-id-token-issuance true
   ```

You'll need the tenant ID, client ID, and client secret from this registration.

## Running with Aspire

```csharp
var secret = builder.AddParameter("entra-client-secret", secret: true);
var tenantId = builder.AddParameter("entra-tenant-id");
var clientId = builder.AddParameter("entra-client-id");

var api = builder.AddProject<Projects.MyApi>("api");

builder.AddEasyAuthSimulator("auth", port: 8080)
    .WithUpstream(api)
    .WithEntraId(tenantId, clientId, secret)
    .WithExternalHttpEndpoints();
```

`AddEasyAuthSimulator` needs your AppHost to reference `EasyAuthSimulator.Hosting.csproj` (`IsAspireProjectResource="false"`, since the simulator runs as a container resource rather than an Aspire project resource) — see `samples/SampleApp.AppHost/SampleApp.AppHost.csproj`. It runs the `easyauthsimulator` image, so build it first:

```bash
docker build -t easyauthsimulator:latest .
```

Aspire's container runtime picks up that locally-built image directly — no registry needed for local development. For a shared/CI setup, push it to a registry and point at it with the standard Aspire container methods instead, e.g. `.WithImageRegistry("myregistry.azurecr.io")` or `.WithImageTag("1.2.3")` on the resource `AddEasyAuthSimulator` returns.

`WithUpstream` accepts any Aspire resource with an HTTP endpoint, so `api` above can just as well be a Go executable (`AddExecutable`) or a Node app (Aspire's JavaScript hosting integration).

Put the tenant ID, client ID, and secret in user secrets (`Parameters:entra-tenant-id`, etc.) — never in source.

**The port is pinned, not Aspire-allocated.** Entra validates the redirect URI against your app registration, so a random port would break sign-in on every run. Pick a port, register that exact redirect URI, and pass the same port to `AddEasyAuthSimulator`.

See `samples/SampleApp.AppHost/AppHost.cs` for a complete example.

### From a TypeScript AppHost

The same three methods are available from a TypeScript AppHost — no C# authoring required on your
side, just a reference to this project so the CLI can generate typed bindings for it:

```typescript
const tenantId = builder.addParameter('entra-tenant-id');
const clientId = builder.addParameter('entra-client-id');
const clientSecret = builder.addParameter('entra-client-secret', { secret: true });

const api = await builder.addProject('api', '../MyApi');

await builder
  .addEasyAuthSimulator('auth', { port: 8080 })
  .withUpstream(api)
  .withEntraId(tenantId, clientId, clientSecret)
  .withExternalHttpEndpoints();
```

Add it to your AppHost's `aspire.config.json` under `packages`, pointing at the hosting project by
path for local development (swap for a published NuGet version once you publish one):

```json
{
  "packages": {
    "EasyAuthSimulator": "../../path/to/EasyAuthSimulator.Hosting.csproj"
  }
}
```

Then run `aspire restore` to regenerate the SDK, or just `aspire run`. See
`samples/SampleApp.AppHost.TypeScript/apphost.mts` for the complete, working example.

## Running standalone

```bash
export EASYAUTH_PORT=8080
export EASYAUTH_UPSTREAM_URL=http://localhost:5000
export EASYAUTH_AAD_TENANT_ID=<tenant-id>
export EASYAUTH_AAD_CLIENT_ID=<client-id>
export EASYAUTH_AAD_CLIENT_SECRET=<client-secret>
dotnet run --project src/EasyAuthSimulator
```

### Configuration (environment variables)

| Variable | Default | Purpose |
|---|---|---|
| `EASYAUTH_PORT` | 8080 | Listen port. |
| `EASYAUTH_UPSTREAM_URL` | *(required)* | The app to forward authenticated requests to. |
| `EASYAUTH_PROVIDER` | `aad` | Default provider used when redirecting to sign in. |
| `EASYAUTH_AAD_TENANT_ID` / `_CLIENT_ID` / `_CLIENT_SECRET` | *(required)* | Your Entra app registration. |
| `EASYAUTH_AAD_AUTHORITY_HOST` | `login.microsoftonline.com` | Override for sovereign clouds. |
| `EASYAUTH_AAD_SCOPES` | `openid;profile;email;offline_access` | `;`-separated. `offline_access` is required for a refresh token. |
| `EASYAUTH_REQUIRE_AUTHENTICATION` | `true` | Mirrors `globalValidation.requireAuthentication`. |
| `EASYAUTH_UNAUTHENTICATED_ACTION` | `RedirectToLoginPage` | `RedirectToLoginPage` \| `AllowAnonymous` \| `Return401` \| `Return403` \| `Return404` (or the `RejectWith401`/`RejectWith404` file-config spellings). |
| `EASYAUTH_EXCLUDED_PATHS` | — | `;`-separated; entries may end in `/*` for a prefix match. |
| `EASYAUTH_API_PREFIX` | `/.auth` | Relocates the `/.auth/*` surface. |
| `EASYAUTH_FORWARD_PROXY_CONVENTION` | `Standard` | `NoProxy` \| `Standard` \| `Custom`. |
| `EASYAUTH_CUSTOM_PROTO_HEADER_NAME` / `_HOST_HEADER_NAME` | — | Used when the convention above is `Custom`. |
| `EASYAUTH_FORWARD_ORIGINAL_HOST` | `true` | Forwards the original ingress Host header to your app. |
| `EASYAUTH_ALLOWED_EXTERNAL_REDIRECT_HOSTS` | — | `;`-separated hosts allowed for `post_login_redirect_uri`/`post_logout_redirect_uri` beyond the current host. |

## Adding another identity provider

Implement `IEasyAuthProvider` (`src/EasyAuthSimulator/Providers/IEasyAuthProvider.cs`) and register it alongside its authentication handler in a `AddXxx()` extension method modeled on `EntraIdServiceCollectionExtensions.AddEntraId()`. Nothing else in the pipeline needs to change — routing, header injection, and the `/.auth/*` endpoints are all provider-agnostic.

## Known deviations from the real service

A few details of real EasyAuth are undocumented publicly and are called out with `[inferred]` comments in the source where a best-effort guess was made instead of a confirmed behavior — notably which claim backs `X-MS-CLIENT-PRINCIPAL-ID` for Entra ID, the exact `/.auth/me` schema, and the session cookie's name/chunking behavior. Not yet implemented: the `X-ZUMO-AUTH` client-directed POST login flow, and the `Origin`/`Referer` CSRF check real EasyAuth applies to cookie-authenticated POSTs.

## Tests

```bash
dotnet test
```

## Distributing the simulator

Two artifacts ship independently:

- **The `easyauthsimulator` container image** — the actual proxy. Build and push it like any
  other container:
  ```bash
  docker build -t <your-registry>/easyauthsimulator:<version> .
  docker push <your-registry>/easyauthsimulator:<version>
  ```
- **The `EasyAuthSimulator.Hosting` NuGet package** — the Aspire hosting integration, a small
  library with no dependency on the proxy's own build output:
  ```bash
  dotnet pack src/EasyAuthSimulator.Hosting/EasyAuthSimulator.Hosting.csproj -c Release -o ./nupkg
  ```
  Push the resulting `.nupkg` to whatever feed you use (a local folder feed, Azure Artifacts,
  GitHub Packages, etc.) — see [`dotnet nuget push`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push).
  From a TypeScript AppHost, add it to `aspire.config.json` by version instead of by path (see
  above) once it's available from a feed the AppHost's restore can see.

If you publish the image under a different name/tag than `easyauthsimulator:latest`, override it
per-AppHost with `.WithImageRegistry()` / `.WithImage()` / `.WithImageTag()` on the resource
`AddEasyAuthSimulator` returns, rather than forking the hosting package.
