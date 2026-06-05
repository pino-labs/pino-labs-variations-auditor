using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.FileProviders;

namespace PiNo.Labs.VariationsAuditor.Hosting
{
    // Resolver for the React bundle, delivered exclusively as an embedded resource inside Core.dll (the NuGet
    // addon packs and ships it). No wwwroot copy and no on-disk fallback. Vite builds into the Core project's
    // obj\frontend-dist folder, embedded under the ".EmbeddedFrontend." logical namespace.
    internal static class VariationsAuditorAssets
    {
        public const string RequestPath = "/variations-auditor";

        private const string EmbeddedMarker = ".EmbeddedFrontend.";

        private static readonly Assembly HostAssembly = typeof(VariationsAuditorAssets).Assembly;
        private static readonly string BaseNamespace = ResolveBaseNamespace(HostAssembly);

        // True only for the NuGet addon assembly, which carries the embedded bundle.
        public static bool HasEmbeddedAssets => BaseNamespace != null;

        public static IFileProvider EmbeddedFileProvider { get; } =
            BaseNamespace != null ? new EmbeddedFileProvider(HostAssembly, BaseNamespace) : null;

        // SPA entry document from the embedded bundle; null when no bundle is embedded.
        public static string ReadIndexHtml()
        {
            var file = EmbeddedFileProvider?.GetFileInfo("index.html");
            if (file is { Exists: true })
            {
                using var stream = file.CreateReadStream();
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }

            return null;
        }

        // Finds the namespace prefix the bundle was embedded under; null when nothing is embedded.
        private static string ResolveBaseNamespace(Assembly assembly)
        {
            foreach (var name in assembly.GetManifestResourceNames())
            {
                var idx = name.IndexOf(EmbeddedMarker, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    return name.Substring(0, idx + EmbeddedMarker.Length - 1);
                }
            }

            return null;
        }
    }
}

