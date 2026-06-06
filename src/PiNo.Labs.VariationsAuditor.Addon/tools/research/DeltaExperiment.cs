using System.Text;
using System.Text.Json;

namespace PiNo.Labs.VariationsAuditor.Research;

/// <summary>
/// EXPERIMENT 1 — Delta amplification at the persistence layer.
///
/// CMS 13 stores a variation as a delta: only the properties whose serialized value differs
/// from the source version are written. The granularity is the *property*, not the field.
/// This experiment builds representative property graphs (a ContentArea page and a Visual
/// Builder Experience composition), applies ONE nested 24-byte edit, and measures the actual
/// serialized byte size of the property that ends up in the delta.
///
/// The serialization (System.Text.Json) is a faithful stand-in for how a complex property is
/// persisted as a single opaque blob: change anything inside it and the whole blob re-writes.
/// These are REAL byte counts produced by serializing the graphs, not estimates.
/// </summary>
internal static class DeltaExperiment
{
    private const string LogicalEditFrom = "Winter Sale";
    private const string LogicalEditTo = "Winter Sale \u2014 Up to 40% off"; // +24 bytes UTF-8

    public static DeltaReport Run()
    {
        var logicalBytes = Encoding.UTF8.GetByteCount(LogicalEditTo)
                           - Encoding.UTF8.GetByteCount(LogicalEditFrom);

        // ---- Case A: standard page with an inline/local-block ContentArea --------------------
        var areaBefore = BuildContentAreaPage(LogicalEditFrom);
        var areaAfter = BuildContentAreaPage(LogicalEditTo);

        var areaRows = DiffProperties(areaBefore, areaAfter);

        // ---- Case B: Visual Builder Experience (single Composition property) -----------------
        var expBefore = BuildExperience(LogicalEditFrom);
        var expAfter = BuildExperience(LogicalEditTo);

        var expRows = DiffProperties(expBefore, expAfter);

        return new DeltaReport(logicalBytes, areaRows, expRows);
    }

    /// <summary>Byte-diff two property bags; a property is "in the delta" if its blob changed.</summary>
    private static IReadOnlyList<PropRow> DiffProperties(
        IReadOnlyDictionary<string, (string Type, object Value)> before,
        IReadOnlyDictionary<string, (string Type, object Value)> after)
    {
        var rows = new List<PropRow>();
        foreach (var (name, (type, afterValue)) in after)
        {
            var b = Serialize(before[name].Value);
            var a = Serialize(afterValue);
            var changed = !b.AsSpan().SequenceEqual(a);
            rows.Add(new PropRow(name, type, changed, changed ? a.Length : 0));
        }
        return rows;
    }

