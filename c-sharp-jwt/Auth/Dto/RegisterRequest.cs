using System.ComponentModel.DataAnnotations;
using c_sharp_jwt.Common;

namespace c_sharp_jwt.Auth.Dto;

public record RegisterRequest(
    [property: NotBlank]
    [property: EmailAddress(ErrorMessage = ValidationMessages.MustBeWellFormedEmail)]
    string Email,
    [property: NotBlank]
    [property: MinLength(RegisterRequest.MinPasswordLength, ErrorMessage = ValidationMessages.PasswordTooShort)]
    string Password)
{
    public const int MinPasswordLength = 8;
}
