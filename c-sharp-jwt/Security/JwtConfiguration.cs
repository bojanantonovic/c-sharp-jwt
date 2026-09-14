using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace c_sharp_jwt.Security;

/// <summary>
/// Keeping the JOSE primitives in one place is what lets the tests exercise the production wiring instead of a
/// look-alike configuration.
/// </summary>
public static class JwtConfiguration
{
    public static SymmetricSecurityKey SigningKey(JwtOptions jwtOptions) =>
        new(Encoding.UTF8.GetBytes(jwtOptions.Secret));

    public static SigningCredentials SigningCredentials(JwtOptions jwtOptions) =>
        new(SigningKey(jwtOptions), JwtConventions.SignatureAlgorithm);

    /// <summary>The audience is not validated because no token is issued for a specific one.</summary>
    public static TokenValidationParameters ValidationParameters(JwtOptions jwtOptions) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey(jwtOptions),
        ValidAlgorithms = [JwtConventions.SignatureAlgorithm],
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = JwtConventions.ClockSkew,
        NameClaimType = JwtConventions.SubjectClaim,
        RoleClaimType = JwtConventions.RolesClaim
    };
}
