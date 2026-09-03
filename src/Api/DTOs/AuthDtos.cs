namespace Api.DTOs;

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, Guid UserId, string Email, string Role);

public record UserDto(Guid Id, string Email, string Role, DateTime CreatedAt);
