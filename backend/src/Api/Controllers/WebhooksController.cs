using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace CodeFamily.Api.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IGitHubService _github;
    private readonly IDatabaseService _db;
    private readonly ISlackService _slack;
    private readonly ILogger<WebhooksController> _logger;
    private readonly string _webhookSecret;

    public WebhooksController(
        IGitHubService github,
        IDatabaseService db,
        ISlackService slack,
        IConfiguration config,
        ILogger<WebhooksController> logger)
    {
        _github = github;
        _db = db;
        _slack = slack;
        _logger = logger;
        _webhookSecret = config.GetValue<string>("GitHub:WebhookSecret") ?? "";
    }

    [HttpPost("github")]
    public async Task<IActionResult> HandleGitHubWebhook()
    {
        try
        {
            // Read payload
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var payload = await reader.ReadToEndAsync();

            // Verify signature
            var signature = Request.Headers["X-Hub-Signature-256"].ToString();
            if (!await _github.VerifyWebhookSignature(payload, signature, _webhookSecret))
            {
                return Unauthorized(new { error = "Invalid signature" });
            }

            // Enqueue for background processing
            var webhookId = await _db.EnqueueWebhook(payload);

            _logger.LogInformation($"Webhook enqueued: {webhookId}");

            return Ok(new { message = "Webhook received", id = webhookId });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Webhook error: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
