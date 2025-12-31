// Service implementation for sending notifications/webhooks
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

public class NotificationService : INotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IExecutionHistoryService _executionHistoryService;

    public NotificationService(
        IConfiguration configuration,
        ILogger<NotificationService> logger,
        IHttpClientFactory httpClientFactory,
        IExecutionHistoryService executionHistoryService)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Notification");
        _executionHistoryService = executionHistoryService;
    }

    public async Task SendReminderAsync(
        ReminderCandidate candidate,
        ReminderDecision decision,
        CancellationToken cancellationToken)
    {
        var webhookUrl = _configuration.GetValue<string>("Notifications:WebhookUrl");
        if (string.IsNullOrEmpty(webhookUrl))
        {
            _logger.LogWarning("Webhook URL not configured, skipping notification");
            return;
        }

        var payload = new
        {
            candidateId = candidate.Id,
            personId = candidate.PersonId,
            suggestedAction = candidate.SuggestedAction,
            naturalLanguagePhrase = decision.NaturalLanguagePhrase,
            style = candidate.Style.ToString(),
            confidence = decision.ConfidenceLevel,
            reason = decision.Reason
        };

        try
        {
            var requestJson = JsonSerializer.Serialize(payload);
            var response = await _httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            // Record execution history
            await _executionHistoryService.RecordExecutionAsync(
                webhookUrl,
                requestJson,
                responseContent,
                DateTime.UtcNow,
                candidate.PersonId,
                null,
                candidate.SuggestedAction,
                candidate.Id,
                null,
                cancellationToken);

            _logger.LogInformation(
                "Sent reminder notification for candidate {CandidateId} to webhook",
                candidate.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reminder notification for candidate {CandidateId}", candidate.Id);
            
            // Record failed execution
            try
            {
                var requestJson = JsonSerializer.Serialize(payload);
                await _executionHistoryService.RecordExecutionAsync(
                    webhookUrl,
                    requestJson,
                    JsonSerializer.Serialize(new { error = ex.Message }),
                    DateTime.UtcNow,
                    candidate.PersonId,
                    null,
                    candidate.SuggestedAction,
                    candidate.Id,
                    null,
                    cancellationToken);
            }
            catch
            {
                // Ignore history recording errors
            }
            
            throw;
        }
    }
}

