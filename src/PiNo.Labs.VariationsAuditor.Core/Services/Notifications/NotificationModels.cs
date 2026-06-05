using PiNo.Labs.VariationsAuditor.Models;

namespace PiNo.Labs.VariationsAuditor.Services.Notifications
{
    // Proactive stale-notification options, bound from "VariationsAuditor:Notifications". Everything is OFF by
    // default: a fresh install never sends anything until an operator opts in; channels with missing config
    // self-disable.
    public sealed class VariationsAuditorNotificationOptions
    {
        public const string SectionName = "VariationsAuditor:Notifications";

        // Master switch. When false, the dispatcher and the publish-drift detector are inert.
        public bool Enabled { get; set; }

        // Only notify when at least this many properties became stale (debounce trivial edits).
        public int MinimumStaleProperties { get; set; } = 1;

        public bool NotifyOnPublishDrift { get; set; } = true;

        public WebhookOptions Webhook { get; set; } = new();
        public EmailOptions Email { get; set; } = new();
        public CmsChannelOptions Cms { get; set; } = new();

        public sealed class WebhookOptions
        {
            public bool Enabled { get; set; }
            public string? Url { get; set; }
            public string Flavor { get; set; } = "Slack";
        }

        public sealed class EmailOptions
        {
            public bool Enabled { get; set; }
            public string? SmtpHost { get; set; }
            public int SmtpPort { get; set; } = 587;
            public bool UseSsl { get; set; } = true;
            public string? From { get; set; }
            public string? To { get; set; }
            public string? Username { get; set; }
            public string? Password { get; set; }
        }

        public sealed class CmsChannelOptions
        {
            public bool Enabled { get; set; } = true;
        }
    }

    public sealed class NotificationMessage
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Recipient { get; set; }
        public string? Link { get; set; }
        public VariantIdentity? Subject { get; set; }
        public NotificationSeverity Severity { get; set; } = NotificationSeverity.Warning;
    }

    public enum NotificationSeverity { Info = 0, Warning = 1, Critical = 2 }

    public interface INotificationChannel
    {
        string Name { get; }
        bool IsEnabled { get; }
        Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    }

    public interface INotificationDispatcher
    {
        // Returns the names of the channels that accepted the message. Never throws.
        Task<IReadOnlyList<string>> DispatchAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    }
}

