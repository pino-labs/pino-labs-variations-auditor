# PiNo.Labs.VariationsAuditor.Addon

> ⚠️ **Experimental / preview release (`0.0.1-experimental`) — not production-ready.** APIs, behaviour and
> packaging may change without notice. Published as a NuGet pre-release.

An Optimizely CMS 13 shell add-on that audits content variations across the whole site.

## What it does

- **Site-wide inventory** of every content variation, grouped by content item, with the audience
  (visitor group) each variation targets.
- **Divergence analysis** - property-level and content-area diffs against the published master, with
  detection of *stale overrides* (a variation whose master has since moved on, so promoting it would
  silently overwrite newer master content).
- **Deliverability / orphan detection** - flags variations that exist in the CMS but are missing from the
  Optimizely Graph index.
- **Safe bulk actions** - Promote / Unpublish / Delete with a mandatory, conflict-aware dry-run preview
  and gating on version status, content locks and ACLs.
- **Sync from default** - pull the master's current value back into a stale variation.

The React UI is embedded in the assembly and served from the package; no `wwwroot` copy step is required.

## Install

```powershell
dotnet add package PiNo.Labs.VariationsAuditor.Addon --prerelease
```

Rebuild the host. The add-on self-registers as a protected shell module, so no configuration is needed:
the **Audit → Variations Auditor** menu item appears and the routes (`/ui/variations-auditor/`,
`/api/auditor/*`) work immediately.

**Target framework:** `net10.0` (Optimizely CMS 13).

## Authorization

The add-on runs in-process inside the authenticated CMS shell and inherits the host's authentication
scheme (Opti ID on DXP/PaaS, ASP.NET Identity cookie auth locally). Access is gated by a single policy
against the canonical CMS roles (`Administrators`, `CmsAdmins`, `CmsEditors`, `WebAdmins`, `WebEditors`).
No separate sign-in and no external endpoint are involved.

## Optimizely Graph (optional)

The add-on uses `IGraphContentClient` for site-wide discovery and the orphan/deliverability probe. The
client is resolved optionally, so the add-on installs and runs even when the host has not called
`AddGraphContentClient()`:

- **Discovery** falls back to an in-process content-tree walk.
- **Deliverability** returns *deliverable* rather than mislabelling variations as orphans.

For full Graph functionality, register the query client and provide Graph credentials in the host:

```csharp
services.AddContentGraph(_ => { });   // CMS 13 also needs this for Content Manager
services.AddGraphContentClient();     // the query client this add-on consumes
```

## License

Apache-2.0.

