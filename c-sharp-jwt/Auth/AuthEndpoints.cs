using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using c_sharp_jwt.Auth.Dto;
using c_sharp_jwt.Common;

namespace c_sharp_jwt.Auth;

public static class AuthEndpoints
{
    private const string BasePath = "/api/auth";
    private const string RegisterPath = "/register";
    private const string LoginPath = "/login";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(BasePath).AllowAnonymous();

        group.MapPost(RegisterPath,
                async (RegisterRequest request, AuthService authService, CancellationToken cancellationToken) =>
                    Results.Json(await authService.RegisterAsync(request, cancellationToken),
                                 statusCode: StatusCodes.Status201Created))
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        group.MapPost(LoginPath,
                (LoginRequest request, AuthService authService, CancellationToken cancellationToken) =>
                    authService.LoginAsync(request, cancellationToken))
            .AddEndpointFilter<ValidationFilter<LoginRequest>>();

        return app;
    }
}
