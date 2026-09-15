# `Program.cs` line by line

`Program.cs` is the whole start-up of this application: there is no `Startup` class, no auto-discovery and no
convention that registers anything behind your back. Seventy-four lines, of which roughly half are comments,
decide which services exist, in which order requests pass through the pipeline, and which endpoints are
reachable.

This document walks through it in the order the file reads. [`DEPENDENCY-INJECTION.md`](DEPENDENCY-INJECTION.md)
covers the container itself in more depth — what is registered, with which lifetime, and how it is injected.

## The file

```csharp
 1  using Microsoft.AspNetCore.Builder;
 2  using Microsoft.Extensions.DependencyInjection;
 3  using c_sharp_jwt.Auth;
 4  using c_sharp_jwt.Common;
 5  using c_sharp_jwt.Data;
 6  using c_sharp_jwt.Security;
 7  using c_sharp_jwt.Users;
 8
 9  // The composition root: every service resolved at runtime is registered here or by an extension method called
10  // here. See DEPENDENCY-INJECTION.md, and DEPENDENCY-INJECTION-COMPARISON.md for how this differs from Spring.
11
12  // CreateBuilder assembles builder.Configuration from appsettings.json, appsettings.{Environment}.json, user
13  // secrets, environment variables and the command line — in that order, a later source overriding an earlier one.
14  // The integration tests add an in-memory source on top of it, which is how they replace the connection string
15  // and the Jwt section without touching appsettings.json.
16  var builder = WebApplication.CreateBuilder(args);
17
18  builder.Services
19      // PersistenceConfiguration.AddPersistence: takes the configuration explicitly because it reads
20      // ConnectionStrings:Default from it, and registers AppDbContext (scoped, its DbContextOptions built from
21      // that connection string) together with IUserRepository -> UserRepository.
22      .AddPersistence(builder.Configuration)
23      // SecurityConfiguration.AddApplicationSecurity: binds the Jwt and Cors sections to JwtOptions/CorsOptions,
24      // validated on start-up, and registers everything that reads them — ITokenService, IPasswordHasher, the
25      // named CORS policy, the JWT bearer handler and the deny-by-default authorization policy.
26      .AddApplicationSecurity(builder.Configuration)
27      // AuthService is a concrete class with no interface, so nothing else registers it. Its constructor
28      // parameters (IUserRepository, IPasswordHasher, ITokenService) are all satisfied by the two calls above.
29      // Scoped, because it depends on the scoped repository, which depends on the scoped AppDbContext.
30      .AddScoped<AuthService>()
31      // Adds ApiExceptionHandler to the IExceptionHandler chain as a singleton. The container instantiates it, so
32      // it could take constructor dependencies — but being a singleton, no scoped one such as AppDbContext.
33      .AddExceptionHandler<ApiExceptionHandler>()
34      // Registers the ProblemDetails writer. It is what UseExceptionHandler() falls back to for an exception no
35      // IExceptionHandler claims, and what makes the argument-less overload below valid in the first place.
36      .AddProblemDetails();
37
38  // Build() closes the service collection and turns it into the root IServiceProvider behind app.Services.
39  // After this line services are resolved, not registered.
40  var app = builder.Build();
41
42  // PersistenceConfiguration.MigrateDatabase: opens a scope on app.Services, resolves AppDbContext from it — the
43  // context is scoped, so it cannot be taken from the root provider — and applies the EF Core migrations before
44  // the first request is served.
45  app.MigrateDatabase();
46
47  // No arguments and no lambda: the middleware resolves the IExceptionHandler instances from the container, in
48  // registration order, and asks each one whether it handles the exception. ApiExceptionHandler returning false
49  // leaves the response to the ProblemDetails writer added above.
50  app.UseExceptionHandler()
51      // Applies the policy that ConfigureCorsPolicy added to the framework's CorsOptions under this name.
52      // The name is a constant on SecurityConfiguration so that the registering and the using side cannot drift.
53      .UseCors(SecurityConfiguration.CorsPolicyName)
54      // Runs the JwtBearerHandler for the default scheme. Its JwtBearerOptions are the ones
55      // ConfigureJwtBearerOptions produced from the bound JwtOptions. On success the decoded token becomes
56      // HttpContext.User, which is what the endpoints below receive as ClaimsPrincipal.
57      .UseAuthentication()
58      // Enforces the authorization metadata of the matched endpoint: whatever the endpoint declares itself, or
59      // else the fallback policy from AddApplicationSecurity, which requires an authenticated user.
60      .UseAuthorization();
61
62  // The endpoint delegates declare their dependencies as parameters. The framework resolves each parameter either
63  // from the request (the JSON body, the ClaimsPrincipal, the CancellationToken) or, for anything it does not
64  // recognise as request data, from the container — AuthService here, and the ValidationFilter<T> instances that
65  // the endpoint filters attach.
66  app.MapAuthEndpoints()
67      .MapUserEndpoints();
68
69  app.Run();
70
71  /// <summary>
72  /// Exposed so that the integration tests can boot the real application through <c>WebApplicationFactory</c>.
73  /// </summary>
74  public partial class Program;
```

