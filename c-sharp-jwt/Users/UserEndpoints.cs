using System.Security.Claims;
using c_sharp_jwt.Security;
using c_sharp_jwt.Users.Dto;

namespace c_sharp_jwt.Users;

public static class UserEndpoints
{
    private const string BasePath = "/api/users";
    private const string MePath = "/me";

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
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
