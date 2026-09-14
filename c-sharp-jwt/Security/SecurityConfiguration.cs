using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AspNetCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

namespace c_sharp_jwt.Security;

public static class SecurityConfiguration
{
    public const string CorsPolicyName = "AngularClient";

    public static IServiceCollection AddApplicationSecurity(this IServiceCollection services,
                                                            IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        services.AddSingleton<IConfigureOptions<AspNetCorsOptions>, ConfigureCorsPolicy>();
        services.AddCors();

        services.ConfigureOptions<ConfigureJwtBearerOptions>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Deny by default: an endpoint that declares no authorization of its own needs an authenticated user,
        // so forgetting RequireAuthorization on a new endpoint cannot silently make it public.
        services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
                                      .RequireAuthenticatedUser()
                                      .Build());

        return services;
    }
}