## Lines 1–7 — the usings

`ImplicitUsings` is switched off in `c-sharp-jwt.csproj`, so every namespace a file needs is listed in it. The
seven lines here are worth reading as a summary of what the composition root touches: two framework namespaces
for the two things it does — `Microsoft.AspNetCore.Builder` for the pipeline, `Microsoft.Extensions.DependencyInjection`
for the container — and the five project namespaces that mirror the five folders of the solution.

Every other type this file names (`AuthService`, `ApiExceptionHandler`, `SecurityConfiguration`) and every
extension method it calls (`AddPersistence`, `AddApplicationSecurity`, `MapAuthEndpoints`, `MapUserEndpoints`,
`MigrateDatabase`) comes from those five.

## Lines 9–16 — the builder and where configuration comes from

`WebApplication.CreateBuilder(args)` (line 16) creates the two things the rest of the file works with:
`builder.Services`, an empty-ish `IServiceCollection`, and `builder.Configuration`, already populated from the
default sources. Each source overrides the previous one:

| Order | Source                                        |
|-------|-----------------------------------------------|
| 1     | `appsettings.json`                            |
| 2     | `appsettings.{Environment}.json`              |
| 3     | User secrets (Development only)               |
| 4     | Environment variables                         |
| 5     | Command-line `args`                           |

The environment in step 2 comes from `ASPNETCORE_ENVIRONMENT`, and steps 4 and 5 are why a deployment can
replace `Jwt:Secret` without shipping a different `appsettings.json` — the key is spelled `Jwt__Secret` as an
environment variable and `--Jwt:Secret` on the command line.

`JwtApplicationFactory` in the test project adds a sixth source on top of these, which is how the integration
tests substitute an in-memory connection string and a fixed `Jwt` section while still booting this exact file.

## Lines 18–36 — registering the services

One fluent chain, because every `Add…` returns the `IServiceCollection` it was called on. The order within the
chain does not matter — these are declarations, and nothing is constructed until something asks for it.

### Lines 19–22 — `AddPersistence(builder.Configuration)`

From `Data/PersistenceConfiguration.cs`:

```csharp
public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString(ConnectionStringName)
                           ?? throw new InvalidOperationException(MissingConnectionStringMessage);

    services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
    services.AddScoped<IUserRepository, UserRepository>();

    return services;
}
```

The configuration is a parameter rather than something the method resolves, because at this point in the file
there is no `IServiceProvider` to resolve anything from — `builder.Build()` is still twenty lines away. This is
the reason both configuration extensions in this project have that signature.

`GetConnectionString("Default")` reads `ConnectionStrings:Default`. A missing entry throws here, during
registration, rather than producing a `DbContext` that fails on the first query.

`AddDbContext<AppDbContext>` registers the context **scoped** — one per HTTP request — along with the
`DbContextOptions<AppDbContext>` its constructor takes. That lifetime is the reason `UserRepository` and
`AuthService` are scoped as well, and the reason line 45 needs a scope of its own.

### Lines 23–26 — `AddApplicationSecurity(builder.Configuration)`

The larger of the two, from `Security/SecurityConfiguration.cs`. It does six things:

1. **Binds and validates `JwtOptions`** from the `Jwt` section, with `ValidateDataAnnotations().ValidateOnStart()`.
2. **Binds and validates `CorsOptions`** from the `Cors` section the same way.
3. **Registers `ITokenService` → `TokenService` and `IPasswordHasher` → `BCryptPasswordHasher`**, both
   singletons — neither holds per-request state.
4. **Registers `ConfigureCorsPolicy`** as an `IConfigureOptions<>` for the framework's own `CorsOptions`, which
   is what turns `Cors:AllowedOrigins` into the named policy line 53 later asks for.
