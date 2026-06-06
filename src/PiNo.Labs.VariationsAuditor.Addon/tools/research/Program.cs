using System.Globalization;
using System.Text;

namespace PiNo.Labs.VariationsAuditor.Research;

internal static class Program
{
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        void Line(string s = "") { Console.WriteLine(s); sb.AppendLine(s); }

        Line("# Variations Auditor — research results");
        Line();
        Line($"_Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC on .NET {Environment.Version}, "
             + $"{Environment.OSVersion.VersionString}._");
        Line();

        // ---------------- EXPERIMENT 1: delta amplification -----------------------------------
        var delta = DeltaExperiment.Run();
        Line("## Experiment 1 — Delta amplification (measured by serialization)");
        Line();
        Line($"One nested edit applied: \"Winter Sale\" -> \"Winter Sale — Up to 40% off\" "
             + $"(**logical change = {delta.LogicalEditBytes} bytes** UTF-8).");
        Line();

        Line("### Case A — standard page with a ContentArea");
        Line();
        Line("| Property | Type | In delta? | Bytes written |");
        Line("|---|---|:--:|--:|");
        foreach (var r in delta.AreaRows)
            Line($"| `{r.Name}` | {r.Type} | {(r.InDelta ? "**yes**" : "no")} | {Fmt(r.Bytes)} |");
        var areaDelta = delta.AreaRows.Where(r => r.InDelta).Sum(r => r.Bytes);
        Line();
        Line($"**Delta total: {Fmt(areaDelta)} bytes** for a {delta.LogicalEditBytes}-byte edit "
             + $"→ **{Ratio(areaDelta, delta.LogicalEditBytes)} amplification**.");
        Line();

        Line("### Case B — Visual Builder Experience (single Composition property)");
        Line();
        Line("| Property | Type | In delta? | Bytes written |");
        Line("|---|---|:--:|--:|");
        foreach (var r in delta.ExperienceRows)
            Line($"| `{r.Name}` | {r.Type} | {(r.InDelta ? "**yes**" : "no")} | {Fmt(r.Bytes)} |");
        var expDelta = delta.ExperienceRows.Where(r => r.InDelta).Sum(r => r.Bytes);
        Line();
        Line($"**Delta total: {Fmt(expDelta)} bytes** for a {delta.LogicalEditBytes}-byte edit "
             + $"→ **{Ratio(expDelta, delta.LogicalEditBytes)} amplification**.");
        Line();

        // ---------------- EXPERIMENT 2: cache by query shape ----------------------------------
        var cache = CacheExperiment.Run();
        Line("## Experiment 2 — Cache hit-rate by query shape (discrete-event model)");
        Line();
        Line("200,000 reads over a 100-id working set, one publish per 100 reads, invalidation-only "
             + "eviction (no TTL). Latency (ms) is NOT modelled here — run `graph-cache.k6.js` against "
             + "a live gateway for p50/p99.");
        Line();
        Line("| Query shape | Variation arg | Reads | Hits | Origin fetches | Hit-rate |");
        Line("|---|---|--:|--:|--:|--:|");
        foreach (var r in cache)
            Line($"| {ShapeName(r.Shape)} | {ShapeVar(r.Shape)} | {Fmt(r.Reads)} | {Fmt(r.Hits)} "
                 + $"| {Fmt(r.OriginFetches)} | **{r.HitRate * 100:0.0}%** |");
        Line();

        Line("### Hit-rate (ASCII)");
        Line();
        Line("```text");
        foreach (var r in cache)
            Line($"{ShapeLabel(r.Shape),-16}{Bar(r.HitRate)} {r.HitRate * 100:0.0}%");
        Line("                0%        25%        50%        75%        100%");
        Line("```");
        Line();

        // ---------------- Single-publish recovery probe --------------------------------------
        var recovery = CacheExperiment.RunSinglePublishProbe();
        Line("### Invalidation cost of ONE base publish");
        Line();
        Line("Warm every key, publish a single base content, then count keys each shape must re-fetch:");
        Line();
        Line("| Query shape | Warm keys before | Keys invalidated by 1 publish |");
        Line("|---|--:|--:|");
        foreach (var r in recovery)
            Line($"| {ShapeLabel(r.Shape)} | {Fmt(r.WarmKeysBefore)} | {Fmt(r.KeysInvalidated)} |");
        Line();

        var resultsPath = Path.Combine(AppContext.BaseDirectory, "RESULTS.md");
        // Also write next to the source for convenience.
        File.WriteAllText("RESULTS.md", sb.ToString());
        Console.WriteLine();
        Console.WriteLine($"Results written to {Path.GetFullPath("RESULTS.md")} (and {resultsPath})");
    }

    private static string Fmt(long n) => n == 0 ? "—" : n.ToString("N0", CultureInfo.InvariantCulture);

    private static string Ratio(long delta, int logical) =>
        logical <= 0 ? "n/a" : $"~{Math.Round((double)delta / logical):N0}×";

    private static string Bar(double frac)
    {
        const int width = 50;
        var filled = (int)Math.Round(frac * width);
        return new string('█', filled) + new string(' ', width - filled);
    }

    private static string ShapeName(string s) => s switch
    {
        "item" => "`item(key)`",
        "item_some" => "`item(key)`",
        "items" => "`items(limit:100)`",
        "items_some" => "`items(limit:100)`",
        "items_all" => "`items(limit:100)`",
        _ => s,
    };

    private static string ShapeVar(string s) => s switch
    {
        "item" => "none (canonical)",
        "item_some" => "`SOME [key]`",
        "items" => "none",
        "items_some" => "`SOME [key]`",
        "items_all" => "`ALL` + `includeOriginal`",
        _ => "",
    };

    private static string ShapeLabel(string s) => s switch
    {
        "item" => "item",
        "item_some" => "item + SOME",
        "items" => "items",
        "items_some" => "items + SOME",
        "items_all" => "items + ALL",
        _ => s,
    };
}

