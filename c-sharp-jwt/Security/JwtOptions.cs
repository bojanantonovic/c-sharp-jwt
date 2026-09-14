using System.ComponentModel.DataAnnotations;

namespace c_sharp_jwt.Security;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section and validated on start-up, so a misconfigured secret or
/// issuer fails the application instead of the first request.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs a key of at least 256 bits, which is 32 bytes.</summary>
    public const int MinimumSecretLength = 32;

    [Required]
    [MinLength(MinimumSecretLength)]
    public string Secret { get; set; } = string.Empty;

    /// <summary>Value of the <c>iss</c> claim. It is written when a token is created and enforced when one is validated.</summary>
    [Required]
    [Url]
    public string Issuer { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long ExpirationMs { get; set; }
}
