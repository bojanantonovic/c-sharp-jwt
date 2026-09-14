using System.ComponentModel.DataAnnotations;

namespace c_sharp_jwt.Security;

public class CorsOptions
{
    public const string SectionName = "Cors";

    [MinLength(1)]
    public string[] AllowedOrigins { get; set; } = [];
}
