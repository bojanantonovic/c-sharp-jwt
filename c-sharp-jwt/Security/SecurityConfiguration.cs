using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AspNetCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

namespace c_sharp_jwt.Security;

/// <summary>
/// The counterpart of the Spring sibling's <c>SecurityConfiguration</c> and <c>JwtConfiguration</c>, but built
/// the other way round. Spring <em>produces</em> the infrastructure — a <c>SecurityFilterChain</c> bean whose
/// collaborators arrive as method parameters — and the auto-configuration backs off once that bean exists. Here
/// the framework always creates its own <c>JwtBearerOptions</c> and <c>CorsOptions</c>, and this class registers
/// callbacks that adjust them. See DEPENDENCY-INJECTION-COMPARISON.md §6.
/// </summary>
public static class SecurityConfiguration
{
    public const string CorsPolicyName = "AngularClient";

    public static IServiceCollection AddApplicationSecurity(this IServiceCollection serviceCollection,
                                                            IConfiguration configuration)
    {
        serviceCollection.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        serviceCollection.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        serviceCollection.AddSingleton<ITokenService, TokenService>();
        serviceCollection.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // Two spellings of the same registration. The explicit form names the options type being configured;
        // ConfigureOptions<T> finds every IConfigureOptions<>/IPostConfigureOptions<>/IValidateOptions<> that the
        // type implements. Unlike a Spring @Bean method, several of these may target the same options object —
        // each one runs and layers its changes on top, rather than one definition winning.
        serviceCollection.AddSingleton<IConfigureOptions<AspNetCorsOptions>, ConfigureCorsPolicy>();
        serviceCollection.AddCors();

        serviceCollection.ConfigureOptions<ConfigureJwtBearerOptions>();
        serviceCollection.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Deny by default: an endpoint that declares no authorization of its own needs an authenticated user,
        // so forgetting RequireAuthorization on a new endpoint cannot silently make it public.
        serviceCollection.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
                                      .RequireAuthenticatedUser()
                                      .Build());

        return serviceCollection;
    }
}
