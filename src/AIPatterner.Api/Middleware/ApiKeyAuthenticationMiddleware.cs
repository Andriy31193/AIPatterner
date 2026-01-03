// Middleware for API key authentication
namespace AIPatterner.Api;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<ApiKeyAuthenticationMiddleware> logger,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;
        
        // Allow OPTIONS requests (CORS preflight)
        if (method == "OPTIONS")
        {
            await _next(context);
            return;
        }
        
        // Skip authentication for health checks, swagger, and public auth endpoints
        if (path.StartsWith("/health") || 
            path.StartsWith("/ready") || 
            path.StartsWith("/swagger") ||
            path.StartsWith("/api/v1/auth"))
        {
            await _next(context);
            return;
        }

        // Allow requests that are already authenticated via JWT
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-API-Key", out StringValues providedKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("API key required");
            return;
        }

        // Check API key from database
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();

        var keyHash = apiKeyService.HashApiKey(providedKey.ToString());
        var now = DateTime.UtcNow;
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k =>
                k.KeyHash == keyHash &&
                k.IsActive &&
                (!k.ExpiresAtUtc.HasValue || k.ExpiresAtUtc > now));

        if (apiKey == null)
        {
            // Fallback to config-based keys for backward compatibility
            var configApiKey = _configuration.GetValue<string>("ApiKeys:Default");
            var configAdminApiKey = _configuration.GetValue<string>("ApiKeys:Admin");

            if (string.IsNullOrEmpty(configApiKey))
            {
                _logger.LogWarning("API key not found in database and not configured, denying request");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid API key");
                return;
            }

            var isConfigAdmin = path.StartsWith("/api/v1/admin") && providedKey == configAdminApiKey;
            var isConfigAuthorized = providedKey == configApiKey || isConfigAdmin;

            if (!isConfigAuthorized)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid API key");
                return;
            }

            await _next(context);
            return;
        }

        // Update last used timestamp
        apiKey.UpdateLastUsed();
        await dbContext.SaveChangesAsync();

        // Store API key in HttpContext.Items for UserContextService
        context.Items["ApiKey"] = apiKey;

        // Check if admin endpoint requires admin role
        var requiresAdmin = path.StartsWith("/api/v1/admin");
        if (requiresAdmin && apiKey.Role != "admin")
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Admin access required");
            return;
        }

        await _next(context);
    }
}

