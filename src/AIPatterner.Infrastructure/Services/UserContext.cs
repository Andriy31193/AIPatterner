// Service for accessing current user context from HTTP request
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public interface IUserContext
{
    /// <summary>
    /// Gets the current user ID from JWT token or API key. Returns null if not authenticated.
    /// </summary>
    Guid? GetCurrentUserId();

    /// <summary>
    /// Gets the current user role. Returns null if not authenticated.
    /// </summary>
    string? GetCurrentUserRole();

    /// <summary>
    /// Gets the current user entity. Returns null if not authenticated.
    /// </summary>
    Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the current user is an admin.
    /// </summary>
    bool IsAdmin();

    /// <summary>
    /// Checks if the current user can access data for the specified user ID.
    /// Admins can access any user's data, normal users can only access their own.
    /// </summary>
    bool CanAccessUser(Guid targetUserId);
}

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _dbContext;
    private Guid? _cachedUserId;
    private string? _cachedRole;

    public UserContext(IHttpContextAccessor httpContextAccessor, ApplicationDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    public Guid? GetCurrentUserId()
    {
        if (_cachedUserId.HasValue)
            return _cachedUserId;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        // Try to get from JWT claims first
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _cachedUserId = userId;
                return userId;
            }
        }

        // Try to get from API key (stored in items by middleware)
        if (httpContext.Items.TryGetValue("ApiKeyUserId", out var apiKeyUserIdObj) && apiKeyUserIdObj is Guid apiKeyUserId)
        {
            _cachedUserId = apiKeyUserId;
            return apiKeyUserId;
        }

        return null;
    }

    public string? GetCurrentUserRole()
    {
        if (_cachedRole != null)
            return _cachedRole;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        // Try to get from JWT claims first
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            var roleClaim = httpContext.User.FindFirst(ClaimTypes.Role);
            if (roleClaim != null)
            {
                _cachedRole = roleClaim.Value;
                return _cachedRole;
            }
        }

        // Try to get from API key (stored in items by middleware)
        if (httpContext.Items.TryGetValue("ApiKeyRole", out var apiKeyRoleObj) && apiKeyRoleObj is string apiKeyRole)
        {
            _cachedRole = apiKeyRole;
            return apiKeyRole;
        }

        return null;
    }

    public async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return null;

        return await _dbContext.Users.FindAsync(new object[] { userId.Value }, cancellationToken);
    }

    public bool IsAdmin()
    {
        var role = GetCurrentUserRole();
        return role?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;
    }

    public bool CanAccessUser(Guid targetUserId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return false;

        // Admins can access any user's data
        if (IsAdmin())
            return true;

        // Normal users can only access their own data
        return currentUserId.Value == targetUserId;
    }
}

