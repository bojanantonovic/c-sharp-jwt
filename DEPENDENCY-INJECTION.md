# Dependency injection in `c-sharp-jwt`

`Program.cs` is the only composition root. Nothing in this project news up a collaborator it depends on, and
nothing reaches into a static service locator: a class states what it needs as constructor parameters, and the
container supplies them. This document describes what ends up in that container, who puts it there, and how it
comes back out again.

[`DEPENDENCY-INJECTION-COMPARISON.md`](DEPENDENCY-INJECTION-COMPARISON.md) takes each of these topics and
contrasts it with how Spring Boot solves the same problem.

There are three separate mechanisms at work, and they are easy to confuse:

1. **Service registration and constructor injection** — the classic container.
2. **The options pattern** — how `appsettings.json` becomes a typed, validated object that can be injected.
3. **Parameter binding in minimal APIs** — endpoint delegates get their arguments partly from the request and
   partly from the container.

---

## 1. Two phases

```
webApplicationBuilder.Services.…  registration  — descriptors are added to an IServiceCollection
─── webApplicationBuilder.Build() ──────────────────────────────────────────────────────
webApplication.Use…() / .Map…()   resolution    — services are pulled out of the IServiceProvider
```

`WebApplication.CreateBuilder(args)` produces the `IServiceCollection` (`webApplicationBuilder.Services`) and
the `IConfiguration` (`webApplicationBuilder.Configuration`). Everything above `webApplicationBuilder.Build()`
only *describes* services; nothing is constructed yet. `Build()` freezes the collection into the root
`IServiceProvider` exposed as `webApplication.Services`.

That is why `AddPersistence` and `AddApplicationSecurity` take `webApplicationBuilder.Configuration` as a
parameter: at
registration time there is no provider to resolve `IConfiguration` from, so the configuration is handed over
explicitly.

### Where `webApplicationBuilder.Configuration` itself comes from

`CreateBuilder` stacks the default configuration sources, each one overriding the previous:

| Order | Source                                                     |
|-------|------------------------------------------------------------|
| 1     | `appsettings.json`                                          |
| 2     | `appsettings.{Environment}.json`                            |
| 3     | User secrets (Development only)                             |
| 4     | Environment variables                                       |
| 5     | Command-line `args`                                         |

`JwtApplicationFactory` adds a sixth, in-memory source on top, which is how the integration tests swap the
connection string and the whole `Jwt` section without editing `appsettings.json`.

---

## 2. What the container holds

Services of this application, in registration order:

| Service                           | Implementation          | Lifetime  | Registered by                             |
|-----------------------------------|-------------------------|-----------|-------------------------------------------|
| `AppDbContext`                    | itself                  | Scoped    | `AddPersistence` → `AddDbContext<>`       |
| `IUserRepository`                 | `UserRepository`        | Scoped    | `AddPersistence`                          |
| `IOptions<JwtOptions>`            | bound `Jwt` section     | Singleton | `AddApplicationSecurity` → `AddOptions<>` |
| `IOptions<CorsOptions>`           | bound `Cors` section    | Singleton | `AddApplicationSecurity` → `AddOptions<>` |
| `ITokenService`                   | `TokenService`          | Singleton | `AddApplicationSecurity`                  |
| `IPasswordHasher`                 | `BCryptPasswordHasher`  | Singleton | `AddApplicationSecurity`                  |
| `IConfigureOptions<CorsOptions>`¹ | `ConfigureCorsPolicy`   | Singleton | `AddApplicationSecurity`                  |
| `IConfigureOptions<JwtBearerOptions>` | `ConfigureJwtBearerOptions` | Transient | `AddApplicationSecurity` → `ConfigureOptions<>` |
| `AuthService`                     | itself                  | Scoped    | `Program.cs`                              |
| `IExceptionHandler`               | `ApiExceptionHandler`   | Singleton | `AddExceptionHandler<>`                   |

¹ the framework's `Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions`, not this project's `CorsOptions`.
`SecurityConfiguration` aliases the ASP.NET type to `AspNetCorsOptions` to keep the two apart.

Beside these, the framework calls register their own machinery — `ICorsService` and `ICorsPolicyProvider`
(`AddCors`), `IAuthenticationService`, the scheme provider and `JwtBearerHandler` (`AddAuthentication().AddJwtBearer()`),
`IAuthorizationService` and `IAuthorizationPolicyProvider` (`AddAuthorization`), and the `ProblemDetails`
writer (`AddProblemDetails`). Those are resolved by middleware, never by application code.

### The resulting graph

```
AuthService (scoped)
├── IUserRepository ── UserRepository (scoped)
│   └── AppDbContext (scoped)
│       └── DbContextOptions<AppDbContext>  ← ConnectionStrings:Default
├── IPasswordHasher ── BCryptPasswordHasher (singleton, no dependencies)
└── ITokenService ──── TokenService (singleton)
    └── IOptions<JwtOptions>  ← "Jwt" section

JwtBearerHandler (framework)
└── IOptionsMonitor<JwtBearerOptions>
    └── ConfigureJwtBearerOptions
        └── IOptions<JwtOptions>  ← the same bound instance as above

CorsMiddleware (framework)
└── IOptions<AspNetCorsOptions>
    └── ConfigureCorsPolicy
        └── IOptions<CorsOptions>  ← "Cors" section
```

