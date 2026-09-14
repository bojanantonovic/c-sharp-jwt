# `spring-boot-oauth2-jwt` vs. `c-sharp-jwt`

Two implementations of the same thing: stateless JWT authentication for a REST API, with registration, login and
one protected endpoint. One is built on Spring Boot 4 / Spring Security 7, the other on ASP.NET Core 10.

They were kept deliberately close, so that the remaining differences are informative: each one is either a
framework constraint or a conscious choice, never an accident. This document lists both.

* Spring: `IdeaProjects/spring-boot-oauth2-jwt`
* C#: `RiderProjects/c-sharp-jwt`

---

## 1. At a glance

|                     | `spring-boot-oauth2-jwt`                              | `c-sharp-jwt`                                 |
|---------------------|-------------------------------------------------------|-----------------------------------------------|
| Language / runtime  | Java 25                                               | C# / .NET 10                                  |
| Framework           | Spring Boot 4.1, Spring Security 7                    | ASP.NET Core 10                               |
| HTTP layer          | Spring MVC `@RestController`                          | Minimal API endpoints                         |
| Token verification  | `spring-boot-starter-security-oauth2-resource-server` | `Microsoft.AspNetCore.Authentication.JwtBearer` |
| JOSE library        | Nimbus (via the resource server starter)              | `Microsoft.IdentityModel.JsonWebTokens`       |
| Persistence         | Spring Data JPA / Hibernate                           | EF Core                                       |
| Database            | H2, in memory, `ddl-auto: update`                     | SQLite file, EF Core migrations               |
| Password hashing    | `BCryptPasswordEncoder`                               | `BCrypt.Net-Next`                             |
| Validation          | Jakarta Bean Validation                               | `System.ComponentModel.DataAnnotations`       |
| Boilerplate removal | Lombok, records                                       | records, primary constructors                 |
| Tests               | JUnit 5, Mockito, AssertJ, MockMvc — 27 tests         | xUnit, Moq, `WebApplicationFactory` — 42 tests |
| Port                | 8080                                                  | 8080                                          |

---

## 2. What is identical

### 2.1 The HTTP contract

| Method | Path                 | Access       | Success   |
|--------|----------------------|--------------|-----------|
| `POST` | `/api/auth/register` | public       | `201`     |
| `POST` | `/api/auth/login`    | public       | `200`     |
| `GET`  | `/api/users/me`      | bearer token | `200`     |

Same request bodies, same error codes, same error body:

```jsonc
// POST /api/auth/register  ->  201
{ "token": "eyJ…", "tokenType": "Bearer", "email": "jane.doe@example.com" }

// 409 Conflict, 401 Unauthorized
{ "timestamp": "…", "status": 409, "message": "Email already in use: jane.doe@example.com" }

// 400 Bad Request
{ "timestamp": "…", "status": 400, "message": "Validation failed",
  "errors": { "email": "must be a well-formed email address" } }
```

The validation messages are spelled identically on both sides (`must not be blank`,
`must be a well-formed email address`, `Password must be at least 8 characters long`), even though the two
validation frameworks have different defaults.

Rejecting a bearer token produces the same RFC 6750 response in both: `401` with
`WWW-Authenticate: Bearer error="invalid_token"`, and a bare `Bearer` challenge when no token was sent at all.

### 2.2 The token

Same algorithm (HS256, shared secret), same claim set, same custom `roles` claim:

```json
{ "iss": "http://localhost:8080", "iat": 1789396825, "exp": 1789400425,
  "sub": "jane.doe@example.com", "roles": ["User"] }
```

### 2.3 The architecture

Both projects make the same five decisions, and for the same reasons:

1. **The framework verifies the token, not application code.** No hand-written filter or middleware, so a token
   that cannot be parsed cannot turn a `401` into a `500`, and the RFC 6750 error responses come for free.
2. **The token is self-contained.** An authenticated request never touches the database. Both projects document
   the price of that with a test proving a deleted user's token is still accepted until it expires.
3. **One conventions class.** `JwtConventions` holds what token creation and token validation must agree on, so
   the two ends cannot drift apart.
4. **One mapper from user to claims**, so a token issued right after registration carries the same claims as one
   issued after a login.
