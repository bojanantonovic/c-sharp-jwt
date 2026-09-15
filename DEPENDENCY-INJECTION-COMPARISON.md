# Dependency injection: ASP.NET Core vs. Spring Boot

Both projects build the same object graph. A service that registers a user needs a repository, a password
hasher and a token issuer; the repository needs a database session; the token issuer needs a signing secret that
came out of a configuration file. Nothing about that graph differs between
[`c-sharp-jwt`](.) and its Spring sibling `spring-boot-oauth2-jwt`.

What differs is **who builds it, when, and how much of it you have to say out loud.**

This document is the deep dive on that one topic. [`DEPENDENCY-INJECTION.md`](DEPENDENCY-INJECTION.md) describes
the C# container on its own terms, [`PROGRAM.md`](PROGRAM.md) walks through the composition root line by line,
and [`COMPARISON.md`](COMPARISON.md) §3.5–3.7 compares the two projects on everything else.

---

## 1. Where the object graph is declared

**Spring: nowhere in particular.** The entry point says almost nothing:

```java
@SpringBootApplication
@ConfigurationPropertiesScan
public class SpringBootOauth2JwtApplication {
    public static void main(String[] args) {
        SpringApplication.run(SpringBootOauth2JwtApplication.class, args);
    }
}
```

`@SpringBootApplication` expands to `@Configuration` + `@ComponentScan` + `@EnableAutoConfiguration`. The graph
is assembled from three sources, none of which is visible here:

1. **Component scanning** of `ch.antonovic.springbootoauth2jwt` and below — every `@Service`, `@Component`,
   `@RestController`, `@Configuration` and `@RestControllerAdvice` found there becomes a bean.
2. **`@ConfigurationPropertiesScan`** — every `@ConfigurationProperties` record becomes a bean too.
3. **Auto-configuration** — roughly a hundred conditional configuration classes on the classpath that register
   a `DataSource`, an `EntityManagerFactory`, a Jackson `ObjectMapper`, the MVC infrastructure, and so on,
   each backing off if the application has already defined its own.

To find out what is in the container you start the application and read the logs, or ask the `ApplicationContext`.

**C#: one file, eighteen lines.** `Program.cs` lines 18–36 *are* the graph:

```csharp
webApplicationBuilder.Services
    .AddPersistence(webApplicationBuilder.Configuration)
    .AddApplicationSecurity(webApplicationBuilder.Configuration)
    .AddScoped<AuthService>()
    .AddExceptionHandler<ApiExceptionHandler>()
    .AddProblemDetails();
```

There is no scanning. A class that is not named in one of these five calls (or in the two extension methods they
delegate to) is not in the container. `AuthService` is injectable *because* line 30 exists — delete it and the
framework stops recognising the parameter.

| | Spring Boot | ASP.NET Core |
|---|---|---|
| Discovery | classpath scanning + auto-configuration | explicit registration only |
| Where to look | annotations spread over the source tree | `Program.cs` and two extension methods |
| Cost of an extra service | one annotation | one line in the composition root |
| Cost of understanding the whole | high — the list is implicit | low — the list is the file |

Neither is strictly better. Spring's model means a new `@Service` needs no edit to any shared file, which scales
well across a large team. ASP.NET's model means there is no such thing as a bean you did not expect.

---

## 2. The same service, both ways

```java
// Spring
@Service
@RequiredArgsConstructor
public class AuthService {
    private final UserRepository userRepository;
    private final PasswordEncoder passwordEncoder;
    private final AuthenticationManager authenticationManager;
    private final TokenService tokenService;
    …
}
```

```csharp
// C#
public class AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService)
{
    …
}
// registered in Program.cs:
webApplicationBuilder.Services.AddScoped<AuthService>();
```

The class bodies are nearly identical. The differences are all around the edges:

* **Who declares it.** `@Service` declares it on the class; `.AddScoped<AuthService>()` declares it in the
  composition root. The class itself stays free of framework attributes — `AuthService` in C# has no `using`
  for anything DI-related at all.
