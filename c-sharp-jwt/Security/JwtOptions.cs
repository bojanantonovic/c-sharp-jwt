namespace c_sharp_jwt.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Secret { get; set; }
    public long ExpirationMs { get; set; }
}
