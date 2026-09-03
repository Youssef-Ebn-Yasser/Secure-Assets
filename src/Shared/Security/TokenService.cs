using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Shared.Models;
using StackExchange.Redis;

namespace Shared.Security;

public interface ITokenService
{
    string GenerateJwtToken(User user, string jwtSecret, string issuer = "SecureMediaVault", string audience = "SecureMediaVault", int expiryHours = 24);
    string GenerateChunkToken(Guid fileId, string chunkId, TimeSpan validFor);
    Task<bool> ValidateChunkTokenAsync(Guid fileId, string chunkId, string token, long expiresUnix);
}

public class TokenService : ITokenService
{
    private readonly string _serverSecret;
    private readonly IConnectionMultiplexer? _redis;

    public TokenService(string serverSecret, IConnectionMultiplexer? redis = null)
    {
        _serverSecret = string.IsNullOrWhiteSpace(serverSecret) 
            ? "default-super-secret-key-32-chars-long-secure-vault!" 
            : serverSecret;
        _redis = redis;
    }

    public string GenerateJwtToken(User user, string jwtSecret, string issuer = "SecureMediaVault", string audience = "SecureMediaVault", int expiryHours = 24)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jwtSecret) ? _serverSecret : jwtSecret);
        if (key.Length < 32)
        {
            Array.Resize(ref key, 32);
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            }),
            Expires = DateTime.UtcNow.AddHours(expiryHours),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateChunkToken(Guid fileId, string chunkId, TimeSpan validFor)
    {
        long expiresUnix = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds();
        string rawData = $"{fileId:N}:{chunkId}:{expiresUnix}";
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_serverSecret));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        string tokenBase64 = Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        
        return $"{tokenBase64}.{expiresUnix}";
    }

    public async Task<bool> ValidateChunkTokenAsync(Guid fileId, string chunkId, string tokenString, long expiresUnix)
    {
        if (string.IsNullOrWhiteSpace(tokenString)) return false;

        // Parse token if combined format "hash.expires"
        string signature = tokenString;
        if (tokenString.Contains('.'))
        {
            var parts = tokenString.Split('.');
            signature = parts[0];
            if (long.TryParse(parts[1], out long parsedExpires))
            {
                expiresUnix = parsedExpires;
            }
        }

        // Check expiration
        long currentUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (currentUnix > expiresUnix)
        {
            return false;
        }

        // Compute expected HMAC
        string rawData = $"{fileId:N}:{chunkId}:{expiresUnix}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_serverSecret));
        byte[] expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        string expectedSignature = Convert.ToBase64String(expectedHash).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature), 
            Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return false;
        }

        // Check Redis for single-use / replay rate-limit if Redis is configured
        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                string redisKey = $"token_use:{fileId:N}:{chunkId}:{signature}";
                long count = await db.StringIncrementAsync(redisKey);
                if (count == 1)
                {
                    // Set expiration on the token key
                    await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(60));
                }
                else if (count > 5) // allow small retry window for network drops, but block widespread sharing
                {
                    return false;
                }
            }
            catch
            {
                // Fallback gracefully if Redis has a transient issue
            }
        }

        return true;
    }
}
