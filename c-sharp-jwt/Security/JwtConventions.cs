using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace c_sharp_jwt.Security;

/// <summary>
/// The conventions that token creation and token validation have to agree on.
/// </summary>
public static class JwtConventions
{
    /// <summary>The only algorithm accepted when a token is validated.</summary>
    public const string SignatureAlgorithm = SecurityAlgorithms.HmacSha256;

    /// <summary>Carries the email of the user.</summary>
    public const string SubjectClaim = JwtRegisteredClaimNames.Sub;

    /// <summary>
    /// Custom claim holding the roles of the subject. Wiring it up as
    /// <see cref="TokenValidationParameters.RoleClaimType"/> is what makes <c>ClaimsPrincipal.IsInRole</c> and
    /// <c>RequireRole</c> work without any further mapping.
    /// </summary>
    public const string RolesClaim = "roles";

    /// <summary>
    /// Tolerance applied to <c>exp</c> and <c>nbf</c>. The default of <see cref="TokenValidationParameters"/> is
    /// five minutes, which is long enough to let a token that the issuer already considers expired still pass.
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(60);
}
