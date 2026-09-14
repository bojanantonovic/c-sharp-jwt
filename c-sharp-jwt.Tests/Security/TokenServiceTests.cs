using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using c_sharp_jwt.Security;

namespace c_sharp_jwt.Tests.Security;

public class TokenServiceTests
{
    [Fact]
    public async Task WhenGenerateToken_ThenTokenCarriesSubjectIssuerAndExpiry()
    {
        // given
        var tokenService = JwtTestSupport.TokenService();

        // when
        var token = tokenService.GenerateToken(TestFixtures.TestEmail, [TestFixtures.UserRole]);

        // then
        var validated = await JwtTestSupport.ValidatedAsync(token);
        var securityToken = validated.SecurityToken;
        Assert.Equal(TestFixtures.TestEmail, validated.ClaimsIdentity.Name);
        Assert.Equal(TestFixtures.TestIssuer, securityToken.Issuer);
        Assert.Equal(securityToken.ValidFrom.AddMilliseconds(TestFixtures.ValidExpirationMs), securityToken.ValidTo);
    }

    [Fact]
    public async Task GivenRoles_WhenGenerateToken_ThenTheyBecomeRolesOfThePrincipal()
    {
        // given
        var tokenService = JwtTestSupport.TokenService();

        // when
        var token = tokenService.GenerateToken(TestFixtures.TestEmail,
                                               [TestFixtures.UserRole, TestFixtures.AdminRole]);

        // then
        var principal = new ClaimsPrincipal((await JwtTestSupport.ValidatedAsync(token)).ClaimsIdentity);
        Assert.True(principal.IsInRole(TestFixtures.UserRole));
        Assert.True(principal.IsInRole(TestFixtures.AdminRole));
    }

    [Fact]
    public async Task GivenNoRoles_WhenGenerateToken_ThenThePrincipalHasNoRoleClaim()
    {
        // given
        var tokenService = JwtTestSupport.TokenService();

        // when
        var token = tokenService.GenerateToken(TestFixtures.TestEmail, []);

        // then
        var claimsIdentity = (await JwtTestSupport.ValidatedAsync(token)).ClaimsIdentity;
        Assert.Empty(claimsIdentity.FindAll(JwtConventions.RolesClaim));
    }

    [Fact]
    public async Task GivenTokenIssuedWithAnotherSecret_WhenValidatedWithTheConfiguredSecret_ThenTokenIsRejected()
    {
        // given
        var foreignToken = JwtTestSupport.TokenService(secret: TestFixtures.OtherSecret)
            .GenerateToken(TestFixtures.TestEmail, [TestFixtures.UserRole]);

        // when
        var failure = await JwtTestSupport.ValidationFailureAsync(foreignToken);

        // then
        Assert.IsAssignableFrom<SecurityTokenInvalidSignatureException>(failure);
    }
}
