# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-09-20

### Added

- Custom OpenID Connect provider support (`WithCustomOpenIdConnect()` / `AddCustomOpenIdConnect()`), so any spec-compliant OIDC identity provider (Google, Apple, Auth0, Okta, Duende IdentityServer, ...) can be wired up alongside or instead of Entra ID — discovered via its `{authority}/.well-known/openid-configuration` metadata document, no code required. Each provider signs in at `/.auth/login/<name>`.
- Standalone configuration for custom OpenID Connect providers via `EASYAUTH_OIDC_<NAME>_CLIENT_ID` / `_CLIENT_SECRET` / `_AUTHORITY` / `_METADATA_ADDRESS` / `_SCOPES` / `_NAME_CLAIM_TYPE` / `_ROLE_CLAIM_TYPE` environment variables.
- `EasyAuthSimulatorOptions.AuthenticationRequired` and `DefaultProvider`: opt in to requiring sign-in before a request reaches the app, with the default provider auto-inferred when exactly one is configured (and a clear startup error when it's ambiguous).
- README: a supported-identity-providers table comparing this project to real Azure Container Apps EasyAuth, a custom OpenID Connect usage sample, and instructions for consuming the published container image (GHCR) and NuGet package (GitHub Packages) as an end user.

### Changed

- **Breaking:** `AddEasyAuthSimulator(builder, name, port)` now takes an `EasyAuthSimulatorOptions` object instead of a bare `port` parameter: `AddEasyAuthSimulator(builder, name, options)`. `Port` moved onto that object and still defaults to `8080`.

### Fixed

- `AddEntraId()` no longer requires `EASYAUTH_AAD_TENANT_ID` / `EASYAUTH_AAD_CLIENT_ID` to be set when Entra ID isn't used. It's now a no-op when neither is configured (matching how custom OpenID Connect providers already behaved), instead of crashing the app at startup with an `OptionsValidationException`.
- Corrected the README's Docker build instructions, which didn't reflect that the image must be built from the repository root.

## [0.1.1] - 2026-09-19

### Added

- Multi-architecture (amd64/arm64) container image builds.

## [0.1.0] - 2026-09-18

### Added

- Initial release: a local stand-in for Azure Container Apps' built-in authentication ("EasyAuth"), exposing the same `X-MS-CLIENT-PRINCIPAL*` / `X-MS-TOKEN-*` headers and `/.auth/*` endpoints as the real service.
- Microsoft Entra ID sign-in support.
- `EasyAuthSimulator.Hosting`: an Aspire hosting integration (`AddEasyAuthSimulator`, `WithUpstream`, `WithEntraId`) usable from both C# and TypeScript AppHosts.
- CI/CD pipeline: build and test on every pull request; on a tagged release, publish the container image to GitHub Container Registry and the `EasyAuthSimulator.Hosting` NuGet package to GitHub Packages.

[Unreleased]: https://github.com/mfcollins3/easyauthsimulator/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/mfcollins3/easyauthsimulator/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/mfcollins3/easyauthsimulator/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/mfcollins3/easyauthsimulator/releases/tag/v0.1.0