* **The constructor.** Lombok's `@RequiredArgsConstructor` generates a constructor over the `final` fields;
  C#'s primary constructor does the same thing in the language. Both containers use it implicitly: Spring
  because a single constructor needs no `@Autowired` since 4.3, ASP.NET because it picks the greediest
  constructor whose parameters it can all satisfy.
* **The lifetime.** Spring's default is singleton and is not written down. ASP.NET has no default — `AddScoped`
  versus `AddSingleton` is a decision you cannot avoid making. §4 is about why that matters here.

---

## 3. How a class becomes injectable

| Spring Boot | ASP.NET Core in this project |
|---|---|
| `@Service` / `@Component` / `@Repository` on the class | `services.AddScoped<AuthService>()` |
| `@Configuration` class with `@Bean` methods | `static` extension method on `IServiceCollection` (`AddPersistence`, `AddApplicationSecurity`) |
| `@Bean` method returning an object | `services.AddSingleton<IFoo, Foo>()` or a factory lambda `sp => new Foo(…)` |
| `@Bean` method **parameters** | constructor parameters of the registered class |
| `@ConfigurationProperties` record | `AddOptions<T>().Bind(section)` → injected as `IOptions<T>` |
| `@ConfigurationPropertiesScan` | nothing — each options class is bound by hand |
| auto-configuration | an explicit `Add…` call (`AddDbContext`, `AddCors`, `AddAuthentication`) |
| `@ConditionalOnMissingBean` | `services.TryAdd…` |
| `@Primary` | *no equivalent* — the **last** registration wins silently |
| `@Qualifier("name")` | keyed services: `AddKeyedScoped<T>("name")` + `[FromKeyedServices("name")]` |
| injecting `List<Foo>` | injecting `IEnumerable<IFoo>` — every registration of that type |
| `ObjectProvider<Foo>` / `@Lazy` | injecting `IServiceProvider` and resolving on demand |
| `@Scope("prototype")` | `AddTransient<T>()` |
| `@RequestScope` | `AddScoped<T>()` |

Two rows deserve a warning.

**`@Primary` has no counterpart.** In Spring, two candidate beans for one injection point is an error
(`NoUniqueBeanDefinitionException`) unless one is marked `@Primary` — the ambiguity is reported. In ASP.NET,
registering `IUserRepository` twice is legal and silent: `GetService<IUserRepository>()` returns the last
registration, and only `GetServices<IUserRepository>()` reveals that there were two. `JwtApplicationFactory`
relies on exactly this to replace the `DbContext` — and has to call `RemoveDbContextRegistrations` first anyway,
because `AddDbContext` adds several descriptors rather than one.

**`@Bean` method parameters map to constructor parameters, not to method parameters.** Spring lets a factory
method ask for its collaborators:

```java
@Bean
public CorsConfigurationSource corsConfigurationSource(CorsProperties corsProperties) { … }
```

ASP.NET extension methods are plain statics — they receive `IServiceCollection` and whatever you hand them, and
nothing is injected into them. That is the whole reason `AddPersistence` and `AddApplicationSecurity` take
`IConfiguration` as an explicit parameter: at registration time there is no provider to ask.

---

## 4. Lifetimes — the difference that actually shapes the code

This is where the two models diverge most, and it is visible in both codebases.

### Spring: one singleton graph, with a proxy at the bottom

Every bean in the Spring project is a singleton. `AuthService`, `CustomUserDetailsService`, `TokenService`,
`AuthController`, even `UserRepository` — one instance each, created at start-up, shared by every request.

That works because the per-request state is hidden one level further down. The Spring Data repository proxy
holds an `EntityManager` that is itself a proxy, and that proxy delegates to whichever persistence context is
bound to the current thread's transaction. The unit of work is per-transaction, but nothing above it has to
know: `AuthService` can be a singleton and still get a correct, isolated `EntityManager` on every call.

