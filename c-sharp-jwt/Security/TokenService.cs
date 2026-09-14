using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace c_sharp_jwt.Security;

/// <inheritdoc />
public class TokenService(IOptions<JwtOptions> jwtOptions) : ITokenService
{
    private static readonly JsonWebTokenHandler TokenHandler = new();

    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public string GenerateToken(string subject, IEnumerable<string> roles) =>
        TokenHandler.CreateToken(ToDescriptor(subject, roles));

    private SecurityTokenDescriptor ToDescriptor(string subject, IEnumerable<string> roles)
    {
        var issuedAt = DateTime.UtcNow;
        return new SecurityTokenDescriptor
        {
            Issuer = _jwtOptions.Issuer,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.AddMilliseconds(_jwtOptions.ExpirationMs),
            Claims = new Dictionary<string, object>
            {
                [JwtConventions.SubjectClaim] = subject,
                [JwtConventions.RolesClaim] = roles.ToArray()
            },
            SigningCredentials = JwtConfiguration.SigningCredentials(_jwtOptions)
        };
    }
}
