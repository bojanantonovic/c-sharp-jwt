using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using c_sharp_jwt.Security;
using c_sharp_jwt.Users.Dto;

namespace c_sharp_jwt.Users;

public static class UserEndpoints
{
    private const string BasePath = "/api/users";
    private const string MePath = "/me";

    extension(IEndpointRouteBuilder app)
    {
        public IEndpointRouteBuilder MapUserEndpoints()
        {
            var group = app.MapGroup(BasePath).RequireAuthorization();

            // The principal is built from the decoded token: the bearer handler validated it before this delegate is
            // reached, so no database lookup is needed to answer the request.
            group.MapGet(MePath, (ClaimsPrincipal user) => new CurrentUserResponse(
                user.Identity!.Name!,
                user.FindAll(JwtConventions.RolesClaim).Select(claim => claim.Value).ToList()));

            return app;
        }
    }
}
