using System.Net;
using c_sharp_jwt.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace c_sharp_jwt.Tests;

/// <summary>
/// The counterpart of a Spring <c>contextLoads</c> test: it proves that the whole composition root — options
/// validation included — starts up and serves requests.
/// </summary>
public class ApplicationTests(JwtApplicationFactory factory) : IClassFixture<JwtApplicationFactory>
{
    private const string MeUrl = "/api/users/me";

    [Fact]
    public async Task WhenTheApplicationStarts_ThenItServesRequests()
    {
        // given
        var client = factory.CreateClient();

        // when
        var response = await client.GetAsync(MeUrl);

        // then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void WhenTheApplicationStarts_ThenTheJwtOptionsAreBoundAndValidated()
    {
        // given
        using var scope = factory.Services.CreateScope();

        // when
        var jwtOptions = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;

        // then
        Assert.Equal(TestFixtures.TestIssuer, jwtOptions.Issuer);
        Assert.Equal(TestFixtures.ValidExpirationMs, jwtOptions.ExpirationMs);
        Assert.True(jwtOptions.Secret.Length >= JwtOptions.MinimumSecretLength);
    }

    [Fact]
    public void WhenTheApplicationStarts_ThenAnEndpointWithoutItsOwnRuleRequiresAnAuthenticatedUser()
    {
        // given
        using var scope = factory.Services.CreateScope();

        // when
        var fallbackPolicy = scope.ServiceProvider
            .GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy;

        // then
        Assert.NotNull(fallbackPolicy);
        Assert.Contains(fallbackPolicy.Requirements, requirement =>
            requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void WhenTheApplicationStarts_ThenTheConfiguredCorsOriginIsBound()
    {
        // given
        using var scope = factory.Services.CreateScope();

        // when
        var corsOptions = scope.ServiceProvider.GetRequiredService<IOptions<CorsOptions>>().Value;

        // then
        Assert.Equal([TestFixtures.AllowedOrigin], corsOptions.AllowedOrigins);
    }
}
