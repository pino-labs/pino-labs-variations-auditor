using System;
using System.Linq;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.OptimizelyIdentity;
using EPiServer.ServiceLocation;
using EPiServer.Shell.Modules;
using PiNo.Labs.VariationsAuditor.Hosting;
using PiNo.Labs.VariationsAuditor.Security;
using PiNo.Labs.VariationsAuditor.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
namespace PiNo.Labs.VariationsAuditor.Initialization
{
    // Registers the Variations Auditor services in the EPiServer IoC. All EPiServer dependencies are provided
    // by the platform / AddGraphContentClient().
    [InitializableModule]
    [ModuleDependency(typeof(EPiServer.Web.InitializationModule))]
    public sealed class VariationsAuditorInitialization : IConfigurableModule
    {
        // Must match the protected-module folder name shipped by the addon NuGet.
        internal const string ProtectedModuleName = "PiNo.Labs.VariationsAuditor";

        public void ConfigureContainer(ServiceConfigurationContext context)
        {
            var services = context.Services;
            services.AddMemoryCache();
            services.AddScoped<ICurrentEditorAccessor, CurrentEditorAccessor>();
            services.AddScoped<IDivergenceEngine, DivergenceEngine>();
            services.AddScoped<IVariationDiscoveryService, GraphVariationDiscoveryService>();
            services.AddScoped<IGraphDeliverabilityService, GraphDeliverabilityService>();
            services.AddScoped<IVariationEditUrlResolver, VariationEditUrlResolver>();
            services.AddScoped<IVariationAudienceResolver, VisitorGroupAudienceResolver>();
            services.AddScoped<IContentVariationService, ContentVariationService>();
            services.AddScoped<IDriftMatrixService, DriftMatrixService>();

            // Proactive stale notifications. Options bind from "VariationsAuditor:Notifications"; everything is
            // off until the host opts in, and channels self-disable when unconfigured.
            services.AddOptions<Services.Notifications.VariationsAuditorNotificationOptions>()
                .BindConfiguration(Services.Notifications.VariationsAuditorNotificationOptions.SectionName);
            services.AddHttpClient(); // backs the webhook channel via IHttpClientFactory
            services.AddSingleton<Services.Notifications.INotificationChannel, Services.Notifications.WebhookNotificationChannel>();
            services.AddSingleton<Services.Notifications.INotificationChannel, Services.Notifications.EmailNotificationChannel>();
            services.AddSingleton<Services.Notifications.INotificationChannel, Services.Notifications.LogNotificationChannel>();
            services.AddSingleton<Services.Notifications.INotificationDispatcher, Services.Notifications.NotificationDispatcher>();

            // The controllers/MenuProvider/IStartupFilter live in THIS addon DLL, which ASP.NET Core's default
            // part discovery is not guaranteed to scan — register it explicitly (guarded against duplicates so
            // the Foundation host, which auto-discovers it, does not get AmbiguousMatchException at routing).
            RegisterApplicationPart(services);

            // The addon relies on the host's authentication scheme and authorizes against the CMS roles.
            services.AddVariationsAuditorAuthorization();

            // Opti ID's scheme provider only authenticates CMS UI/shell paths, so register the module's paths in
            // AdditionalPaths to make Opti ID authenticate the /api/auditor REST calls too (else AJAX gets 401).
            // No-op when Opti ID is not in use.
            services.Configure<OptimizelyIdentityOptions>(o =>
            {
                foreach (var path in new[] { "/api/auditor", "/ui/variations-auditor" })
                {
                    var ps = new PathString(path);
                    if (!o.AdditionalPaths.Contains(ps))
                    {
                        o.AdditionalPaths.Add(ps);
                    }
                }
            });

            // Self-register the protected shell module so the host does not have to touch ProtectedModuleOptions.
            services.Configure<ProtectedModuleOptions>(options =>
            {
                if (!options.Items.Any(m =>
                        string.Equals(m.Name, ProtectedModuleName, StringComparison.OrdinalIgnoreCase)))
                {
                    options.Items.Add(new ModuleDetails { Name = ProtectedModuleName });
                }
            });

            // Serve the embedded UI bundle from the addon assembly (NuGet install). No-op in the Foundation host.
            services.AddSingleton<IStartupFilter, VariationsAuditorStaticFilesStartup>();
        }
        public void Initialize(InitializationEngine context) { }
        public void Uninitialize(InitializationEngine context) { }

        // Make MVC aware of the controllers AND the compiled Razor views shipped in this addon assembly,
        // idempotently. Uses the assembly's declared application-part factory so the Razor Class Library's
        // compiled views (not just controllers) are registered; guarded against duplicates to avoid the
        // Foundation host surfacing every controller twice (AmbiguousMatchException at routing).
        private static void RegisterApplicationPart(IServiceCollection services)
        {
            var assembly = typeof(VariationsAuditorInitialization).Assembly;
            var partManager = services.AddMvc().PartManager;
            var factory = ApplicationPartFactory.GetApplicationPartFactory(assembly);
            foreach (var part in factory.GetApplicationParts(assembly))
            {
                var alreadyPresent = partManager.ApplicationParts
                    .Any(p => p.GetType() == part.GetType()
                              && string.Equals(p.Name, part.Name, StringComparison.OrdinalIgnoreCase));

                if (!alreadyPresent)
                {
                    partManager.ApplicationParts.Add(part);
                }
            }
        }
    }
}