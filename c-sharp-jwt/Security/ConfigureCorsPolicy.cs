using Microsoft.Extensions.Options;
using AspNetCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

namespace c_sharp_jwt.Security;

internal sealed class ConfigureCorsPolicy(IOptions<CorsOptions> corsOptions) : IConfigureOptions<AspNetCorsOptions>
{
    public void Configure(AspNetCorsOptions options) =>
        options.AddPolicy(SecurityConfiguration.CorsPolicyName, policy => policy
            .WithOrigins(corsOptions.Value.AllowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
}
