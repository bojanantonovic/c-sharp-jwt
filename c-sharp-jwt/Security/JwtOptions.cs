using System.ComponentModel.DataAnnotations;

namespace c_sharp_jwt.Security;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section and validated on start-up, so a misconfigured secret or
/// issuer fails the application instead of the first request.
/// </summary>
/// <remarks>
/// A class rather than a record. The binder fills <c>init</c> accessors either way, so the immutability costs
/// nothing here, but a record would also generate a <c>ToString</c> that prints <see cref="Secret"/> into every
/// log line that formats these options. See DEPENDENCY-INJECTION-COMPARISON.md §5.
/// </remarks>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs a key of at least 256 bits, which is 32 bytes.</summary>
    public const int MinimumSecretLength = 32;

    [Required]
    [MinLength(MinimumSecretLength)]
    public string Secret { get; init; } = string.Empty;

    /// <summary>Value of the <c>iss</c> claim. It is written when a token is created and enforced when one is validated.</summary>
    [Required]
    [Url]
    public string Issuer { get; init; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long ExpirationMs { get; init; }
}
