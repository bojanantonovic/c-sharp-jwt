using System;
using System.ComponentModel.DataAnnotations;

namespace c_sharp_jwt.Common;

/// <summary>
/// Counterpart of Jakarta Bean Validation's <c>@NotBlank</c>: unlike <see cref="RequiredAttribute"/> it also
/// rejects a value consisting only of whitespace.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotBlankAttribute : ValidationAttribute
{
    public NotBlankAttribute() : base(ValidationMessages.MustNotBeBlank)
    {
    }

    public override bool IsValid(object? value) => value is string text && !string.IsNullOrWhiteSpace(text);
}
