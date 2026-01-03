// Service for extracting current user context from HTTP request (JWT token or API key)
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class UserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _context;

    public UserContextService(IHttpContextAccessor httpContextAccessor, ApplicationDbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public async Task<Guid?> GetCurrentUserIdAsync()
    {
        var user = await GetCurrentUserAsync();
        return user?.Id;
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        // Try to get user from JWT claims
        var userIdClaim = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? httpContext.User?.FindFirst("sub")?.Value
            ?? httpContext.User?.FindFirst("userId")?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return await _context.Users.FindAsync(userId);
        }

        // Try to get user from API key (stored in HttpContext.Items by middleware)
        if (httpContext.Items.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj is ApiKey apiKey)
        {
            if (apiKey.UserId.HasValue)
            {
                return await _context.Users.FindAsync(apiKey.UserId.Value);
            }
        }

        return null;
    }

    public bool IsAdmin()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return false;

        // Check JWT claims
        if (httpContext.User?.IsInRole("admin") == true)
            return true;

        // Check API key role
        if (httpContext.Items.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj is ApiKey apiKey)
        {
            return apiKey.Role == "admin";
        }

        return false;
    }
}

