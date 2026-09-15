using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using c_sharp_jwt.Auth;
using c_sharp_jwt.Common;
using c_sharp_jwt.Data;
using c_sharp_jwt.Security;
using c_sharp_jwt.Users;

// The composition root: every service resolved at runtime is registered here or by an extension method called
// here. See DEPENDENCY-INJECTION.md, and DEPENDENCY-INJECTION-COMPARISON.md for how this differs from Spring.

// CreateBuilder assembles builder.Configuration from appsettings.json, appsettings.{Environment}.json, user
// secrets, environment variables and the command line — in that order, a later source overriding an earlier one.
// The integration tests add an in-memory source on top of it, which is how they replace the connection string
// and the Jwt section without touching appsettings.json.
var webApplicationBuilder = WebApplication.CreateBuilder(args);

webApplicationBuilder.Services
    // PersistenceConfiguration.AddPersistence: takes the configuration explicitly because it reads
    // ConnectionStrings:Default from it, and registers AppDbContext (scoped, its DbContextOptions built from
    // that connection string) together with IUserRepository -> UserRepository.
    .AddPersistence(webApplicationBuilder.Configuration)
    // SecurityConfiguration.AddApplicationSecurity: binds the Jwt and Cors sections to JwtOptions/CorsOptions,
    // validated on start-up, and registers everything that reads them — ITokenService, IPasswordHasher, the
    // named CORS policy, the JWT bearer handler and the deny-by-default authorization policy.
    .AddApplicationSecurity(webApplicationBuilder.Configuration)
    // AuthService is a concrete class with no interface, so nothing else registers it. Its constructor
    // parameters (IUserRepository, IPasswordHasher, ITokenService) are all satisfied by the two calls above.
    // Scoped, because it depends on the scoped repository, which depends on the scoped AppDbContext.
    .AddScoped<AuthService>()
    // Adds ApiExceptionHandler to the IExceptionHandler chain as a singleton. The container instantiates it, so
    // it could take constructor dependencies — but being a singleton, no scoped one such as AppDbContext.
    .AddExceptionHandler<ApiExceptionHandler>()
    // Registers the ProblemDetails writer. It is what UseExceptionHandler() falls back to for an exception no
    // IExceptionHandler claims, and what makes the argument-less overload below valid in the first place.
    .AddProblemDetails();

// Build() closes the service collection and turns it into the root IServiceProvider behind app.Services.
// After this line services are resolved, not registered.
var webApplication = webApplicationBuilder.Build();

// PersistenceConfiguration.MigrateDatabase: opens a scope on app.Services, resolves AppDbContext from it — the
// context is scoped, so it cannot be taken from the root provider — and applies the EF Core migrations before
// the first request is served.
webApplication.MigrateDatabase();

// No arguments and no lambda: the middleware resolves the IExceptionHandler instances from the container, in
// registration order, and asks each one whether it handles the exception. ApiExceptionHandler returning false
// leaves the response to the ProblemDetails writer added above.
webApplication.UseExceptionHandler()
    // Applies the policy that ConfigureCorsPolicy added to the framework's CorsOptions under this name.
    // The name is a constant on SecurityConfiguration so that the registering and the using side cannot drift.
    .UseCors(SecurityConfiguration.CorsPolicyName)
    // Runs the JwtBearerHandler for the default scheme. Its JwtBearerOptions are the ones
    // ConfigureJwtBearerOptions produced from the bound JwtOptions. On success the decoded token becomes
    // HttpContext.User, which is what the endpoints below receive as ClaimsPrincipal.
    .UseAuthentication()
    // Enforces the authorization metadata of the matched endpoint: whatever the endpoint declares itself, or
    // else the fallback policy from AddApplicationSecurity, which requires an authenticated user.
    .UseAuthorization();

// The endpoint delegates declare their dependencies as parameters. The framework resolves each parameter either
// from the request (the JSON body, the ClaimsPrincipal, the CancellationToken) or, for anything it does not
// recognise as request data, from the container — AuthService here, and the ValidationFilter<T> instances that
// the endpoint filters attach.
webApplication.MapAuthEndpoints()
    .MapUserEndpoints();

webApplication.Run();

/// <summary>
/// Exposed so that the integration tests can boot the real application through <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