5. **Package by feature** — `security`, `auth`, `user(s)`, `common` — not by technical layer.

### 2.4 Test structure

Both suites are written Given-When-Then, name their tests `given…_when…_then…`, share a `TestFixtures` holder,
and share a `JwtTestSupport` that builds the *production* encoder/validation parameters rather than a look-alike
configuration.

---

## 3. What differs

### 3.1 Roles: prefix vs. no prefix

This is the one difference visible on the wire.

Spring Security models a role as a `GrantedAuthority` with a `ROLE_` prefix. `TokenService` strips the prefix
when writing the claim and `JwtGrantedAuthoritiesConverter` puts it back when reading, so the claim holds
`"USER"` while the authority is `"ROLE_USER"`.

ASP.NET Core has no prefix convention: a role claim carries the bare name. `JwtConventions.RolesClaim` is wired
up as `TokenValidationParameters.RoleClaimType`, which is all `User.IsInRole(...)` and `RequireRole(...)` need.

|                        | Spring                             | C#                    |
|------------------------|------------------------------------|-----------------------|
| `roles` claim          | `["USER"]`                         | `["User"]`            |
| Internal representation| `ROLE_USER` authority              | `User` role claim     |
| Mapping code needed    | `JwtGrantedAuthoritiesConverter`   | one property assignment |

Consequently `/api/users/me` is the one response that is not byte-identical:

```jsonc
// Spring
{ "email": "jane.doe@example.com", "authorities": ["ROLE_USER", "FACTOR_BEARER"] }

// C#
{ "email": "jane.doe@example.com", "roles": ["User"] }
```

`FACTOR_BEARER` is an authority Spring Security 7 adds to every bearer-authenticated request; ASP.NET Core has
no equivalent concept.

### 3.2 How the password is checked

| Spring                                                      | C#                                            |
|-------------------------------------------------------------|-----------------------------------------------|
| `AuthenticationManager` → `DaoAuthenticationProvider` → `CustomUserDetailsService` → `PasswordEncoder` | `IUserRepository` → `IPasswordHasher`         |
| Failure: `BadCredentialsException` (framework)              | Failure: `InvalidCredentialsException` (own)  |
| `UserDetailsMapper` produces a `UserDetails`                | `UserRolesMapper` produces role names         |

Spring reuses its authentication pipeline, which is why it needs a `UserDetailsService` and a `UserDetails`
object. ASP.NET Core has no comparable password-authentication pipeline outside ASP.NET Core Identity, which
brings its own user store, schema and cookie-shaped sign-in model — far too much for three endpoints. The C#
side therefore verifies the hash directly, which also means it needs no `UserDetails` equivalent: the mapper
only has to produce the role names the token carries.

Both raise the same exception for an unknown email and for a wrong password, so a caller cannot tell them apart.

### 3.3 Request validation

| Spring                                      | C#                                                 |
|---------------------------------------------|----------------------------------------------------|
| `@Valid` on the controller parameter        | `ValidationFilter<T>` endpoint filter              |
| `MethodArgumentNotValidException`           | filter returns the error response directly         |
| handled in `@RestControllerAdvice`          | shape shared with the handler via `ValidationApiError` |
| `@NotBlank` built in                        | `NotBlankAttribute` written by hand                |

`[Required]` accepts a whitespace-only string, unlike Jakarta's `@NotBlank`, so the C# side adds a small
`NotBlankAttribute` to keep the behaviour — and the message — the same.

The C# filter returns the response instead of throwing, because using exceptions for an expected outcome is
idiomatic in Spring but not in .NET. Both keep the error shape in exactly one place.

### 3.4 Error handling

| Spring                          | C#                                              |
|---------------------------------|-------------------------------------------------|
| `@RestControllerAdvice` with `@ExceptionHandler` per exception type | `IExceptionHandler` with a `switch` expression |
| Returns `ResponseEntity<Map<String, Object>>` | Returns typed `ApiError` / `ValidationApiError` records |

The C# version returns declared record types rather than an ad-hoc map, which the integration tests then
deserialize back into.

### 3.5 Persistence

