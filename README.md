# c-sharp-jwt

Stateless JWT authentication for an ASP.NET Core 10 minimal API, built on the framework's own
`JwtBearer` authentication handler instead of a hand-written JWT middleware.

It is the ASP.NET counterpart of [`spring-boot-oauth2-jwt`](../../IdeaProjects/spring-boot-oauth2-jwt):
same endpoints, same domain model, same token on the wire, same tests — only the framework differs.
[`COMPARISON.md`](COMPARISON.md) lists what the two projects share and where they diverge.

## Endpoints

| Method | Path                 | Access       | Description                                |
|--------|----------------------|--------------|--------------------------------------------|
| `POST` | `/api/auth/register` | public       | Creates a user and returns an access token |
| `POST` | `/api/auth/login`    | public       | Verifies the password and returns a token  |
| `GET`  | `/api/users/me`      | bearer token | Returns the subject and roles of the token |

## How the token handling works

**Issuing** — `TokenService` builds a `SecurityTokenDescriptor` (`iss`, `iat`, `nbf`, `exp`, `sub` and a custom
`roles` claim) and signs it with the `SigningCredentials` from `JwtConfiguration` (`JsonWebTokenHandler`, HS256,
shared secret).

**Validating** — no application code is involved. `SecurityConfiguration` registers
`AddAuthentication(...).AddJwtBearer()`, which installs the framework's `JwtBearerHandler`. It reads the
`Authorization` header, checks the token against the `TokenValidationParameters` from `JwtConfiguration` and
answers with `401` and a `WWW-Authenticate: Bearer error="invalid_token"` header if anything is wrong —
signature, algorithm, `exp`, `nbf` or `iss`.

**Roles** — the `roles` claim is wired up as `TokenValidationParameters.RoleClaimType`, so `User.IsInRole(...)`
and `RequireRole(...)` work on it without any further mapping. `NameClaimType` points at `sub`, which makes
`User.Identity.Name` the email. Both need `MapInboundClaims = false`; otherwise the handler renames the standard
JWT claims to their long WS-Federation URIs.

`JwtConventions` holds the settings that token creation and token validation have to agree on: signature
algorithm, subject claim, roles claim and clock skew.

### Difference to the Spring sibling

Spring Security models a role as a `ROLE_`-prefixed `GrantedAuthority` and strips the prefix when writing the
`roles` claim. ASP.NET Core has no such prefix — a role claim carries the bare name — so the claim holds
`"User"` / `"Admin"` where Spring writes `"USER"` / `"ADMIN"`. The wire format is otherwise identical.

## What this buys over hand-written middleware

* **No middleware to get wrong.** A token that cannot be parsed cannot make an exception escape the pipeline,
  which is the classic way of turning a `401` into a `500`.
* **RFC 6750 error responses**, including the `WWW-Authenticate` challenge, out of the box.
* **Standard claim validation** (`exp`, `nbf`, `iss`, clock skew) instead of a hand-rolled expiry check.
* **Migration path**: pointing the app at a real identity provider (Keycloak, Entra ID, Auth0) mainly means
  setting `JwtBearerOptions.Authority` instead of a symmetric key. Endpoints and authorization rules stay as
  they are.

## The trade-off: no database lookup per request

The token is self-contained, so an authenticated request never touches the database — that is the point, and it
is also the price: a token stays valid until it expires, even if the user has been deleted or locked in the
meantime. `UserEndpointsTests.GivenTokenOfDeletedUser_...` documents exactly that behaviour.

Short token lifetimes plus refresh tokens are the usual answer. If immediate revocation is required, hook into
`JwtBearerEvents.OnTokenValidated` (checking a deny list or the user table) — which brings the per-request
lookup, and its cost, back.

## Project layout

| Folder     | Contents                                                                                |
|------------|-----------------------------------------------------------------------------------------|
| `Security` | `JwtConventions`, `JwtConfiguration`, `SecurityConfiguration`, `TokenService`, hashing   |
| `Auth`     | Registration and login: endpoints, service, DTOs, the exceptions they raise              |
| `Users`    | The `User` entity, its repository, and the `/api/users/me` endpoint                      |
| `Common`   | Error response shapes, the validation endpoint filter and the exception handler          |
| `Data`     | `AppDbContext`, the EF Core migrations and the persistence registrations                 |

## Configuration

```jsonc
{
  "ConnectionStrings": { "Default": "Data Source=c-sharp-jwt.db" },
  "Jwt": {
    "Secret": "…",                    // at least 256 bits for HS256; use an environment variable in production
    "Issuer": "http://localhost:8080", // written as `iss` and enforced on validation; must be a URI
    "ExpirationMs": 3600000
  },
  "Cors": { "AllowedOrigins": [ "http://localhost:4200" ] }
}
```

Both sections are bound with `ValidateDataAnnotations().ValidateOnStart()`, so a missing or too short secret
fails the application at start-up rather than on the first request.

## Running

```bash
dotnet run --project c-sharp-jwt
dotnet test
```

A SQLite database file is used; `Database.Migrate()` creates and updates it on start-up. The tests run against a
private in-memory SQLite database instead and never touch that file.
