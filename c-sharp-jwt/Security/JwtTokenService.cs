using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace c_sharp_jwt.Security;

public class JwtTokenService(IOptions<JwtOptions> jwtOptions)
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public string GenerateToken(string email)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            claims: [new Claim(JwtRegisteredClaimNames.Sub, email)],
            notBefore: now,
            expires: now.AddMilliseconds(_jwtOptions.ExpirationMs),
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }
}
