// Authentication controller for login and registration
using AIPatterner.Domain.Entities;
using AIPatterner.Infrastructure.Persistence;
using AIPatterner.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIPatterner.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IAuthService _authService;

    public AuthController(
        ILogger<AuthController> logger,
        ApplicationDbContext context,
        IAuthService authService)
    {
        _logger = logger;
        _context = context;
        _authService = authService;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        _logger.LogInformation("Registration attempt for username: {Username}", request.Username);
        
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Username) || 
            string.IsNullOrWhiteSpace(request.Email) || 
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username, email, and password are required" });
        }

        // Validate password length
        if (request.Password.Length < 6)
        {
            return BadRequest(new { message = "Password must be at least 6 characters long" });
        }

        // Validate email format
        if (!request.Email.Contains("@") || !request.Email.Contains("."))
        {
            return BadRequest(new { message = "Invalid email format" });
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
            // Check if this is the first user - make them admin
            var isFirstUser = !await _context.Users.AnyAsync();
            var userRole = isFirstUser ? "admin" : "user";

            // Hash password
            var passwordHash = _authService.HashPassword(request.Password);

            // Create user
            var user = new User(request.Username, request.Email, passwordHash, userRole);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            if (isFirstUser)
            {
                _logger.LogInformation("First user registered - automatically assigned admin role: {Username}", request.Username);
            }

            // Generate JWT token
            var token = _authService.GenerateJwtToken(user);

            _logger.LogInformation("User registered successfully: {Username}", request.Username);

            return Ok(new
            {
                token = token,
                user = new
                {
                    id = user.Id.ToString(),
                    username = user.Username,
                    email = user.Email,
                    role = user.Role
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration");
            return StatusCode(500, new { message = "An error occurred during registration" });
        }
    }

    /// <summary>
    /// Login with username/email and password
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for username: {Username}", request.Username);

        // Validate request
        if (string.IsNullOrWhiteSpace(request.Username) || 
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required" });
        }

        try
        {
            // Find user by username or email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Username);

            if (user == null)
            {
                _logger.LogWarning("Login failed: User not found - {Username}", request.Username);
                return Unauthorized(new { message = "Invalid username or password" });
            }

            // Verify password
            if (!_authService.VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: Invalid password for user - {Username}", request.Username);
                return Unauthorized(new { message = "Invalid username or password" });
            }

            // Generate JWT token
            var token = _authService.GenerateJwtToken(user);

            _logger.LogInformation("User logged in successfully: {Username}", request.Username);

            return Ok(new
            {
                token = token,
                user = new
                {
                    id = user.Id.ToString(),
                    username = user.Username,
                    email = user.Email,
                    role = user.Role
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }
}

// Request DTOs
public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

