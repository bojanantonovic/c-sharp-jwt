using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using c_sharp_jwt.Auth;
using c_sharp_jwt.Common;
using c_sharp_jwt.Data;
using c_sharp_jwt.Security;
using c_sharp_jwt.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPersistence(builder.Configuration)
    .AddApplicationSecurity(builder.Configuration)
    .AddScoped<AuthService>()
    .AddExceptionHandler<ApiExceptionHandler>()
    .AddProblemDetails();

var app = builder.Build();

app.MigrateDatabase();

app.UseExceptionHandler()
    .UseCors(SecurityConfiguration.CorsPolicyName)
    .UseAuthentication()
    .UseAuthorization();

app.MapAuthEndpoints()
    .MapUserEndpoints();

app.Run();

/// <summary>
/// Exposed so that the integration tests can boot the real application through <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