### ASP.NET Core: the scope propagates upwards

`AppDbContext` is not a proxy. It is an ordinary object with a change tracker, and it must not be shared between
requests — so `AddDbContext` registers it **scoped**, one per HTTP request. And a lifetime constrains everything
above it:

```
AppDbContext (scoped)
  ↑ injected into
UserRepository → must be scoped
  ↑ injected into
AuthService    → must be scoped
```

That chain is why `Program.cs` line 30 says `AddScoped<AuthService>()` and not `AddSingleton`. A singleton
holding a scoped dependency is a **captive dependency**: the scoped object would be captured for the lifetime of
the application, quietly outliving the request it belonged to. The container detects this and throws — the check
(`ValidateScopes`) is on by default in the Development environment.

Spring has the same hazard in principle (a singleton holding a prototype bean) and does *not* check for it; the
usual answer is `ObjectProvider` or method injection. It simply comes up less, because the one thing that is
genuinely per-request — the persistence context — was already solved by a proxy.

### Consequences you can see in this project

| | Spring | C# |
|---|---|---|
| `AuthService` | singleton | **scoped** |
| Repository | singleton proxy | **scoped** |
| Controller / endpoint | singleton `@RestController` | no instance at all — a delegate |
| Password hasher | singleton `PasswordEncoder` bean | singleton `IPasswordHasher` |
| Token issuer | singleton `@Service` | singleton `ITokenService` |
| Start-up work needing the DB | a `@Transactional` bean or auto-configured `ddl-auto` | an explicit scope: `app.Services.CreateScope()` |

The last row is the one that surprises people coming from Spring. `MigrateDatabase` cannot simply ask
`app.Services` for the context, because `app.Services` is the *root* provider and the context is scoped:

```csharp
public static WebApplication MigrateDatabase(this WebApplication app)
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    return app;
}
```

In Spring the equivalent work is invisible: `spring.jpa.hibernate.ddl-auto: update` lets the auto-configured
`EntityManagerFactory` create the schema while the context starts. Neither line of `Program.cs` has an
equivalent in the Spring source, because there is nothing to write.

---

## 5. Configuration as an injectable dependency

Both projects refuse to read raw configuration inside a service. Both bind it into a typed object first.

```java
// Spring — a record, bound through its canonical constructor
@ConfigurationProperties(prefix = "app.jwt")
public record JwtProperties(String secret, String issuer, long expirationMs) { }
```

```csharp
// C# — a mutable class, bound property by property
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] [MinLength(MinimumSecretLength)] public string Secret { get; set; } = string.Empty;
    [Required] [Url]                            public string Issuer { get; set; } = string.Empty;
    [Range(1, long.MaxValue)]                   public long ExpirationMs { get; set; }
}
```

