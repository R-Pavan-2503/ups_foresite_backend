using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using System.Text.Json;

namespace CodeFamily.Api.Workers;

/// <summary>
/// Background worker that processes webhook queue.
/// This is the CORE of real-time conflict detection.
/// 
/// WORKFLOW:
/// 1. Poll webhook_queue for pending items
/// 2. Parse GitHub webhook payload
/// 3. Run incremental analysis on changed files
/// 4. Calculate risk with open PRs
/// 5. If risk ≥ 80%:
///    - Block merge via GitHub Status API
///    - Send Slack DM
///    - Publish to Supabase Realtime
/// </summary>
public class IncrementalWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<IncrementalWorker> _logger;

    public IncrementalWorker(IServiceProvider services, ILogger<IncrementalWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IncrementalWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
                var analysis = scope.ServiceProvider.GetRequiredService<IAnalysisService>();
                var github = scope.ServiceProvider.GetRequiredService<IGitHubService>();
                var slack = scope.ServiceProvider.GetRequiredService<ISlackService>();

                // Get next pending webhook
                var webhook = await db.GetNextPendingWebhook();

                if (webhook == null)
                {
                    await Task.Delay(1000, stoppingToken); // Poll every second
                    continue;
                }

                _logger.LogInformation($"Processing webhook {webhook.Id}");

                try
                {
                    await ProcessWebhook(webhook, db, analysis, github, slack);
                    await db.UpdateWebhookStatus(webhook.Id, "completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing webhook {webhook.Id}: {ex.Message}");
                    await db.UpdateWebhookStatus(webhook.Id, "failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Worker error: {ex.Message}");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("IncrementalWorker stopping");
    }
}