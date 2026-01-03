// Mock implementation of IUserContextService for tests
namespace AIPatterner.Tests.Integration;

using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;

public class MockUserContextService : IUserContextService
{
    private readonly Guid? _userId;
    private readonly bool _isAdmin;

    public MockUserContextService(Guid? userId = null, bool isAdmin = false)
    {
        _userId = userId;
        _isAdmin = isAdmin;
    }

    public Task<Guid?> GetCurrentUserIdAsync()
    {
        return Task.FromResult(_userId);
    }

    public Task<User?> GetCurrentUserAsync()
    {
        if (!_userId.HasValue)
            return Task.FromResult<User?>(null);

        // Return a mock user
        return Task.FromResult<User?>(new User(
            "testuser",
            "test@example.com",
            "hashedpassword",
            _isAdmin ? "admin" : "user"));
    }

    public bool IsAdmin()
    {
        return _isAdmin;
    }
}