```csharp
services.AddOptions<JwtOptions>()
    .Bind(configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

| | Spring | C# |
|---|---|---|
| Registration | `@ConfigurationPropertiesScan` finds it | one `AddOptions<T>().Bind(...)` call per class |
| Shape | `record` — immutable, constructor binding | `class` or `record` with settable/`init` properties — the *options factory* needs a parameterless constructor |
| Name matching | relaxed: `expiration-ms` → `expirationMs` | exact: `ExpirationMs` → `ExpirationMs` |
| Injected as | `JwtProperties` directly | `IOptions<JwtOptions>`, then `.Value` |
| Validation | `@Validated` + Jakarta constraints (not used here) | `ValidateDataAnnotations()` |
| When validation runs | at context refresh | at `webApplication.Run()`, via `ValidateOnStart()` |

Two things are worth pulling out.

**The shape difference comes from the options factory, not from the binder.** This is worth getting right,
because the obvious explanation is wrong. `Microsoft.Extensions.Configuration`'s binder *can* bind through a
constructor — `configuration.GetSection("Jwt").Get<PositionalRecord>()` works, and has since .NET 6. What cannot
take a positional record is the options pattern around it: `OptionsFactory<TOptions>` is declared
`where TOptions : class, new()` and creates the instance before handing it to the binder, so

```csharp
public record JwtOptions(string Secret, string Issuer, long ExpirationMs);   // ← positional
services.AddOptions<JwtOptions>().Bind(configuration.GetSection("Jwt"));
```

fails at the first resolution with `MissingMethodException: Cannot dynamically create an instance of type
'JwtOptions'. Reason: No parameterless constructor defined.`

A **nominal** record does work, because it has an implicit parameterless constructor and the binder can reach
`init` accessors by reflection:

```csharp
public record JwtOptions
{
    [Required] [MinLength(MinimumSecretLength)] public string Secret { get; init; } = string.Empty;
    …
}
```

So the split is narrower than it looks: C# cannot express the *positional* Java-record shape as an options
class, but the nominal form is available — which makes the choice a style decision rather than the hard
constraint it is usually described as.

`JwtOptions` and `CorsOptions` are plain classes anyway, and the reason is worth recording, because the record
version was written and then withdrawn. Nothing in this project compares options for equality or clones them
with a `with` expression, so two of the three record features are dead weight. The third is actively unhelpful:
the generated `ToString` prints every property, which would put the signing secret into any log line or
exception message that formats `JwtOptions` — suppressing that took a hand-written `PrintMembers` override plus
the tests to guard it, roughly fifty lines defending against a hazard the record itself introduced.

What the exercise did leave behind is `init` instead of `set`. That needs no record at all, and is what both
options classes use today:

```csharp
public class JwtOptions
{
    [Required] [MinLength(MinimumSecretLength)] public string Secret { get; init; } = string.Empty;
    …
}
```

Records remain the right shape elsewhere in this project — `AuthResponse`, `CurrentUserResponse`, `ApiError` and
the request DTOs are all records, because those genuinely are compared and serialised. An options class is a
container that is filled once and only read.

(One caveat either way: the source-generated configuration binder used for trimmed and AOT-published
applications assigns properties directly, which `init` accessors do not allow from outside the type — verify
before enabling `EnableConfigurationBindingGenerator`.)

**The `IOptions<T>` wrapper buys reloading and named options.** Spring injects the properties object itself;
ASP.NET injects a wrapper you have to unwrap with `.Value`. The wrapper exists because there are three variants:
`IOptions<T>` (computed once, singleton), `IOptionsSnapshot<T>` (recomputed per scope, so a changed
`appsettings.json` takes effect), and `IOptionsMonitor<T>` (push notifications on change). This project uses the
plain `IOptions<T>` everywhere — a JWT secret that changes while the application runs is not a feature anyone
wants. Spring's counterpart is `@RefreshScope` from Spring Cloud, which is not part of Boot itself.

**Start-up validation is opt-in on both sides, and only C# opted in.** `ValidateOnStart()` is why a secret
shorter than 256 bits fails `dotnet run`. `JwtProperties` carries no constraints today; adding `@Validated`
plus `@NotBlank`/`@Size` would close the gap ([`COMPARISON.md`](COMPARISON.md) §3.6).

---

## 6. Configuring objects the framework owns

Neither application constructs its own JWT verifier or CORS filter. Both have to hand configuration to
infrastructure the framework creates — and here the two models are genuinely different in kind.

**Spring: you produce the object, and its collaborators are injected into the factory method.**

```java
@Bean
public CorsConfigurationSource corsConfigurationSource(CorsProperties corsProperties) {
    var configuration = new CorsConfiguration();
    configuration.setAllowedOrigins(corsProperties.allowedOrigins());
    …
    return source;
}

@Bean
public SecurityFilterChain securityFilterChain(HttpSecurity http,
                                               CorsConfigurationSource corsConfigurationSource,
                                               DaoAuthenticationProvider authenticationProvider,
                                               JwtAuthenticationConverter jwtAuthenticationConverter) { … }
