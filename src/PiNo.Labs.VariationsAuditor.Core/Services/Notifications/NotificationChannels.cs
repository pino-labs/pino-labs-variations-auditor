using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PiNo.Labs.VariationsAuditor.Services.Notifications
{
    // Webhook channel — posts to a Slack/Teams incoming webhook (or any JSON endpoint). Never throws.
    public sealed class WebhookNotificationChannel(
        IHttpClientFactory httpClientFactory,
        IOptions<VariationsAuditorNotificationOptions> options,
        ILogger<WebhookNotificationChannel> logger)
        : INotificationChannel
    {
        private readonly VariationsAuditorNotificationOptions _options = options.Value;

        public string Name => "Webhook";

        public bool IsEnabled => _options.Enabled && _options.Webhook.Enabled && !string.IsNullOrWhiteSpace(_options.Webhook.Url);

        public async Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return false;
            }
            try
            {
                var client = httpClientFactory.CreateClient("VariationsAuditor.Webhook");
                client.Timeout = TimeSpan.FromSeconds(10);
                var payload = BuildPayload(message);
                var response = await client.PostAsJsonAsync(_options.Webhook.Url, payload, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("VariationsAuditor webhook returned {Status} for '{Title}'.", (int)response.StatusCode, message.Title);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "VariationsAuditor webhook delivery failed for '{Title}'.", message.Title);
                return false;
            }
        }

        // Slack and Teams both accept a simple {"text": "..."} incoming-webhook body.
        private object BuildPayload(NotificationMessage message)
        {
            var prefix = message.Severity switch
            {
                NotificationSeverity.Critical => "🔴 ",
                NotificationSeverity.Warning => "🟠 ",
                _ => "🔵 ",
            };
            var text = $"{prefix}*{message.Title}*\n{message.Body}";
            if (!string.IsNullOrWhiteSpace(message.Link))
            {
                text += $"\n{message.Link}";
            }
            return new { text };
        }
    }

    // Email channel — SMTP via System.Net.Mail. Disabled unless host/from are set.
    public sealed class EmailNotificationChannel(
        IOptions<VariationsAuditorNotificationOptions> options,
        ILogger<EmailNotificationChannel> logger)
        : INotificationChannel
    {
        private readonly VariationsAuditorNotificationOptions _options = options.Value;

        public string Name => "Email";

        public bool IsEnabled =>
            _options.Enabled && _options.Email.Enabled &&
            !string.IsNullOrWhiteSpace(_options.Email.SmtpHost) &&
            !string.IsNullOrWhiteSpace(_options.Email.From);

        public async Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return false;
            }

            // Prefer the message recipient (variant owner) when it looks like an email; else the configured To.
            var to = LooksLikeEmail(message.Recipient) ? message.Recipient! : _options.Email.To;
            if (string.IsNullOrWhiteSpace(to))
            {
                return false;
            }

            try
            {
                using var mail = new MailMessage(_options.Email.From!, to)
                {
                    Subject = message.Title,
                    Body = string.IsNullOrWhiteSpace(message.Link) ? message.Body : message.Body + "\n\n" + message.Link,
                    IsBodyHtml = false,
                };
                using var smtp = new SmtpClient(_options.Email.SmtpHost, _options.Email.SmtpPort)
                {
                    EnableSsl = _options.Email.UseSsl,
                };
                if (!string.IsNullOrWhiteSpace(_options.Email.Username))
                {
                    smtp.Credentials = new NetworkCredential(_options.Email.Username, _options.Email.Password);
                }
                await smtp.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "VariationsAuditor email delivery failed for '{Title}'.", message.Title);
                return false;
            }
        }

        private static bool LooksLikeEmail(string? value)
            => !string.IsNullOrWhiteSpace(value) && value.Contains('@') && value.Contains('.');
    }

    // Always-available fallback channel: guarantees a notification is observable in the host logs.
    public sealed class LogNotificationChannel : INotificationChannel
    {
        private readonly ILogger<LogNotificationChannel> _logger;
        public LogNotificationChannel(ILogger<LogNotificationChannel> logger) => _logger = logger;

        public string Name => "Log";
        public bool IsEnabled => true;

        public Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "VariationsAuditor notification [{Severity}] {Title} — {Body} (recipient={Recipient}, link={Link})",
                message.Severity, message.Title, message.Body, message.Recipient ?? "(none)", message.Link ?? "(none)");
            return Task.FromResult(true);
        }
    }
}

