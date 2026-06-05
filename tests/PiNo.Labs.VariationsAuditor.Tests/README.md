# PiNo.Labs.VariationsAuditor.Tests
Unit and contract test suite for the **Variations Auditor** Optimizely CMS 13 add-on.
## Goals
- Protect the add-on''s public contracts (authorization gate, drift-matrix shape, discovery rule,
  embedded-UI delivery, REST surface, models) against regressions.
- Run fast and deterministically in CI - no database, no live CMS, no Optimizely Graph, no network.
- No third-party mocking framework: pure-logic units need none, and the thin controller layer is exercised
  with small hand-written in-memory fakes (`TestDoubles/`).
## What is covered
| Area | Test class |
|---|---|
| Authorization | `Security/VariationsAuditorAuthorizationTests` |
| Drift matrix | `Services/DriftMatrixServiceTests` |
| Discovery rule | `Services/GraphVariationDiscoveryFilterTests` |
| Notifications | `Configuration/NotificationOptionsTests` |
| REST surface | `Controllers/VariationsAuditorControllerTests` |
| Shell menu | `Menu/VariationsAuditorMenuProviderTests` |
| Embedded UI | `Hosting/EmbeddedUiAssetTests` |
| Promote semantics | `Models/PromoteModeTests` |
| Identity model | `Models/VariantIdentityTests` |
## Design notes
- **Single shared assembly under test.** The suite references `PiNo.Labs.VariationsAuditor.Core` - the exact
  assembly the host and the NuGet package ship - so the tests validate what is actually delivered.
- **`InternalsVisibleTo`.** One assembly-level grant in `Core.csproj` lets the suite assert the
  embedded-bundle contract and the role set without weakening production accessibility.
- **Hand-written fakes** (`TestDoubles/`) stand in for the thin orchestration dependencies; **builders**
  (`TestData/`) keep the specs readable.
## Running
```powershell
dotnet test tests/PiNo.Labs.VariationsAuditor.Tests/PiNo.Labs.VariationsAuditor.Tests.csproj -c Release
```
> The first build compiles `Core`, which embeds the React bundle. If the bundle has never been built,
> `Core.csproj` runs `npm run build` in `frontend/` automatically (Node.js required).