5. **Registers `ConfigureJwtBearerOptions` and calls `AddAuthentication(...).AddJwtBearer()`**, installing the
   framework's `JwtBearerHandler` with the validation parameters from `JwtConfiguration`.
6. **Sets a fallback authorization policy** requiring an authenticated user, so an endpoint that declares no
   authorization of its own is protected rather than public.

Point 6 is the one with teeth: adding a new endpoint without thinking about authorization makes it return `401`,
not leak data. `AuthEndpoints` opts its group out explicitly with `.AllowAnonymous()`.

Points 4 and 5 are both the same indirection — a class that the container builds from the *application's*
options and that then configures the *framework's* options. `DEPENDENCY-INJECTION.md` §4 explains why that is
preferable to reading the configuration a second time inside an `AddJwtBearer(options => …)` lambda.

### Lines 27–30 — `AddScoped<AuthService>()`

The only service registered directly in this file. It belongs to neither of the two areas above, and giving
`Auth` an extension method that registers a single class would be ceremony.

`AuthService` has no interface. It is registered by its concrete type because there is no second implementation
and no test substitutes it — `AuthServiceTests` constructs it with fakes directly, and the integration tests use
the real one. Its three constructor parameters are all satisfied by the two calls above:

```csharp
public class AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService)
```

Scoped, and not something longer-lived, because `IUserRepository` is scoped: a singleton holding a scoped
dependency is a captive dependency, which the container rejects at the first resolution.

### Lines 31–33 — `AddExceptionHandler<ApiExceptionHandler>()`

Adds `ApiExceptionHandler` to the chain of `IExceptionHandler` instances that line 50 will pull back out. The
handler maps the exceptions the application raises on purpose onto status codes:

| Exception                     | Status |
|-------------------------------|--------|
| `EmailAlreadyInUseException`  | `409`  |
| `InvalidCredentialsException` | `401`  |
| anything else                 | not handled — `TryHandleAsync` returns `false` |

It is registered as a singleton. That is fine as it stands — it has no constructor parameters — but it means a
future version could not inject `AppDbContext` to, say, look something up while formatting the error.

### Lines 34–36 — `AddProblemDetails()`

Registers the `ProblemDetails` writer, which does two jobs here. It is the fallback for every exception
`ApiExceptionHandler` declines, producing an RFC 9457 response with a `500` and no leaked message. And it is
what makes the argument-less `UseExceptionHandler()` on line 50 legal in the first place: without a fallback,
that overload throws at start-up.

## Lines 38–40 — `builder.Build()`

The dividing line of the file. Above it, services are *described*; below it, they are *resolved*. The
`IServiceCollection` is sealed into the root `IServiceProvider` behind `app.Services`, and a later
`builder.Services.Add…` would throw.

Everything after this point — the migration, the four middleware, the endpoints — reads from that provider.

## Lines 42–45 — `app.MigrateDatabase()`

From `PersistenceConfiguration` again:

```csharp
public static WebApplication MigrateDatabase(this WebApplication app)
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

    return app;
}
```

`app.Services` is the **root** provider. `AppDbContext` is scoped, and a scoped service cannot be resolved from
the root — so the method creates a scope, resolves the context inside it, applies the pending EF Core
migrations, and disposes both at the closing brace. This is the standard shape for any work that has to happen
at start-up but needs a request-lifetime service.

Two consequences of where this call sits:

* It runs before the HTTP server is listening, so no request can ever hit a schema that has not been migrated.
* It also runs before `app.Run()`, and therefore before the `ValidateOnStart` validators from lines 23–26 get
  their chance. A database with a bad `Jwt:Secret` in the configuration is migrated first and rejected second.

In the integration tests the same call runs against the in-memory SQLite connection `JwtApplicationFactory`
substituted, which is what gives every test class a fresh schema without a migration step of its own.

## Lines 47–60 — the middleware pipeline

Four middleware, and their order is the load-bearing part.

| Order | Middleware              | Why here                                                                        |
|-------|-------------------------|---------------------------------------------------------------------------------|
| 1     | `UseExceptionHandler()` | It can only catch what runs *after* it, so it goes first                         |
| 2     | `UseCors(…)`            | A CORS preflight carries no `Authorization` header and must be answered before anything can reject it |
| 3     | `UseAuthentication()`   | Establishes `HttpContext.User` …                                                 |
| 4     | `UseAuthorization()`    | … which this one then checks against the endpoint's policy                        |

