// API controller for user management
namespace AIPatterner.Api.Controllers;

using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using AIPatterner.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "admin")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuthService _authService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        ApplicationDbContext context,
        IAuthService authService,
        ILogger<UsersController> logger)
    {
        _context = context;
        _authService = authService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        var users = await _context.Users
            .OrderBy(u => u.CreatedAtUtc)
            .ToListAsync();

        return Ok(users.Select(u => new UserDto
        {
            Id = u.Id.ToString(),
            Username = u.Username,
            Email = u.Email,
            Role = u.Role,
            CreatedAt = u.CreatedAtUtc.ToString("O")
        }).ToList());
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username, email, and password are required" });
        }

        if (request.Password.Length < 6)
        {
            return BadRequest(new { message = "Password must be at least 6 characters long" });
        }

        // Check if username already exists
        var existingUserByUsername = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);
        if (existingUserByUsername != null)
        {
            return Conflict(new { message = "Username already exists" });
        }

        // Check if email already exists
        var existingUserByEmail = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingUserByEmail != null)
        {
            return Conflict(new { message = "Email already exists" });
        }

        try
        {
            var passwordHash = _authService.HashPassword(request.Password);
            var user = new User(request.Username, request.Email, passwordHash, request.Role ?? "user");
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User created by admin: {Username}, Role: {Role}", request.Username, user.Role);

            var dto = new UserDto
            {
                Id = user.Id.ToString(),
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAtUtc.ToString("O")
            };

            return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { message = "An error occurred while creating the user" });
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Prevent deleting the last admin
        var adminCount = await _context.Users.CountAsync(u => u.Role == "admin");
        if (user.Role == "admin" && adminCount <= 1)
        {
            return BadRequest(new { message = "Cannot delete the last admin user" });
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User deleted by admin: {Username}", user.Username);

        return NoContent();
    }
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Role { get; set; }
}