| Spring                                        | C#                                                   |
|-----------------------------------------------|------------------------------------------------------|
| `UserRepository extends JpaRepository`        | `IUserRepository` + hand-written `UserRepository`     |
| Query methods derived from names, no implementation | LINQ over `AppDbContext`                       |
| H2 in memory, schema from `ddl-auto: update`  | SQLite file, schema from EF Core migrations           |
| `@Entity` / `@Table` / `@Column` annotations  | Fluent configuration in `OnModelCreating`             |

Spring Data generates the repository; EF Core has no equivalent, so the C# repository is written out. It is kept
anyway — rather than injecting `AppDbContext` into `AuthService` directly — because it mirrors the Spring
structure and lets `AuthServiceTests` run against a mock instead of a database.

The database choice differs because the ecosystems differ: an H2 in-memory database is the default throwaway
database in the Spring world, while EF Core's story is migrations plus a real provider. The C# tests still get an
in-memory database — SQLite, opened per test class.

### 3.6 Configuration and start-up validation

| Spring                                    | C#                                                       |
|-------------------------------------------|----------------------------------------------------------|
| `application.yaml`                        | `appsettings.json`                                       |
| `@ConfigurationProperties` records        | options classes bound with `AddOptions<T>().Bind(...)`    |
| relaxed binding (`expiration-ms`)         | exact names (`ExpirationMs`)                              |
| no validation on the properties           | `ValidateDataAnnotations().ValidateOnStart()`             |

The C# side additionally fails the application at start-up on a missing issuer or a secret shorter than 256 bits,
instead of failing on the first request. The Spring side has no equivalent check today — adding `@Validated` plus
constraints to `JwtProperties` would be the way to close that gap.

### 3.7 Wiring

| Spring                                       | C#                                                     |
|----------------------------------------------|--------------------------------------------------------|
| `@Configuration` classes with `@Bean` methods | extension methods on `IServiceCollection`              |
| beans injected as method parameters           | `IConfigureOptions<T>` implementations                 |
| `SecurityFilterChain` bean describes the chain| `SecurityConfiguration.AddApplicationSecurity` + middleware order in `Program.cs` |

`ConfigureJwtBearerOptions` and `ConfigureCorsPolicy` exist so the `Jwt` and `Cors` sections are bound exactly
once and the already-validated options flow into the handler — the ASP.NET equivalent of injecting a bean rather
than calling its factory method.

### 3.8 Security defaults

Both projects deny by default, but they say so in different places.

Spring's filter chain ends with `.requestMatchers("/**").authenticated()`, after `/api/auth/**` and the H2
console have been permitted: the rules are one ordered list, and the last one catches everything else.

ASP.NET Core has no such list. `SecurityConfiguration` sets `AuthorizationOptions.FallbackPolicy` to
`RequireAuthenticatedUser()`, which applies to every endpoint that declares no authorization of its own, and each
endpoint group then states its own rule — `/api/auth` is `AllowAnonymous()`, `/api/users` is
`RequireAuthorization()`. An endpoint added without either is protected rather than public.

### 3.9 Things that exist on only one side

**Spring only**

* The H2 console at `/h2-console`, explicitly permitted in the filter chain and needing `frameOptions(sameOrigin)`.
* Explicit `csrf().disable()` and `SessionCreationPolicy.STATELESS` — ASP.NET Core minimal APIs have neither
  sessions nor CSRF tokens to switch off.
* Lombok (`@Getter`, `@Builder`, `@RequiredArgsConstructor`) and JSpecify nullability annotations; C# has records,
  primary constructors and nullable reference types in the language.

**C# only**

* An explicit `ClockSkew` of 60 seconds. Spring's default is already 60 s, .NET's is 300 s — long enough to let a
  token the issuer considers expired still pass, so it is set explicitly.
* `ValidAlgorithms`, restricting verification to HS256. Spring's `NimbusJwtDecoder.withSecretKey(...)` already
  pins the algorithm.
* `nbf` in the issued token. Spring's `TokenService` writes only `iss`, `iat`, `exp`, `sub` and `roles`.
* `MapInboundClaims = false`, which stops the handler from renaming `sub` to its WS-Federation URI. Nimbus has no
  such legacy mapping.
* EF Core migrations and `PersistenceConfiguration`.

---

## 4. Type-by-type map

