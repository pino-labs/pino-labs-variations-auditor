// DeltaProbe.cs — the LIVE counterpart to DeltaExperiment.cs.
//
// DeltaExperiment.cs measures delta amplification by serializing representative property graphs
// (no CMS required). This file is the same byte-diff run against *live* CMS 13 content: load the
// source version and the variation version, walk the property bag, and report which properties
// diverged and how many bytes each wrote into the delta.
//
// It depends on EPiServer assemblies, so it is EXCLUDED from this console harness's compilation
// (see Research.csproj: <Compile Remove="DeltaProbe.cs" />). Drop it into the add-on or any CMS 13
// project to run it for real, e.g. from a scheduled job or an /api/auditor/delta endpoint.

#if CMS13_HOST   // only compiles inside a CMS 13 host with EPiServer references
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EPiServer;
using EPiServer.Core;

namespace PiNo.Labs.VariationsAuditor.Research;

public sealed record DeltaRow(string Property, string Type, bool InDelta, int Bytes);

public sealed class DeltaProbe
{
    private readonly IContentLoader _loader;
    public DeltaProbe(IContentLoader loader) => _loader = loader;

    /// <summary>
    /// Byte-diff a variation version against its source version. A property is "in the delta" if
    /// its serialized value differs; its serialized size is what diverged.
    /// </summary>
    public IReadOnlyList<DeltaRow> Diff(ContentReference original, ContentReference variation)
    {
        var src = _loader.Get<IContent>(original);
        var var_ = _loader.Get<IContent>(variation);
        var rows = new List<DeltaRow>();

        foreach (var vp in var_.Property)
        {
            var sp = src.Property[vp.Name];
            var sb = Serialize(sp?.Value);
            var vb = Serialize(vp.Value);
            var inDelta = !sb.AsSpan().SequenceEqual(vb);
            rows.Add(new DeltaRow(vp.Name, vp.GetType().Name, inDelta, inDelta ? vb.Length : 0));
        }
        return rows;
    }

    private static byte[] Serialize(object? value) => value is null
        ? Array.Empty<byte>()
        : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOpts));

    private static readonly JsonSerializerOptions JsonOpts =
        new() { ReferenceHandler = ReferenceHandler.IgnoreCycles, WriteIndented = false };
}
#endif

