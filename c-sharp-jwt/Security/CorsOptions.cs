using System.ComponentModel.DataAnnotations;

namespace c_sharp_jwt.Security;

/// <summary>
/// Bound from the <c>Cors</c> configuration section. A class with <c>init</c> accessors for the same reason as
/// <see cref="JwtOptions"/>.
/// </summary>
public class CorsOptions
{
    public const string SectionName = "Cors";

    [MinLength(1)]
    public string[] AllowedOrigins { get; } = [];
}