    private static byte[] Serialize(object value) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOpts));

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    // ---------------------------------------------------------------------------------------
    // Representative graphs. Shapes/sizes mirror real Foundation content: a few blocks per area,
    // a composition with two sections of rows/columns/elements each carrying settings.
    // ---------------------------------------------------------------------------------------

    private static Dictionary<string, (string, object)> BuildContentAreaPage(string heroHeadline) => new()
    {
        ["Heading"] = ("PropertyString", "Seasonal Landing Page"),
        ["MainBody"] = ("PropertyXhtmlString",
            "<p>Discover our seasonal collection with curated picks for the colder months.</p>" +
            "<p>Free shipping on orders over $50. Members get early access.</p>"),
        // A local-block ContentArea: each item carries embedded block data, not just a reference.
        ["MainContentArea"] = ("PropertyContentArea", new[]
        {
            Block(1, "HeroBlock", new { Headline = heroHeadline, SubHeading = "Limited time", CtaText = "Shop now", CtaUrl = "/winter", ImageUrl = "/img/hero-winter.jpg", Alignment = "center" }),
            Block(2, "PromoBlock", new { Title = "Members save more", Body = "Join free and save 10% extra.", Badge = "MEMBERS" }),
            Block(3, "ProductGrid", new { CategoryRef = 12345, Columns = 4, ShowPrice = true, ShowRating = true, Take = 8 }),
        }),
    };

    private static Dictionary<string, (string, object)> BuildExperience(string heroHeadline) => new()
    {
        ["Name"] = ("Metadata", "Winter Experience"),
        // The entire Visual Builder Experience is ONE complex property: Outline -> Sections -> Rows
        // -> Columns -> Elements, every element carrying its own settings blob. Representative of a
        // real landing Experience: a hero, a 3-up feature row, a story split, a product band, signup.
        ["Composition"] = ("ExperienceComposition", new
        {
            key = "comp-root",
            nodeType = "page",
            displayName = "Winter Experience",
            metadata = new { theme = "winter-2026", grid = 12, breakpoints = new[] { "sm", "md", "lg", "xl" } },
            sections = new object[]
            {
                Section("hero-section", "Hero", new object[]
                {
                    Row("hero-row", new object[]
                    {
                        Column("hero-col", 12, new object[]
                        {
                            Element("e-hero", "HeroBlock", new
                            {
                                Headline = heroHeadline,
                                SubHeading = "Limited time only — the winter edit is live",
                                CtaPrimaryText = "Shop the sale", CtaPrimaryUrl = "/winter-sale",
                                CtaSecondaryText = "Browse new in", CtaSecondaryUrl = "/new-in",
                                ImageUrl = "/img/hero-winter-2400.jpg", ImageAlt = "Model in winter coat",
                                Overlay = 0.35, Alignment = "center", VerticalAlign = "middle",
                                TextColor = "#ffffff", BackgroundColor = "#0b1f2a", MinHeight = 640,
                                PaddingTop = 96, PaddingBottom = 96, Animation = "fade-up",
                            }),
                        }),
                    }),
                }),
                Section("features-section", "Features", new object[]
                {
                    Row("features-row", new object[]
                    {
                        Column("f1", 4, new object[] { Element("e-f1", "FeatureBlock", Feature("truck", "Free shipping", "On orders over $50 across the entire winter range, delivered in 2–4 days.")) }),
                        Column("f2", 4, new object[] { Element("e-f2", "FeatureBlock", Feature("star", "Member early access", "Members shop the sale 24 hours before everyone else — join free at checkout.")) }),
                        Column("f3", 4, new object[] { Element("e-f3", "FeatureBlock", Feature("refresh", "Easy returns", "60-day no-questions returns on all seasonal items, in store or by post.")) }),
                    }),
                }),
                Section("story-section", "Story", new object[]
                {
                    Row("story-row", new object[]
                    {
                        Column("s1", 6, new object[] { Element("e-s1", "RichTextBlock", RichText(
                            "<h2>Built for the cold</h2><p>Our winter range pairs technical insulation with materials you'll actually want to wear. Every piece is tested to -15°C so the only thing you feel is warm.</p><p>From the city commute to the weekend trail, the 2026 edit is designed to layer, pack down, and last.</p>")) }),
                        Column("s2", 6, new object[] { Element("e-s2", "ImageBlock", new { ImageUrl = "/img/story-winter-1200.jpg", ImageAlt = "Winter jacket detail", Caption = "The Alpine parka, in slate", Rounded = true, Shadow = "lg" }) }),
                    }),
                }),
                Section("product-section", "Products", new object[]
                {
                    Row("product-row", new object[]
                    {
                        Column("p1", 12, new object[] { Element("e-p1", "ProductBandBlock", new
                        {
                            Title = "Winter best sellers", CategoryRef = 88231, Columns = 4, Take = 8,
                            ShowPrice = true, ShowRating = true, ShowBadge = true, Sort = "best-selling",
                            CtaText = "Shop all winter", CtaUrl = "/winter-sale/all",
                        }) }),
                    }),
                }),
                Section("newsletter-section", "Newsletter", new object[]
                {
                    Row("nl-row", new object[]
                    {
                        Column("nl1", 12, new object[] { Element("e-nl", "NewsletterBlock", new
                        {
                            Heading = "Stay in the loop", Body = "Get early access to drops and member-only prices.",
                            Placeholder = "you@example.com", ButtonText = "Sign me up", Consent = "I agree to the privacy policy.",
                            BackgroundColor = "#0b1f2a", TextColor = "#ffffff",
                        }) }),
                    }),
                }),
            },
        }),
    };

    private static object Feature(string icon, string title, string body) => new { Icon = icon, Title = title, Body = body };
    private static object RichText(string html) => new { Html = html, Container = "prose", MaxWidth = 720 };

    private static object Block(int id, string type, object settings) =>
        new { contentLink = 10000 + id, blockType = type, displayOption = "full", tag = "", inlineBlock = true, settings };

    private static object Section(string key, string name, object[] rows) => new { key, nodeType = "section", displayName = name, rows };
    private static object Row(string key, object[] cols) => new { key, nodeType = "row", columns = cols };
    private static object Column(string key, int span, object[] els) => new { key, nodeType = "column", span, elements = els };
    private static object Element(string key, string type, object settings) => new { key, nodeType = "block", blockType = type, settings };
}

internal sealed record PropRow(string Name, string Type, bool InDelta, int Bytes);

internal sealed record DeltaReport(int LogicalEditBytes, IReadOnlyList<PropRow> AreaRows, IReadOnlyList<PropRow> ExperienceRows);

