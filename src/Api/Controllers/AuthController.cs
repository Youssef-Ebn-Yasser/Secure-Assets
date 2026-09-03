using System.Security.Claims;
using Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Models;
using Shared.Security;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly VaultDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;

    public AuthController(VaultDbContext db, ITokenService tokenService, IConfiguration config)
    {
        _db = db;
        _tokenService = tokenService;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        string emailLower = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == emailLower))
        {
            return Conflict(new { message = "Email is already registered." });
        }

        var user = new User
        {
            Email = emailLower,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        string jwtSecret = _config["Jwt:Secret"] ?? "default-super-secret-key-32-chars-long-secure-vault!";
        string token = _tokenService.GenerateJwtToken(user, jwtSecret);

        return Ok(new AuthResponse(token, user.Id, user.Email, user.Role.ToString()));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        string emailLower = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailLower);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        string jwtSecret = _config["Jwt:Secret"] ?? "default-super-secret-key-32-chars-long-secure-vault!";
        string token = _tokenService.GenerateJwtToken(user, jwtSecret);

        return Ok(new AuthResponse(token, user.Id, user.Email, user.Role.ToString()));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return Ok(new UserDto(user.Id, user.Email, user.Role.ToString(), user.CreatedAt));
    }
}
