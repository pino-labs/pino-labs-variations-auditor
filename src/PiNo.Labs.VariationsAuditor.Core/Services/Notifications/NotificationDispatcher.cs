using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PiNo.Labs.VariationsAuditor.Models;

namespace PiNo.Labs.VariationsAuditor.Services.Notifications
{

    // Fans a message out to every enabled channel concurrently. A failing channel never blocks the others and
    // never throws into the caller (a content publish must not fail because Slack is down).
    public sealed class NotificationDispatcher : INotificationDispatcher
    {
        private readonly IReadOnlyList<INotificationChannel> _channels;
        private readonly VariationsAuditorNotificationOptions _options;
        private readonly ILogger<NotificationDispatcher> _logger;

        public NotificationDispatcher(
            IEnumerable<INotificationChannel> channels,
            IOptions<VariationsAuditorNotificationOptions> options,
            ILogger<NotificationDispatcher> logger)
        {
            _channels = channels.ToList();
            _options = options.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> DispatchAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled || message == null)
            {
                return Array.Empty<string>();
            }

            var enabled = _channels.Where(c => c.IsEnabled).ToList();
            if (enabled.Count == 0)
            {
                return Array.Empty<string>();
            }

            var tasks = enabled.Select(async channel =>
            {
                try
                {
                    var ok = await channel.SendAsync(message, cancellationToken).ConfigureAwait(false);
                    return (channel.Name, ok);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "VariationsAuditor notification channel '{Channel}' threw.", channel.Name);
                    return (channel.Name, false);
                }
            });

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            return results.Where(r => r.Item2).Select(r => r.Item1).ToList();
        }
    }
}

