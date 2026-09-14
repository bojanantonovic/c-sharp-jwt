namespace c_sharp_jwt.Users.Dto;

public record CurrentUserResponse(string Email, IReadOnlyList<string> Roles);
