using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace c_sharp_jwt.Security;

/// <summary>
/// Feeds the validated <see cref="JwtOptions"/> into the bearer handler. Going through
/// <see cref="IConfigureNamedOptions{TOptions}"/> rather than reading the configuration a second time keeps the
/// <c>Jwt</c> section bound in exactly one place.
/// </summary>
internal sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name == JwtBearerDefaults.AuthenticationScheme)
        {
            Configure(options);
        }
    }

    public void Configure(JwtBearerOptions options)
    {
        // Keep the claim names as they are on the wire. With the default mapping, "sub" would be renamed to its
        // WS-Federation URI and the NameClaimType of the validation parameters would no longer match anything.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = JwtConfiguration.ValidationParameters(jwtOptions.Value);
    }
}