`WebApplication` adds `UseRouting` in front of this chain and `UseEndpoints` behind it automatically, so by the
time `UseAuthorization` runs, the endpoint has been matched and its authorization metadata is available. That is
what lets a `RequireAuthorization()` on a route group have any effect.

**Line 50, `UseExceptionHandler()`** — no path, no lambda. The older overloads re-execute the request against an
error path or run a handler delegate; this one means *use what is in the container*, and asks each registered
`IExceptionHandler` in registration order whether it handles the exception. `ApiExceptionHandler` returning
`false` hands the response to the `ProblemDetails` writer from line 36. The upshot is that error mapping is
declared in exactly one place — line 33 — and never in an endpoint.

**Line 53, `UseCors(SecurityConfiguration.CorsPolicyName)`** — looks up a policy *by name*. The policy itself
was never mentioned in this file; `ConfigureCorsPolicy` added it to the framework's `CorsOptions` from
`Cors:AllowedOrigins`. The name is a constant on `SecurityConfiguration` so that the side registering it and the
side requesting it cannot drift apart — a typo here would otherwise be a runtime error at the first preflight.

**Line 57, `UseAuthentication()`** — runs the `JwtBearerHandler` for the default scheme. No application code is
involved in validating a token: the handler reads the `Authorization` header and checks it against the
`TokenValidationParameters` that `ConfigureJwtBearerOptions` produced from the bound `JwtOptions`. On success the
decoded token becomes `HttpContext.User`.

**Line 60, `UseAuthorization()`** — enforces the matched endpoint's metadata: `RequireAuthorization()` where the
endpoint declares it, `AllowAnonymous()` where it opts out, and otherwise the fallback policy from
`AddApplicationSecurity`.

## Lines 62–67 — mapping the endpoints

`MapAuthEndpoints` creates the `/api/auth` group, marks it `AllowAnonymous()`, and maps `POST /register` and
`POST /login`, each with a `ValidationFilter<T>` endpoint filter that validates the body before the delegate
runs. `MapUserEndpoints` creates the `/api/users` group with `RequireAuthorization()` and maps `GET /me`.

Both return `IEndpointRouteBuilder`, which is why they chain.

The delegates are where injection stops being constructor-based. Each parameter is bound from a different
source:

| Parameter           | Bound from                                                                       |
|---------------------|----------------------------------------------------------------------------------|
| `RegisterRequest`   | the JSON body — a complex type the container does not know                        |
| `AuthService`       | the request's service scope, because line 30 registered it                        |
| `CancellationToken` | `HttpContext.RequestAborted`                                                      |
| `ClaimsPrincipal`   | `HttpContext.User` — the claims of the token line 57 validated                     |

The second row is the one that surprises people: no `[FromServices]` attribute is involved. The framework asks
the container whether the type is registered, and `AuthService` is only injectable *because* line 30 exists.
Delete that line and the framework would try to deserialise a request body into it.

The fourth row is what makes `GET /api/users/me` free of a database lookup: the principal is built from the
decoded token, so the endpoint answers from claims alone.

## Line 69 — `app.Run()`

Starts the host and blocks until shutdown. The `ValidateOnStart` validators registered back on lines 23–26 run
inside this call, so a `Jwt:Secret` shorter than 256 bits or a non-URI `Jwt:Issuer` fails here — before the
server accepts a single connection, rather than on the first login.

## Lines 71–74 — `public partial class Program`

Top-level statements are compiled into a generated entry-point class that is `internal`. `WebApplicationFactory<TEntryPoint>`
needs that type to be reachable from the test assembly, so it is re-declared here as `public partial`. The
trailing semicolon instead of `{ }` is the C# 12 form for a type with an empty body.

This one declaration is what allows `JwtApplicationFactory` to boot the real application — these exact
registrations, this exact pipeline — instead of a look-alike assembled in the test project.

## Why the file stays this short

Every area contributes one extension method named after what it does, and the composition root calls it:
`AddPersistence`, `AddApplicationSecurity`, `MigrateDatabase`, `MapAuthEndpoints`, `MapUserEndpoints`. The
details live next to the code they configure, and `Program.cs` stays readable as a table of contents for the
application.

---

`ProgramListingTests` checks that the listing above quotes `c-sharp-jwt/Program.cs` verbatim and that its line
numbers run from 1 without a gap, so the numbers this document refers to cannot silently go stale.