```

The filter chain is itself a bean, built from other beans. Spring Boot's security auto-configuration sees that a
`SecurityFilterChain` exists and backs off; the same is true of `JwtConfiguration`'s `JwtDecoder`, which is why
no `spring.security.oauth2.resourceserver.*` property is needed.

**ASP.NET Core: the framework produces the object, and you register a callback that mutates it.**

```csharp
internal sealed class ConfigureCorsPolicy(IOptions<CorsOptions> corsOptions) : IConfigureOptions<AspNetCorsOptions>
{
    public void Configure(AspNetCorsOptions options) =>
        options.AddPolicy(SecurityConfiguration.CorsPolicyName, policy => policy
            .WithOrigins(corsOptions.Value.AllowedOrigins)
            …);
}
```

`ConfigureCorsPolicy` never returns anything. It is a class the container builds — with its own injected
dependencies, exactly like a bean — whose `Configure` method the options factory calls when the framework first
assembles its `CorsOptions`. `ConfigureJwtBearerOptions` does the same for the bearer handler, implementing
`IConfigureNamedOptions<JwtBearerOptions>` because authentication options are keyed by scheme name.

| | Spring | C# |
|---|---|---|
| Unit of configuration | a `@Bean` method returning the object | an `IConfigureOptions<T>` implementation mutating it |
| Its dependencies | method parameters | constructor parameters |
| Overriding the framework default | define the bean; auto-configuration backs off | the framework's object is always created; you adjust it |
| Composing several | not directly — one bean wins | every registered `IConfigureOptions<T>` runs, in order |

The last row is a real difference. Two `@Bean` methods for the same type are a conflict; two
`IConfigureOptions<JwtBearerOptions>` registrations both run, each layering its changes onto the same object.
That is how a library and an application can both contribute to the same options without knowing about each
other — and also why a mistake there is harder to spot.

Note that both projects arrived at the same *shape* independently: a class that takes its own validated
properties as a dependency and hands them to framework infrastructure. `corsConfigurationSource(CorsProperties)`
and `ConfigureCorsPolicy(IOptions<CorsOptions>)` are the same idea in two dialects.

---

## 7. Injection at the web layer

This is the place where the C# project stops resembling Spring at all.

**Spring separates the two kinds of parameter.** A controller is a bean; its collaborators arrive through the
constructor. Handler-method parameters are bound from the *request*:

```java
@RestController
@RequestMapping(AuthController.BASE_PATH)
@RequiredArgsConstructor
public class AuthController {
    private final AuthService authService;                 // ← from the container

    @PostMapping(REGISTER_PATH)
    public ResponseEntity<AuthResponse> register(@Valid @RequestBody RegisterRequest request) { … }
                                              //  ↑ from the request body
}
```

**Minimal APIs have no class, so both kinds share one parameter list:**

```csharp
group.MapPost(RegisterPath,
    async (RegisterRequest request, AuthService authService, CancellationToken cancellationToken) => …);
