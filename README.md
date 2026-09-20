# EasyAuth Simulator

A local stand-in for [Azure Container Apps' built-in authentication ("EasyAuth")](https://learn.microsoft.com/en-us/azure/container-apps/authentication), for developing and debugging container apps locally with real, authenticated users.

It's a YARP reverse proxy that performs a genuine OIDC sign-in against Microsoft Entra ID or any other OpenID Connect–compliant provider, then injects the same `X-MS-CLIENT-PRINCIPAL*` / `X-MS-TOKEN-*` headers and exposes the same `/.auth/*` endpoints that Container Apps EasyAuth does in production, before forwarding the request to your app. Your app needs zero auth-related code changes between local development and Azure.

## Supported identity providers

| Provider | Real EasyAuth | EasyAuthSimulator |
|---|---|---|
| Microsoft Entra ID | Yes — built-in (`aad`) | Yes — `WithEntraId()` |
| Any OpenID Connect–compliant provider (Google, Apple, Auth0, Okta, Duende IdentityServer, ...) | Yes — "Custom OpenID Connect" | Yes — `WithCustomOpenIdConnect()` |
| Facebook | Yes — built-in | No — Facebook Login isn't OpenID Connect–compliant, so there's no metadata document for `WithCustomOpenIdConnect()` to discover either |
| GitHub | Yes — built-in | No — same reason; GitHub's OAuth apps don't implement OpenID Connect |
| X (formerly Twitter) | Yes — built-in | No — same reason; OAuth, not OpenID Connect |
| Client-directed sign-in (POST a provider token to `/.auth/login/<provider>` for validation, then use `X-ZUMO-AUTH`, instead of a browser redirect) | Yes | No — not yet implemented, see [Known deviations](#known-deviations-from-the-real-service) |

Facebook, GitHub, and X aren't reachable through `WithCustomOpenIdConnect()` either, since none of them expose a standards-compliant `.well-known/openid-configuration` document for it to discover — see [Adding another identity provider](#adding-another-identity-provider) if you need one of those.

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

builder.AddEasyAuthSimulator("auth")
    .WithUpstream(api)
    .WithEntraId(tenantId, clientId, secret)
    .WithExternalHttpEndpoints();
```

`AddEasyAuthSimulator` needs your AppHost to reference `EasyAuthSimulator.Hosting.csproj` (`IsAspireProjectResource="false"`, since the simulator runs as a container resource rather than an Aspire project resource) — see `samples/SampleApp.AppHost/SampleApp.AppHost.csproj`. By default it pulls `ghcr.io/mfcollins3/easyauthsimulator`, anonymously, tagged at the same version as the `EasyAuthSimulator.Hosting` package you're using — no local build or registry login required.

To run against a locally-built image instead (e.g. while working on the proxy itself), build it and point at it explicitly:

```bash
docker build -t easyauthsimulator:latest -f src/EasyAuthSimulator/Dockerfile .
```

Run this from the repo root — the Dockerfile's `COPY . .` and publish path assume a repo-root
build context, not the `src/EasyAuthSimulator` directory. Then override the default with `.WithImage("easyauthsimulator")` on the resource `AddEasyAuthSimulator` returns (or `.WithImageRegistry()` / `.WithImageTag()` to point at a different published image).

`WithUpstream` accepts any Aspire resource with an HTTP endpoint, so `api` above can just as well be a Go executable (`AddExecutable`) or a Node app (Aspire's JavaScript hosting integration).

Put the tenant ID, client ID, and secret in user secrets (`Parameters:entra-tenant-id`, etc.) — never in source.

**The port is pinned, not Aspire-allocated.** Entra validates the redirect URI against your app registration, so a random port would break sign-in on every run. Pick a port, register that exact redirect URI, and pass the same port via `EasyAuthSimulatorOptions.Port` (it defaults to `8080`, which is what the examples above rely on):

```csharp
builder.AddEasyAuthSimulator("auth", new EasyAuthSimulatorOptions { Port = 8081 })
```

See `samples/SampleApp.AppHost/AppHost.cs` for a complete example.

### Using a custom OpenID Connect provider

`WithCustomOpenIdConnect()` wires up any spec-compliant OpenID Connect provider — Google, Apple, Auth0, Okta, Duende IdentityServer, your own IdentityServer/OpenIddict instance, etc. — discovered via its `{authority}/.well-known/openid-configuration` metadata document, no code required. Call it once per provider with a distinct name to enable more than one; each signs in at `/.auth/login/<name>`:

```csharp
builder.AddEasyAuthSimulator("auth")
    .WithUpstream(api)
    .WithCustomOpenIdConnect(
        "duende",
        "https://demo.duendesoftware.com",
        builder.AddParameter("duende-client-id"),
        builder.AddParameter("duende-client-secret", secret: true))
    .WithExternalHttpEndpoints();
```

You can combine this with `.WithEntraId(...)` to offer more than one provider. When you do, requests are unauthenticated by default (`EasyAuthSimulatorOptions.AuthenticationRequired = false`), so your app can render its own sign-in picker instead of being redirected straight into a single provider; set `AuthenticationRequired = true` (and `DefaultProvider`, if more than one provider is configured) to require sign-in instead. See `samples/SampleApp.AppHost/AppHost.cs` and `samples/SampleApp/Program.cs` for a complete example with two providers and a picker page.

### From a TypeScript AppHost

The same methods (`AddEasyAuthSimulator`, `WithUpstream`, `WithEntraId`, `WithCustomOpenIdConnect`) are
available from a TypeScript AppHost — no C# authoring required on your side, just a reference to this
project so the CLI can generate typed bindings for it:

```typescript
const tenantId = builder.addParameter('entra-tenant-id');
const clientId = builder.addParameter('entra-client-id');
const clientSecret = builder.addParameter('entra-client-secret', { secret: true });

const api = await builder.addProject('api', '../MyApi');

await builder
  .addEasyAuthSimulator('auth')
  .withUpstream(api)
  .withEntraId(tenantId, clientId, clientSecret)
  .withExternalHttpEndpoints();
```

Add it to your AppHost's `aspire.config.json` under `packages`, pointing at the hosting project by
path for local development (see [Using the published packages](#using-the-published-packages) below
for the published-NuGet-package form instead):

```json
{
  "packages": {
    "EasyAuthSimulator": "../../path/to/EasyAuthSimulator.Hosting.csproj"
  }
}
```

Then run `aspire restore` to regenerate the SDK, or just `aspire run`. See
`samples/SampleApp.AppHost.TypeScript/apphost.mts` for the complete, working example.

### Using the published packages

Both artifacts described in [Distributing the simulator](#distributing-the-simulator) are published
publicly from this repo's CI: the `easyauthsimulator` container image to the GitHub Container
Registry, and the `EasyAuthSimulator.Hosting` NuGet package to GitHub Packages.

**Container image** — public, no authentication needed to pull:

```bash
docker pull ghcr.io/mfcollins3/easyauthsimulator:<version>
```

In Aspire, point at it instead of building locally:

```csharp
builder.AddEasyAuthSimulator("auth")
    .WithUpstream(api)
    .WithEntraId(tenantId, clientId, secret)
    .WithImage("ghcr.io/mfcollins3/easyauthsimulator")
    .WithImageTag("<version>")
    .WithExternalHttpEndpoints();
```

**NuGet package** — also public, but GitHub Packages requires an authenticated pull for *every*
package it hosts, public or not (there's no anonymous-read exception the way there is for GHCR
container images). You'll need a classic personal access token with the `read:packages` scope
(fine-grained tokens aren't supported for package installs):

```bash
dotnet nuget add source https://nuget.pkg.github.com/mfcollins3/index.json \
  --name easyauthsimulator-github \
  --username <your-github-username> \
  --password <your-personal-access-token> \
  --store-password-in-clear-text
```

From a C# AppHost:

```bash
dotnet add package EasyAuthSimulator.Hosting --version <version> --source easyauthsimulator-github
```

From a TypeScript AppHost, once the source above is configured:

```bash
aspire add EasyAuthSimulator.Hosting --version <version> --source easyauthsimulator-github
```

Either way, swap in the latest version from the
[package's version history](https://github.com/mfcollins3/easyauthsimulator/pkgs/nuget/EasyAuthSimulator.Hosting).

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
| `EASYAUTH_OIDC_<NAME>_CLIENT_ID` / `_CLIENT_SECRET` | *(required per provider)* | Registers a custom OpenID Connect provider named `<NAME>` (e.g. `EASYAUTH_OIDC_DUENDE_CLIENT_ID`). `<NAME>` must be alphanumeric and can't be `AAD`. |
| `EASYAUTH_OIDC_<NAME>_AUTHORITY` | *(required, unless `_METADATA_ADDRESS` is set)* | The provider's issuer; metadata is discovered at `{authority}/.well-known/openid-configuration`. |
| `EASYAUTH_OIDC_<NAME>_METADATA_ADDRESS` | — | An explicit metadata document URL, for a provider that doesn't follow the issuer + `/.well-known/openid-configuration` convention. |
| `EASYAUTH_OIDC_<NAME>_SCOPES` | `openid;profile;email` | `;`-separated. |
| `EASYAUTH_OIDC_<NAME>_NAME_CLAIM_TYPE` / `_ROLE_CLAIM_TYPE` | `name` / `roles` | Overrides which claim in the provider's tokens backs the principal name/roles. |
| `EASYAUTH_REQUIRE_AUTHENTICATION` | `true` | Mirrors `globalValidation.requireAuthentication`. |
| `EASYAUTH_UNAUTHENTICATED_ACTION` | `RedirectToLoginPage` | `RedirectToLoginPage` \| `AllowAnonymous` \| `Return401` \| `Return403` \| `Return404` (or the `RejectWith401`/`RejectWith404` file-config spellings). |
| `EASYAUTH_EXCLUDED_PATHS` | — | `;`-separated; entries may end in `/*` for a prefix match. |
| `EASYAUTH_API_PREFIX` | `/.auth` | Relocates the `/.auth/*` surface. |
| `EASYAUTH_FORWARD_PROXY_CONVENTION` | `Standard` | `NoProxy` \| `Standard` \| `Custom`. |
| `EASYAUTH_CUSTOM_PROTO_HEADER_NAME` / `_HOST_HEADER_NAME` | — | Used when the convention above is `Custom`. |
| `EASYAUTH_FORWARD_ORIGINAL_HOST` | `true` | Forwards the original ingress Host header to your app. |
| `EASYAUTH_ALLOWED_EXTERNAL_REDIRECT_HOSTS` | — | `;`-separated hosts allowed for `post_login_redirect_uri`/`post_logout_redirect_uri` beyond the current host. |

## Adding another identity provider

If the provider speaks standard OpenID Connect, you don't need to add anything — use `WithCustomOpenIdConnect()` (see [Using a custom OpenID Connect provider](#using-a-custom-openid-connect-provider) above) instead. This section is for a provider that isn't OIDC-compliant (e.g. Facebook, GitHub, X — see [Supported identity providers](#supported-identity-providers)).

Implement `IEasyAuthProvider` (`src/EasyAuthSimulator/Providers/IEasyAuthProvider.cs`) and register it alongside its authentication handler in a `AddXxx()` extension method modeled on `EntraIdServiceCollectionExtensions.AddEntraId()`. Nothing else in the pipeline needs to change — routing, header injection, and the `/.auth/*` endpoints are all provider-agnostic.

## Known deviations from the real service

A few details of real EasyAuth are undocumented publicly and are called out with `[inferred]` comments in the source where a best-effort guess was made instead of a confirmed behavior — notably which claim backs `X-MS-CLIENT-PRINCIPAL-ID` for Entra ID, the exact `/.auth/me` schema, and the session cookie's name/chunking behavior. Not yet implemented: the `X-ZUMO-AUTH` client-directed POST login flow, and the `Origin`/`Referer` CSRF check real EasyAuth applies to cookie-authenticated POSTs.

## Tests

```bash
dotnet test
```

## Distributing the simulator

Two artifacts ship together, from the same release tag (`.github/workflows/release.yml`), always
at the same version number:

- **The `easyauthsimulator` container image** — the actual proxy, published to
  `ghcr.io/mfcollins3/easyauthsimulator` tagged with the release version (plus `latest` and
  rolling `major`/`major.minor` tags). Build and push your own with:
  ```bash
  docker build -t <your-registry>/easyauthsimulator:<version> -f src/EasyAuthSimulator/Dockerfile .
  docker push <your-registry>/easyauthsimulator:<version>
  ```
  Run this from the repo root, for the same reason noted above.
- **The `EasyAuthSimulator.Hosting` NuGet package** — the Aspire hosting integration. Its default
  image tag (see `EasyAuthSimulatorResourceBuilderExtensions`) is resolved from its own assembly
  version at runtime, so installing `EasyAuthSimulator.Hosting` version `X.Y.Z` pulls
  `ghcr.io/mfcollins3/easyauthsimulator:X.Y.Z` without any extra configuration. Pack it yourself with:
  ```bash
  dotnet pack src/EasyAuthSimulator.Hosting/EasyAuthSimulator.Hosting.csproj -c Release -o ./nupkg
  ```
  Push the resulting `.nupkg` to whatever feed you use (a local folder feed, Azure Artifacts,
  GitHub Packages, etc.) — see [`dotnet nuget push`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push).

See [Using the published packages](#using-the-published-packages) above for how consumers pull
these once published — this repo's own CI publishes both artifacts on every tagged release (see
`.github/workflows/release.yml`).

If you fork this repo or publish your own build under a different name/tag, override the default
per-AppHost with `.WithImageRegistry()` / `.WithImage()` / `.WithImageTag()` on the resource
`AddEasyAuthSimulator` returns, rather than forking the hosting package.
