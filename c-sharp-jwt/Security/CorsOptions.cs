namespace c_sharp_jwt.Security;

public class CorsOptions
{
    public const string SectionName = "Cors";

    public required string[] AllowedOrigins { get; set; }
}
