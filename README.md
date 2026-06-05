# Variations Auditor for Optimizely CMS 13

> ⚠️ **Experimental / preview release (`0.0.1-experimental`).** This package is **not production-ready**.
> APIs, behaviour and packaging may change without notice. Published as a NuGet pre-release — include
> pre-release versions to discover it. Use at your own risk and pin the exact version in production hosts.

A native Optimizely CMS 13 shell add-on that audits content variations across the whole site: it lists
every variation, surfaces structural divergence and stale-override reversion risk, and performs safe,
previewed bulk actions (Promote / Unpublish / Delete) with workflow, lock and ACL gating.

The React UI is embedded in the assembly and served from the NuGet package — there is no `wwwroot` copy
step and no configuration in the consuming site.

## Install

```powershell
dotnet add package PiNo.Labs.VariationsAuditor.Addon --prerelease
```

Rebuild the host, then open **Audit → Variations Auditor** in the CMS shell.

## Features

- Site-wide inventory of every content variation, grouped by content and resolved to a human-readable
  audience (visitor group).
- Property-level and content-area divergence against the published master, including stale-override
  detection.
- Optimizely Graph deliverability / orphan detection (optional; degrades gracefully when Graph is absent).
- Safe bulk Promote / Unpublish / Delete with a mandatory, conflict-aware dry-run preview and gating on
  version status, content locks and ACLs.
- "Sync from default" to pull the master's current value back into a stale variation.

## Repository layout

```
frontend/                              React + Vite + Tailwind shell-module UI
src/PiNo.Labs.VariationsAuditor.Core/  Runtime assembly: services, controllers, views, embedded UI
src/PiNo.Labs.VariationsAuditor.Addon/ NuGet packaging project (bundles Core into the package)
tests/PiNo.Labs.VariationsAuditor.Tests/  xUnit unit & contract tests
.github/workflows/build.yml            CI: build, test, pack, publish
```

## Build & pack

Requires the .NET 10 SDK and Node.js.

```powershell
# Builds the React bundle, packs the NuGet package, and verifies the embedded UI.
./src/PiNo.Labs.VariationsAuditor.Addon/pack.ps1
```

The package is written to `artifacts/nuget/`.

## Test

```powershell
dotnet test PiNo.Labs.VariationsAuditor.sln -c Release
```

## Continuous integration

`.github/workflows/build.yml` builds the frontend bundle, runs the tests, packs and verifies the NuGet
package, and uploads it as a build artifact on every push and pull request. Pushing a `v*` tag (e.g.
`v1.0.0`) additionally publishes the package: configure the `NUGET_API_KEY` secret and, optionally, a
`NUGET_FEED_URL` repository variable (defaults to nuget.org).

## Requirements

- Optimizely CMS 13 (`net10.0`)
- Optimizely Commerce is not required.
- Optimizely Graph is optional (see the package README for graceful-degradation details).

## License

Apache-2.0 — see [LICENSE](LICENSE).

