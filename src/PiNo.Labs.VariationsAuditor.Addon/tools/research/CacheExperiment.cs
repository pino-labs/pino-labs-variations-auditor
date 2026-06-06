namespace PiNo.Labs.VariationsAuditor.Research;

/// <summary>
/// EXPERIMENT 2 — Cache behaviour by query shape.
///
/// A discrete-event model of Optimizely Graph's documented caching rule: a result is cached
/// under a key derived from the query, and a write invalidates every cached key whose result
/// *could* change. A narrow item(id) key covers one document; a broad items(...) listing key
/// covers every document it could return; opting into variations widens that coverage to the
/// variant space as well.
///
/// This is a MODEL (one tick == one read op), so it yields real, reproducible hit-rates and
/// origin-fetch counts that follow directly from the invalidation rules. It does NOT model
/// network latency — p50/p99 in milliseconds require running the shipped k6 script against a
/// live gateway (see graph-cache.k6.js). The hit-rate cliff is the part that is intrinsic to
/// the cache-key math, and that is what this measures.
/// </summary>
internal static class CacheExperiment
{
    // Dataset shape mirrors the article: 5,000 pages, 500 varied x 3 variants = 1,500 variants.
    // The workload exercises a fixed 100-id working set (what a dashboard/listing actually pages).
    private const int WorkingSet = 100;
    private const int VariedInWorkingSet = 40;   // of the 100, these carry variants
    private const int Reads = 200_000;
    private const int PublishEvery = 100;        // one publish per 100 reads
    private const int Seed = 1337;               // deterministic

    // Key fragmentation per shape — the decisive variable. A narrow item lookup has ONE key per id.
    // A broad listing fragments across filter/sort/page combinations a UI emits; opting into
    // variations adds more query dimensions, so SOME and especially ALL fragment further. Each
    // distinct key is therefore reused less often, so an invalidation is more likely to land
    // between two reads of the same key — which is exactly why broad queries cache worse.
    private const int ListingKeys = 150;
    private const int ListingKeysSome = 300;
    private const int ListingKeysAll = 600;

    private static readonly string[] Shapes =
        { "item", "item_some", "items", "items_some", "items_all" };

    public static IReadOnlyList<CacheRow> Run()
    {
        var rng = new Random(Seed);

        // Per-shape cache as a set of currently-warm keys (no TTL: invalidation is the only eviction,
        // so the result isolates the cache-key math the docs describe).
        var caches = Shapes.ToDictionary(s => s, _ => new HashSet<string>());
        var hits = Shapes.ToDictionary(s => s, _ => 0L);
        var misses = Shapes.ToDictionary(s => s, _ => 0L);

        for (var i = 0; i < Reads; i++)
        {
            var id = rng.Next(WorkingSet);

            Read(caches["item"], $"item:{id}", hits, misses, "item");
            Read(caches["item_some"], $"itemSome:{id}", hits, misses, "item_some");
            Read(caches["items"], $"items:{rng.Next(ListingKeys)}", hits, misses, "items");
            Read(caches["items_some"], $"itemsSome:{rng.Next(ListingKeysSome)}", hits, misses, "items_some");
            Read(caches["items_all"], $"itemsAll:{rng.Next(ListingKeysAll)}", hits, misses, "items_all");

            if (i % PublishEvery == 0 && i > 0)
            {
                var pubId = rng.Next(WorkingSet);
                var varied = pubId < VariedInWorkingSet;
                var isVariant = varied && rng.NextDouble() < 0.5;
                var isWinterCampaign = isVariant && rng.NextDouble() < 0.5;
                Invalidate(caches, pubId, isVariant, isWinterCampaign);
            }
        }

        var rows = new List<CacheRow>();
        foreach (var shape in Shapes)
        {
            var total = hits[shape] + misses[shape];
            rows.Add(new CacheRow(shape, total, hits[shape], misses[shape], (double)hits[shape] / total));
        }
        return rows;
    }

    private static void Read(HashSet<string> cache, string key,
        Dictionary<string, long> hits, Dictionary<string, long> misses, string shape)
    {
        if (cache.Contains(key)) hits[shape]++;
        else { misses[shape]++; cache.Add(key); }   // miss == origin fetch, then warm
    }

    /// <summary>
    /// Invalidate every cached key whose result could change because content <paramref name="pubId"/>
    /// (or one of its variants) was published. Listing keys all cover the working set, so any
    /// qualifying publish clears the whole listing family; item keys clear only their own id.
    /// </summary>
    private static void Invalidate(Dictionary<string, HashSet<string>> caches,
        int pubId, bool isVariant, bool isWinterCampaign)
    {
        // item (canonical): only a base publish of that exact id.
        if (!isVariant) caches["item"].Remove($"item:{pubId}");

        // item + SOME[WinterCampaign]: base publish of that id, or its WinterCampaign variant.
        if (!isVariant || isWinterCampaign) caches["item_some"].Remove($"itemSome:{pubId}");

        // items: base publishes only — clears the whole listing family.
        if (!isVariant) caches["items"].Clear();

        // items + SOME: base publishes, or WinterCampaign-variant publishes.
        if (!isVariant || isWinterCampaign) caches["items_some"].Clear();

        // items + ALL: base publishes, or ANY variant publish anywhere in the page.
        caches["items_all"].Clear();
    }

    /// <summary>
    /// Focused probe: warm every key, then perform ONE base publish of an id in the working set,
    /// and report how many distinct keys each shape must re-fetch from origin to recover.
    /// </summary>
    public static IReadOnlyList<RecoveryRow> RunSinglePublishProbe()
    {
        var caches = Shapes.ToDictionary(s => s, _ => new HashSet<string>());
        // Warm everything.
        for (var id = 0; id < WorkingSet; id++)
        {
            caches["item"].Add($"item:{id}");
            caches["item_some"].Add($"itemSome:{id}");
        }
        for (var k = 0; k < ListingKeys; k++) caches["items"].Add($"items:{k}");
        for (var k = 0; k < ListingKeysSome; k++) caches["items_some"].Add($"itemsSome:{k}");
        for (var k = 0; k < ListingKeysAll; k++) caches["items_all"].Add($"itemsAll:{k}");

        var before = Shapes.ToDictionary(s => s, s => caches[s].Count);

        // One base publish of a single working-set id.
        Invalidate(caches, pubId: 7, isVariant: false, isWinterCampaign: false);

        var rows = new List<RecoveryRow>();
        foreach (var s in Shapes)
            rows.Add(new RecoveryRow(s, before[s], before[s] - caches[s].Count));
        return rows;
    }
}

internal sealed record CacheRow(string Shape, long Reads, long Hits, long OriginFetches, double HitRate);

internal sealed record RecoveryRow(string Shape, int WarmKeysBefore, int KeysInvalidated);


