using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace PiNo.Labs.VariationsAuditor.Hosting
{
    // Serves the embedded React bundle from the addon assembly: maps "/variations-auditor" to an
    // EmbeddedFileProvider over Core.dll. Guarded by HasEmbeddedAssets so it is a safe no-op without a bundle.
    public sealed class VariationsAuditorStaticFilesStartup : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                if (VariationsAuditorAssets.HasEmbeddedAssets)
                {
                    app.Map(VariationsAuditorAssets.RequestPath, branch =>
                    {
                        branch.UseStaticFiles(new StaticFileOptions
                        {
                            FileProvider = VariationsAuditorAssets.EmbeddedFileProvider,
                            RequestPath = string.Empty,
                            OnPrepareResponse = ctx =>
                            {
                                // Vite emits content-hashed filenames, so the bundle is safe to cache immutably.
                                ctx.Context.Response.Headers["Cache-Control"] =
                                    "public, max-age=31536000, immutable";
                            },
                        });
                    });
                }

                next(app);
            };
        }
    }
}

