using System.ComponentModel.DataAnnotations;
using c_sharp_jwt.Common;

namespace c_sharp_jwt.Auth.Dto;

public record LoginRequest(
    [property: NotBlank]
    [property: EmailAddress(ErrorMessage = ValidationMessages.MustBeWellFormedEmail)]
    string Email,
    [property: NotBlank]
    string Password);
