namespace c_sharp_jwt.Auth.Dto;

public record AuthResponse(string Token, string TokenType, string Email)
{
    public const string BearerTokenType = "Bearer";

    public static AuthResponse Of(string token, string email) => new(token, BearerTokenType, email);
}
