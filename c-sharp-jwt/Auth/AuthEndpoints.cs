using c_sharp_jwt.Auth.Dto;
using c_sharp_jwt.Common;

namespace c_sharp_jwt.Auth;

public static class AuthEndpoints
{
    private const string BasePath = "/api/auth";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(BasePath).AllowAnonymous();

        group.MapPost("/register", async (RegisterRequest request, AuthService authService) =>
        {
            var errors = ValidationHelper.Validate(request);
            if (errors.Count > 0)
            {
                return Results.BadRequest(new
                {
                    timestamp = DateTimeOffset.UtcNow, status = 400, message = "Validation failed", errors
                });
            }

            var response = await authService.RegisterAsync(request);
            return Results.Created(string.Empty, response);
        });

        group.MapPost("/login", async (LoginRequest request, AuthService authService) =>
        {
            var errors = ValidationHelper.Validate(request);
            if (errors.Count > 0)
            {
                return Results.BadRequest(new
                {
                    timestamp = DateTimeOffset.UtcNow, status = 400, message = "Validation failed", errors
                });
            }

            var response = await authService.LoginAsync(request);
            return Results.Ok(response);
        });
    }
}