```

| Parameter | Bound from |
|---|---|
| `RegisterRequest` | the JSON body — a complex type the container does not know |
| `AuthService` | the request scope — because `Program.cs` registered it |
| `CancellationToken` | `HttpContext.RequestAborted` |
| `ClaimsPrincipal` (in `UserEndpoints`) | `HttpContext.User` — the claims of the validated token |

The rule for row 2 is that the framework asks the container, via `IServiceProviderIsService`, whether the type is
registered. Spring's equivalent decision is made by annotation (`@RequestBody`, `@AuthenticationPrincipal`,
`@PathVariable`) or by the parameter being a known type; `@Autowired` never appears on a handler parameter.

Two consequences:

* **No `[FromServices]` needed** — but the flip side is that deleting `.AddScoped<AuthService>()` turns a DI
  parameter into an attempted body deserialisation instead of producing a clear error.
* **Nothing is a singleton here.** Spring's controller instance is shared and must be stateless; the C# delegate
  is a static lambda with no instance at all, which removes the question.

The `@RestControllerAdvice` / `IExceptionHandler` pair follows the same pattern. Spring finds
`ApiExceptionHandler` by scanning and dispatches on `@ExceptionHandler` method signatures; C# registers it
explicitly with `AddExceptionHandler<ApiExceptionHandler>()` and dispatches inside a `switch` expression, with
`UseExceptionHandler()` pulling the chain back out of the container.

---

## 8. The repository: generated vs. written

```java
public interface UserRepository extends JpaRepository<User, Long> {
    Optional<User> findByEmail(String email);
    boolean existsByEmail(String email);
}
```

No implementation exists in the Spring source. At start-up, Spring Data builds a proxy from the interface,
derives SQL from the method names, and registers it as a bean.

EF Core has nothing comparable, so the C# side writes it out — `UserRepository(AppDbContext dbContext)` with
three LINQ queries — and registers it with `services.AddScoped<IUserRepository, UserRepository>()`.

From a DI perspective this is the widest gap in the two projects: one bean that exists without any code, versus
a class *and* an interface *and* a registration. The interface is kept on the C# side anyway, mirroring the
Spring structure and giving `AuthServiceTests` something to substitute. `AppDbContext` itself is registered by
type, without an interface, because there is nothing to substitute — the tests replace its *options*, not the
class.

---

## 9. When the wiring is wrong

The most practical difference, and the one worth internalising before switching between the two.

| Failure | Spring Boot | ASP.NET Core |
|---|---|---|
| Dependency not registered | `NoSuchBeanDefinitionException` at context refresh — the application does not start | `InvalidOperationException: Unable to resolve service for type …` — see below |
| Two candidates for one type | `NoUniqueBeanDefinitionException` unless `@Primary`/`@Qualifier` | **silent** — the last registration wins |
| Circular dependency | detected; fails for constructor injection, resolvable with `@Lazy` | always throws, no escape hatch |
| Wrong lifetime (captive dependency) | not checked | throws — `ValidateScopes`, on by default in Development |
| Bad configuration value | only if `@Validated` is present | at `webApplication.Run()` if `ValidateOnStart()` was called |

The first row needs the detail. ASP.NET Core does have a start-up check — `ValidateOnBuild`, on by default in
Development — that tries to construct every *registered* service when `webApplicationBuilder.Build()` runs, and
fails there if
one of them cannot be satisfied. What it cannot check is anything that is not a registration: the parameter list
of a minimal API delegate is not a service descriptor, so a missing `AddScoped<AuthService>()` surfaces on the
first request to `/api/auth/register`, not at start-up. And in Production both validations default to off.

Spring has no such hole, because every injection point belongs to a bean definition and the whole context is
built eagerly. The `contextLoads` test in `SpringBootOauth2JwtApplicationTests` is a single `@Test` with an empty
body, and it is a genuine test: if any bean cannot be wired, it fails. The C# equivalent is
`ApplicationTests` plus the endpoint tests actually issuing requests — coverage of the graph comes from
exercising it, not from building it.

---

## 10. Substituting services in tests

**Spring replaces by type, declaratively.** `@MockitoBean` (formerly `@MockBean`) on a test field swaps the bean
in the context for a mock. Neither project needs it here, because the Spring application already runs on an H2
in-memory database in its normal configuration — the test context *is* the production context.

**C# manipulates the service collection as a list.** Its production configuration points at a SQLite *file*, so
the tests must intervene:

```csharp
builder.ConfigureServices(services =>
{
    RemoveDbContextRegistrations(services);
    services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
});
```

`ConfigureServices` runs after `Program.cs` has registered everything, and the last registration wins — but
`AddDbContext` adds several descriptors, so the old ones are removed rather than merely shadowed. There is no
`@MockitoBean`-style shortcut; there is a `List<ServiceDescriptor>` and ordinary LINQ.

For pure unit tests the two converge again. `AuthServiceTest` uses `@Mock` + `@InjectMocks`; `AuthServiceTests`
calls the constructor with fakes. Neither involves a container — which is the point of constructor injection on
both sides, and the reason `AuthService` depends on `IUserRepository` rather than on `AppDbContext`.

| | Spring | C# |
|---|---|---|
| Boot the real application | `@SpringBootTest` | `WebApplicationFactory<Program>` |
| Requires the entry point to be public | no | yes — hence `public partial class Program;` |
| Replace one bean | `@MockitoBean` | remove + re-add descriptors |
| Override configuration | `@TestPropertySource` / `application-test.yaml` | an extra `IConfiguration` source |
| Unit-test a service | `@Mock` + `@InjectMocks` | `new AuthService(fake, fake, fake)` |

---

## 11. What each model buys

**Spring Boot's strengths**
* Far less wiring code. The repository has no implementation; the datasource has no registration.
* The whole graph is validated eagerly, so wiring mistakes are start-up failures, never runtime ones.
* Ambiguity is an error rather than a silent last-wins.
* Adding a service touches exactly one file — its own.

**ASP.NET Core's strengths**
* The container's contents are readable in one place; there is no "why is this bean here" archaeology.
* No classpath scanning at start-up and no reflection-heavy proxying, which is what makes trimming and
  ahead-of-time compilation viable.
* Application classes carry no framework annotations — `AuthService` compiles with no DI reference at all.
* Lifetimes are explicit, and the mismatch that actually bites (a singleton capturing a request-scoped object)
  is detected rather than hidden behind a proxy.

**Where each is weaker**
* Spring: the graph is implicit, auto-configuration is hard to reason about backwards, and proxying means the
  object you injected is often not the object you think you injected.
* ASP.NET: every line is yours to write and to forget, a duplicate registration is silent, and a service missing
  from a minimal API delegate is a runtime error in production.

Neither project fought its framework. The C# side looks like an ASP.NET application and the Java side looks like
a Spring application — which is why the remaining differences say something.

---

## Appendix: translation table

| Spring | ASP.NET Core |
|---|---|
| `ApplicationContext` | `IServiceProvider` |
| bean definition | `ServiceDescriptor` |
| `BeanFactory` registration | `IServiceCollection` |
| `@Service` / `@Component` | `AddScoped<T>()` / `AddSingleton<T>()` / `AddTransient<T>()` |
| `@Configuration` + `@Bean` | extension method on `IServiceCollection` |
| `@Autowired` (constructor) | implicit — the greediest satisfiable constructor |
| `@Qualifier` | keyed services (`AddKeyedScoped`, `[FromKeyedServices]`) |
| `@Primary` | *(none — last registration wins)* |
| `@ConditionalOnMissingBean` | `TryAddScoped` / `TryAddSingleton` / `TryAddEnumerable` |
| `@ConfigurationProperties` | `AddOptions<T>().Bind(section)` → `IOptions<T>` |
| `@Validated` on properties | `.ValidateDataAnnotations().ValidateOnStart()` |
| `@RefreshScope` | `IOptionsSnapshot<T>` / `IOptionsMonitor<T>` |
| `@Scope("prototype")` | `AddTransient<T>()` |
| `@RequestScope` | `AddScoped<T>()` |
| `ObjectProvider<T>` | `IServiceProvider.GetService<T>()` |
| `List<T>` injection | `IEnumerable<T>` injection |
| `@PostConstruct` | constructor, or `IHostedService.StartAsync` |
| `DisposableBean` / `@PreDestroy` | `IDisposable` / `IAsyncDisposable` |
| `SmartInitializingSingleton` | `IHostedService` |
| `@SpringBootTest` | `WebApplicationFactory<TEntryPoint>` |
| `@MockitoBean` | remove and re-add a `ServiceDescriptor` |
| `SecurityFilterChain` bean | middleware order in `Program.cs` + `IConfigureOptions<T>` |
