using System.ComponentModel.DataAnnotations;

namespace c_sharp_jwt.Auth.Dto;

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public record RegisterRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(RegisterRequest.MinPasswordLength,
        ErrorMessage = "Password must be at least 8 characters long")]
    string Password)
{
    private const int MinPasswordLength = 8;
}

public record AuthResponse(string Token, string TokenType, string Email)
{
    private const string BearerTokenType = "Bearer";

    public static AuthResponse Of(string token, string email) => new(token, BearerTokenType, email);
}
