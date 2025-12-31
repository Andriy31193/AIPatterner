// Service implementation for LLM client (optional, can be disabled)
namespace AIPatterner.Infrastructure.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

public class LLMClient : ILLMClient
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LLMClient> _logger;
    private readonly HttpClient? _httpClient;

    public LLMClient(IConfiguration configuration, ILogger<LLMClient> logger, IHttpClientFactory? httpClientFactory = null)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory?.CreateClient("LLM");
    }

    public async Task<string?> GeneratePhraseAsync(string action, string personId, CancellationToken cancellationToken)
    {
        var enabled = _configuration.GetValue<bool>("LLM:Enabled", false);
        if (!enabled)
        {
            return $"Would you like me to {action} now?";
        }

        var endpoint = _configuration.GetValue<string>("LLM:Endpoint");
        if (string.IsNullOrEmpty(endpoint) || _httpClient == null)
        {
            _logger.LogWarning("LLM enabled but endpoint not configured, using fallback phrase");
            return $"Would you like me to {action} now?";
        }

        try
        {
            var prompt = $"Generate a natural, friendly reminder phrase for the action '{action}' for person '{personId}'. " +
                        "Keep it short (under 50 words) and conversational.";

            var request = new { prompt = prompt };
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LLMResponse>(cancellationToken: cancellationToken);
                return result?.Text;
            }

            _logger.LogWarning("LLM request failed with status {StatusCode}, using fallback phrase", response.StatusCode);
            return $"Would you like me to {action} now?";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling LLM service, using fallback phrase");
            return $"Would you like me to {action} now?";
        }
    }

    private class LLMResponse
    {
        public string? Text { get; set; }
    }
}

