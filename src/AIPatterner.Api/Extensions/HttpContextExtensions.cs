// Extension methods for accessing API key information from HttpContext
namespace AIPatterner.Api.Extensions;

using AIPatterner.Domain.Entities;

public static class HttpContextExtensions
{
    public static ApiKey? GetApiKey(this HttpContext context)
    {
        return context.Items.TryGetValue("ApiKey", out var apiKey) ? apiKey as ApiKey : null;
    }

    public static string? GetApiKeyRole(this HttpContext context)
    {
        return context.Items.TryGetValue("ApiKeyRole", out var role) ? role as string : null;
    }

    public static string? GetApiKeyPersonId(this HttpContext context)
    {
        // First check if API key personId is set
        if (context.Items.TryGetValue("ApiKeyPersonId", out var personId) && personId is string apiKeyPersonId)
        {
            return apiKeyPersonId;
        }
        
        // If not, check JWT authentication - use username as personId
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var usernameClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.Name);
            return usernameClaim?.Value;
        }
        
        return null;
    }

    public static Guid? GetApiKeyUserId(this HttpContext context)
    {
        return context.Items.TryGetValue("ApiKeyUserId", out var userId) ? userId as Guid? : null;
    }

    public static bool IsAdmin(this HttpContext context)
    {
        // Check both API key role and JWT role
        var apiKeyRole = GetApiKeyRole(context);
        if (!string.IsNullOrEmpty(apiKeyRole))
        {
            return apiKeyRole == "admin";
        }
        
        // Check JWT role
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var roleClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role);
            return roleClaim?.Value == "admin";
        }
        
        return false;
    }

    public static bool IsUser(this HttpContext context)
    {
        // Check both API key role and JWT role
        var apiKeyRole = GetApiKeyRole(context);
        if (!string.IsNullOrEmpty(apiKeyRole))
        {
            return apiKeyRole == "user";
        }
        
        // Check JWT role
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var roleClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role);
            return roleClaim?.Value == "user";
        }
        
        return false;
    }

    public static string? GetJwtUsername(this HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var usernameClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.Name);
            return usernameClaim?.Value;
        }
        return null;
    }
}

