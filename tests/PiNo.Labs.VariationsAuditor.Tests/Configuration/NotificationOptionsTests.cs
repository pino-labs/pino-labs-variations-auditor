using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PiNo.Labs.VariationsAuditor.Services.Notifications;
using Xunit;

namespace PiNo.Labs.VariationsAuditor.Tests.Configuration
{
    // Notifications are OFF by default; pins the safe defaults and the appsettings binding shape.
    public sealed class NotificationOptionsTests
    {
        [Fact]
        public void Section_name_is_stable()
        {
            Assert.Equal("VariationsAuditor:Notifications", VariationsAuditorNotificationOptions.SectionName);
        }

        [Fact]
        public void Fresh_options_are_safe_by_default()
        {
            var options = new VariationsAuditorNotificationOptions();

            Assert.False(options.Enabled);                 // master switch off
            Assert.Equal(1, options.MinimumStaleProperties);
            Assert.True(options.NotifyOnPublishDrift);
            Assert.False(options.Webhook.Enabled);
            Assert.Equal("Slack", options.Webhook.Flavor);
            Assert.False(options.Email.Enabled);
            Assert.Equal(587, options.Email.SmtpPort);
            Assert.True(options.Email.UseSsl);
            Assert.True(options.Cms.Enabled);              // in-product channel on once master switch flips
        }

        [Fact]
        public void Options_bind_from_host_appsettings_shape()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["VariationsAuditor:Notifications:Enabled"] = "true",
                    ["VariationsAuditor:Notifications:MinimumStaleProperties"] = "3",
                    ["VariationsAuditor:Notifications:Webhook:Enabled"] = "true",
                    ["VariationsAuditor:Notifications:Webhook:Url"] = "https://hooks.slack.com/services/x",
                    ["VariationsAuditor:Notifications:Webhook:Flavor"] = "Teams",
                    ["VariationsAuditor:Notifications:Email:From"] = "cms@acme.com",
                    ["VariationsAuditor:Notifications:Email:SmtpPort"] = "25",
                })
                .Build();

            var services = new ServiceCollection();
            services.Configure<VariationsAuditorNotificationOptions>(
                config.GetSection(VariationsAuditorNotificationOptions.SectionName));
            var options = services.BuildServiceProvider()
                .GetRequiredService<IOptions<VariationsAuditorNotificationOptions>>().Value;

            Assert.True(options.Enabled);
            Assert.Equal(3, options.MinimumStaleProperties);
            Assert.True(options.Webhook.Enabled);
            Assert.Equal("Teams", options.Webhook.Flavor);
            Assert.Equal("https://hooks.slack.com/services/x", options.Webhook.Url);
            Assert.Equal("cms@acme.com", options.Email.From);
            Assert.Equal(25, options.Email.SmtpPort);
        }
    }
}

