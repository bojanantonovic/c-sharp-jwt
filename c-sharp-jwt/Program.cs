using c_sharp_jwt.Auth;
using c_sharp_jwt.Common;
using c_sharp_jwt.Data;
using c_sharp_jwt.Security;
using c_sharp_jwt.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplicationSecurity(builder.Configuration);
builder.Services.AddScoped<AuthService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.MigrateDatabase();

app.UseExceptionHandler();
app.UseCors(SecurityConfiguration.CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapUserEndpoints();

app.Run();

/// <summary>
/// Exposed so that the integration tests can boot the real application through <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
