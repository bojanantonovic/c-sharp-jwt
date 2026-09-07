using System.Security.Claims;
using c_sharp_jwt.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace c_sharp_jwt.Security;

/// <summary>
/// Re-loads the user's role from the database on every request, mirroring the Spring
/// counterpart's CustomUserDetailsService lookup in JwtAuthenticationFilter: the JWT
/// itself only carries the email (sub claim), never a role.
/// </summary>
public class UserClaimsTransformation(AppDbContext dbContext) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var email = principal.Identity?.Name;
        if (email is null || principal.HasClaim(claim => claim.Type == ClaimTypes.Role))
        {
            return principal;
        }

        var role = await dbContext.Users
            .Where(user => user.Email == email)
            .Select(user => user.Role)
            .FirstOrDefaultAsync();

        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
        return principal;
    }
}
