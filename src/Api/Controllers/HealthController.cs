using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _config;

    public HealthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
    }

    [HttpGet("auth-precheck")]
    public IActionResult AuthPrecheck()
    {
        // Check for Authorization header or query token
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        string? token = null;

        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader.Substring("Bearer ".Length).Trim();
        }
        else if (Request.Query.ContainsKey("token"))
        {
            token = Request.Query["token"].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized();
        }

        var secret = _config["Jwt:Secret"] ?? "default-super-secret-key-32-chars-long-secure-vault!";
        var key = Encoding.UTF8.GetBytes(secret);
        if (key.Length < 32) Array.Resize(ref key, 32);

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.FromMinutes(5)
            }, out _);

            return Ok();
        }
        catch
        {
            return Unauthorized();
        }
    }
}