| Concern                        | Spring                                       | C#                                           |
|--------------------------------|----------------------------------------------|----------------------------------------------|
| Entry point                    | `SpringBootOauth2JwtApplication`             | `Program.cs`                                 |
| JWT settings                   | `JwtProperties`                              | `JwtOptions`                                 |
| CORS settings                  | `CorsProperties`                             | `CorsOptions`                                |
| Shared JWT constants           | `JwtConventions`                             | `JwtConventions`                             |
| Encoder / decoder wiring       | `JwtConfiguration`                           | `JwtConfiguration` + `ConfigureJwtBearerOptions` |
| Filter chain / CORS / hashing  | `SecurityConfiguration`                      | `SecurityConfiguration` + `ConfigureCorsPolicy` |
| Token issuing                  | `TokenService`                               | `ITokenService` / `TokenService`             |
| User → claims                  | `UserDetailsMapper`                          | `UserRolesMapper`                            |
| Loading a user to log in       | `CustomUserDetailsService`                   | *(none — `IUserRepository` is used directly)* |
| Password hashing               | `PasswordEncoder` bean                       | `IPasswordHasher` / `BCryptPasswordHasher`   |
| Auth HTTP layer                | `AuthController`                             | `AuthEndpoints`                              |
| Auth logic                     | `AuthService`                                | `AuthService`                                |
| Duplicate email                | `EmailAlreadyInUseException`                 | `EmailAlreadyInUseException`                 |
| Bad credentials                | `BadCredentialsException` *(framework)*      | `InvalidCredentialsException`                |
| Auth DTOs                      | `RegisterRequest`, `LoginRequest`, `AuthResponse` | same names                              |
| User HTTP layer                | `UserController`                             | `UserEndpoints`                              |
| Current-user DTO               | `CurrentUserResponse(email, authorities)`    | `CurrentUserResponse(Email, Roles)`          |
| Entity / enum                  | `User`, `Role`                               | `User`, `Role`                               |
| Repository                     | `UserRepository` *(generated)*               | `IUserRepository` / `UserRepository`         |
| Persistence wiring             | *(auto-configuration)*                       | `AppDbContext`, `PersistenceConfiguration`   |
| Error responses                | `ApiExceptionHandler`                        | `ApiExceptionHandler`, `ApiError`, `ValidationApiError` |
| Validation plumbing            | *(`@Valid`, framework)*                      | `ValidationFilter<T>`, `ValidationHelper`, `NotBlankAttribute`, `ValidationMessages` |

---

## 5. Test-by-test map

| Spring                             | C#                          | Covers                                                    |
|------------------------------------|-----------------------------|-----------------------------------------------------------|
| `SpringBootOauth2JwtApplicationTests` | `ApplicationTests`       | the context starts; options are bound and validated        |
| `TokenServiceTest`                 | `TokenServiceTests`         | claims, roles, expiry, foreign secret rejected             |
| `JwtConfigurationTest`             | `JwtConfigurationTests`     | wrong secret / wrong issuer / expired rejected, claim mapping |
| `CustomUserDetailsServiceTest`     | `UserRepositoryTests`       | loading a user, unknown email                              |
| `AuthServiceTest`                  | `AuthServiceTests`          | register, duplicate email, login, bad credentials          |
| `AuthControllerTest`               | `AuthEndpointsTests` + `UserEndpointsTests` | the full HTTP surface                      |
| `JwtTestSupport`                   | `JwtTestSupport`            | production encoder/validation parameters for the tests     |
| `TestFixtures`                     | `TestFixtures`              | shared test data                                           |
| *(none)*                           | `UserRolesMapperTests`, `BCryptPasswordHasherTests`, `TestDatabase`, `JwtApplicationFactory` | hand-written code Spring gets from the framework |

`AuthControllerTest` is split in two on the C# side because the protected-endpoint cases belong to
`UserEndpoints`, not to the auth endpoints. The extra C# test classes cover code that simply does not exist in
the Spring project: the role mapper, the password hasher, and the test host itself.

---

## 6. Running them

```bash
# Spring
cd ~/IdeaProjects/spring-boot-oauth2-jwt
./mvnw spring-boot:run
./mvnw test

# C#
cd ~/RiderProjects/c-sharp-jwt
dotnet run --project c-sharp-jwt
dotnet test
```

Both serve on port 8080 and expect an Angular client on `http://localhost:4200`.