Every class in that graph uses a primary constructor and holds no state beyond what it was given —
`AuthService(IUserRepository, IPasswordHasher, ITokenService)`, `UserRepository(AppDbContext)`,
`TokenService(IOptions<JwtOptions>)`, `AppDbContext(DbContextOptions<AppDbContext>)`.

---

## 3. Lifetimes, and why each one was chosen

| Lifetime      | One instance per… | Used here for                                         |
|---------------|-------------------|-------------------------------------------------------|
| **Singleton** | application       | `TokenService`, `BCryptPasswordHasher`, `ApiExceptionHandler`, the bound options |
| **Scoped**    | HTTP request      | `AppDbContext`, `UserRepository`, `AuthService`        |
| **Transient** | injection point   | `ConfigureJwtBearerOptions`                            |

Two rules explain the whole table.

**Stateless services are singletons.** `TokenService` reads its `JwtOptions` once in its constructor and then
only computes; `BCryptPasswordHasher` holds nothing at all. Creating either per request would buy nothing.

**Anything touching the database is scoped.** EF Core's change tracker is per-unit-of-work, so `AppDbContext` is
registered scoped by `AddDbContext`, and that lifetime propagates upwards: `UserRepository` depends on the
context, `AuthService` depends on the repository, so both must be scoped too. A singleton may not depend on a
scoped service — the container detects this as a captive dependency and throws on the first resolution.

That constraint is also why `MigrateDatabase` opens a scope by hand:

```csharp
using var scope = app.Services.CreateScope();
scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
```

At start-up there is no request and therefore no ambient scope. `app.Services` is the *root* provider, and
asking it directly for the scoped `AppDbContext` would fail. The explicit scope also disposes the context as
soon as the migration is done.

`ApiExceptionHandler` is a singleton because `AddExceptionHandler<T>()` registers it as one. It currently has no
constructor parameters; if it ever needed the database, it too would have to create a scope rather than inject
the context.

---

## 4. The options pattern: configuration as an injectable dependency

No class in this project reads `IConfiguration`. Configuration enters through two typed objects instead:

```csharp
services.AddOptions<JwtOptions>()
    .Bind(configuration.GetSection(JwtOptions.SectionName))   // "Jwt"
    .ValidateDataAnnotations()                                 // [Required], [MinLength], [Url], [Range]
    .ValidateOnStart();                                        // run that validation during host start-up
```

* **`Bind`** maps `Jwt:Secret`, `Jwt:Issuer` and `Jwt:ExpirationMs` onto the properties of `JwtOptions` by name.
* **`ValidateDataAnnotations`** turns the attributes on `JwtOptions` into a validator.
* **`ValidateOnStart`** moves that validation from *first use* to *start-up*. A secret shorter than 256 bits
  fails `dotnet run`, not the first login.

The result is available as `IOptions<JwtOptions>`, which is what `TokenService` and `ConfigureJwtBearerOptions`
inject. `IOptions<T>` is a singleton whose value is computed once and cached, so both see the same instance and
the `Jwt` section is read in exactly one place.

`CorsOptions` follows the same shape with `[MinLength(1)] AllowedOrigins`.

### Configuring *framework* options from *your* options

The interesting half of the pattern is how the bound values reach objects the application does not construct.
`JwtBearerHandler` and the CORS middleware are built by the framework from `JwtBearerOptions` and the ASP.NET
`CorsOptions`. Both are configured by registering an `IConfigureOptions<T>` — a class the container instantiates
with its own dependencies, and whose `Configure` method the options factory calls when the target options object
is first assembled:

```csharp
internal sealed class ConfigureCorsPolicy(IOptions<CorsOptions> corsOptions) : IConfigureOptions<AspNetCorsOptions>
{
    public void Configure(AspNetCorsOptions options) =>
        options.AddPolicy(SecurityConfiguration.CorsPolicyName, policy => policy
            .WithOrigins(corsOptions.Value.AllowedOrigins)
            …);
}
```

So the chain is: `appsettings.json` → `CorsOptions` → `ConfigureCorsPolicy` → the framework's `CorsOptions` →
`UseCors(SecurityConfiguration.CorsPolicyName)`.

`ConfigureJwtBearerOptions` does the same for the bearer handler, implementing `IConfigureNamedOptions<JwtBearerOptions>`
because authentication options are *named* — one set per scheme — and it must only configure the `Bearer` one.

The alternative, reading `configuration.GetSection("Jwt")` a second time inside a `AddJwtBearer(options => …)`
lambda, would bypass the validation and duplicate the binding. Going through DI keeps the section bound once.

The two registrations differ in a detail worth knowing:

```csharp
services.AddSingleton<IConfigureOptions<AspNetCorsOptions>, ConfigureCorsPolicy>();  // explicit
services.ConfigureOptions<ConfigureJwtBearerOptions>();                             // reflection-based
```

