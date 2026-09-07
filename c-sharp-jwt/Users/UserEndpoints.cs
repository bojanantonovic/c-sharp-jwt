namespace c_sharp_jwt.Users;

public static class UserEndpoints
{
    private const string BasePath = "/api/users";

    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(BasePath).RequireAuthorization();

        group.MapGet("/me", (HttpContext context) =>
            Results.Ok(new { email = context.User.Identity!.Name }));
    }
}
