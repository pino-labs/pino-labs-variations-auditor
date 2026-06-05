# PiNo.Labs.VariationsAuditor.Tests

Enterprise unit & contract test suite for the **Variations Auditor** Optimizely CMS 13 add-on.

## Goals

- Protect the add-on's **public contracts** (scoring rules, drift-matrix shape, audit-trail semantics,
  authorization gate, embedded-UI delivery) against regressions.
- Run **fast and deterministically** in CI — no database, no live CMS, no Optimizely Graph, no network.
- Stay **licence-clean** for corporate use: only MIT/Apache-licensed test tooling, **no** third-party
  mocking framework (and therefore no SponsorLink/telemetry concerns).

## What is covered

| Area | Test class | What it pins |
|---|---|---|
| Authorization | `Security/VariationsAuditorAuthorizationTests` | Policy name, `RequireAuthenticatedUser`, the 5 canonical CMS roles |
| Health scoring (2.5) | `Services/VariationHealthScorerTests` | Stale/orphan/freshness weighting, grade boundaries, worst-first ordering |
| Drift matrix (2.3) | `Services/DriftMatrixServiceTests` | Dense grid densification, default-column ordering, audience headers |
| Audit trail (2.4) | `Services/InMemoryAuditTrailTests` | Newest-first, filters, capacity bound, null-safety |
| Notifications (2.1) | `Configuration/NotificationOptionsTests` | Safe-by-default values, appsettings binding |
| REST surface | `Controllers/VariationsAuditorControllerTests` | Delegation, editor threading, 200 OK payload shaping |
| Shell menu | `Menu/VariationsAuditorMenuProviderTests` | Section + URL item, shell route, availability gate |
| Embedded UI | `Hosting/EmbeddedUiAssetTests` | The React bundle is embedded in `Core.dll` (same invariant `pack-addon.ps1` checks) |
| Models | `Models/VariantIdentityTests` | Tuple value-equality, paging math, bulk-result aggregation |

## Design notes

- **Single shared assembly under test.** The suite references
  `PiNo.Labs.VariationsAuditor.Core` — the *exact* assembly the host and the NuGet package ship — so the
  tests validate what is actually delivered.
- **`InternalsVisibleTo`.** One assembly-level grant in `Core.csproj` lets the suite assert the
  embedded-bundle contract (`VariationsAuditorAssets`) and the role array (`AllowedRoles`) without
  weakening production accessibility.
- **Hand-written fakes** (`TestDoubles/`) stand in for the thin orchestration dependencies. The
  deterministic, pure-logic units need no test doubles at all.
- **Builders** (`TestData/AuditDtoBuilder`) make the behavioural specs read as English.

## Running

```powershell
# From the repo root
dotnet test tests/PiNo.Labs.VariationsAuditor.Tests/PiNo.Labs.VariationsAuditor.Tests.csproj -c Release

# With coverage (coverlet collector is referenced)
dotnet test tests/PiNo.Labs.VariationsAuditor.Tests/PiNo.Labs.VariationsAuditor.Tests.csproj `
  --collect:"XPlat Code Coverage"
```

> The first build compiles `Core`, which embeds the React bundle. If the bundle has never been built,
> `Core.csproj` runs `npm run build` in `frontend/` automatically (Node.js required), exactly like
> `pack-addon.ps1`.