`ConfigureOptions<T>()` scans `T` for every `IConfigureOptions<>`, `IPostConfigureOptions<>` and
`IValidateOptions<>` interface it implements and registers it as transient for each. It is shorter; the explicit
form says exactly which options type is being configured. The lifetime difference is immaterial — the resulting
options value is cached either way, so `Configure` runs once.

---

## 5. Injection at the HTTP layer

### Middleware

The pipeline in `Program.cs` resolves services rather than receiving them:

| Call                       | Pulls out of the container                                                              |
|----------------------------|------------------------------------------------------------------------------------------|
| `UseExceptionHandler()`    | all registered `IExceptionHandler` instances, in registration order, plus the `ProblemDetails` writer as fallback |
| `UseCors(policyName)`      | `ICorsService` and `ICorsPolicyProvider`, which read the policy `ConfigureCorsPolicy` added |
| `UseAuthentication()`      | `IAuthenticationService` → the `Bearer` scheme's `JwtBearerHandler` with its configured `JwtBearerOptions` |
| `UseAuthorization()`       | `IAuthorizationPolicyProvider` and `IAuthorizationService`                                |

`UseExceptionHandler()` is called without an argument on purpose. The overloads taking a path or a lambda
predate `IExceptionHandler`; the argument-less one means "use whatever is in the container", which is what makes
`AddExceptionHandler<ApiExceptionHandler>()` the single place where error mapping is declared. It requires a
fallback to exist, and `AddProblemDetails()` is that fallback — without it, the call throws at start-up.

`UseAuthorization()` enforces the endpoint's authorization metadata. `AddApplicationSecurity` sets a
`FallbackPolicy` requiring an authenticated user, so an endpoint that declares nothing is protected by default;
`AuthEndpoints` opts its group out with `.AllowAnonymous()`.

### Endpoint delegates

Minimal API delegates are the one place where injection is not constructor-based. Each parameter is bound by
source:

```csharp
group.MapPost(RegisterPath,
    async (RegisterRequest request, AuthService authService, CancellationToken cancellationToken) => …);
```

| Parameter           | Bound from                                                                          |
|---------------------|--------------------------------------------------------------------------------------|
| `RegisterRequest`   | the JSON request body — a complex type the container does not know                    |
| `AuthService`       | the request's service scope — the framework asks `IServiceProviderIsService` first    |
| `CancellationToken` | `HttpContext.RequestAborted`, a well-known type                                       |
| `ClaimsPrincipal`   | `HttpContext.User`, i.e. the claims of the validated token (`UserEndpoints`)           |

The rule for the second row is worth spelling out: a parameter is taken from the container **because it is
registered there**. `AuthService` is only injectable because `Program.cs` contains `.AddScoped<AuthService>()`;
remove that line and the framework would try to deserialise a request body into it. No `[FromServices]`
attribute is needed.

The scope those services come from is the per-request scope ASP.NET Core creates, which is also what disposes
`AppDbContext` at the end of the request.

### Endpoint filters

`AddEndpointFilter<ValidationFilter<RegisterRequest>>()` is a third variant. The filter is not registered in the
container; it is *activated from* it when the route is built, and the instance is reused for every request
through that endpoint. `ValidationFilter<T>` has no constructor dependencies, but one would be resolved the same
way a constructor parameter is.

---

## 6. Replacing registrations in the tests

Because everything is registered in one place, the integration tests can override it from outside instead of
introducing a test-only abstraction. `JwtApplicationFactory` boots the real `Program` and then:

1. **Adds a configuration source** with an in-memory connection string and a fixed `Jwt` section, so the tests
   never depend on `appsettings.json`. The options pattern picks it up unchanged — including the validation.
2. **Removes and replaces the `AppDbContext` registration**:

```csharp
builder.ConfigureServices(services =>
{
    RemoveDbContextRegistrations(services);
    services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
});
```

`ConfigureServices` runs *after* `Program.cs` has registered everything, and the last registration of a service
type wins — but `AddDbContext` adds several descriptors (the context plus its option types), so the old ones are
removed first rather than merely shadowed.

Everything downstream is unaffected: `UserRepository`, `AuthService`, the endpoints and the bearer handler are
the production ones, resolved through the production graph. Only the leaf changed.

---

## 7. Rules of thumb used in this project

* Register in the composition root or in an `IServiceCollection` extension named after its area
  (`AddPersistence`, `AddApplicationSecurity`) — never in a static initialiser or a `Configure` side effect.
* Pass `IConfiguration` as a parameter to those extensions; do not inject it into services.
* Read a configuration section exactly once, into a validated options class, and inject that.
* Depend on the interface (`IUserRepository`, `ITokenService`, `IPasswordHasher`) where a seam is wanted for
  testing; depend on the concrete class (`AuthService`, `AppDbContext`) where there is no second implementation.
* Let the lifetime follow the state: no state → singleton, per-request state → scoped.
* Never resolve from the root provider in request handling code; create an explicit scope for start-up work.
