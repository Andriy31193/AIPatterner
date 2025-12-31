// Service implementation for pushing summaries to Mirix memory
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

public class MemoryGateway : IMemoryGateway
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MemoryGateway> _logger;
    private readonly HttpClient? _httpClient;

    public MemoryGateway(
        IConfiguration configuration,
        ILogger<MemoryGateway> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory?.CreateClient("Memory");
    }

    public async Task PushSummaryAsync(string summary, CancellationToken cancellationToken)
    {
        var enabled = _configuration.GetValue<bool>("Memory:Enabled", false);
        if (!enabled)
        {
            _logger.LogDebug("Memory gateway disabled, skipping summary push");
            return;
        }

        var endpoint = _configuration.GetValue<string>("Memory:Endpoint");
        if (string.IsNullOrEmpty(endpoint) || _httpClient == null)
        {
            _logger.LogWarning("Memory enabled but endpoint not configured, skipping summary push");
            return;
        }

        try
        {
            var payload = new { summary = summary };
            var response = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Pushed summary to memory gateway: {Summary}", summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push summary to memory gateway");
        }
    }
}

