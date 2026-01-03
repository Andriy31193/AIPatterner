// Interface for user context service
namespace AIPatterner.Application.Services;

using AIPatterner.Domain.Entities;

public interface IUserContextService
{
    Task<Guid?> GetCurrentUserIdAsync();
    Task<User?> GetCurrentUserAsync();
    bool IsAdmin();
}

