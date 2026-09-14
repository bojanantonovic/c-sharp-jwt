using c_sharp_jwt.Security;
using Microsoft.IdentityModel.Tokens;

namespace c_sharp_jwt.Tests.Security;

public class JwtConfigurationTests
{
    [Fact]
    public async Task GivenTokenSignedWithAnotherSecret_WhenValidated_ThenTokenIsRejected()
    {
        // given
        var foreignToken = JwtTestSupport.TokenService(secret: TestFixtures.OtherSecret)
            .GenerateToken(TestFixtures.TestEmail, [TestFixtures.UserRole]);

        // when
        var failure = await JwtTestSupport.ValidationFailureAsync(foreignToken);

        // then
        Assert.IsAssignableFrom<SecurityTokenInvalidSignatureException>(failure);
    }

    [Fact]
    public async Task GivenTokenOfAnotherIssuer_WhenValidated_ThenTokenIsRejected()
    {
        // given
        var foreignToken = JwtTestSupport.TokenService(issuer: TestFixtures.OtherIssuer)
            .GenerateToken(TestFixtures.TestEmail, [TestFixtures.UserRole]);

        // when
        var failure = await JwtTestSupport.ValidationFailureAsync(foreignToken);

        // then
        Assert.IsAssignableFrom<SecurityTokenInvalidIssuerException>(failure);
    }

    [Fact]
    public async Task GivenExpiredToken_WhenValidated_ThenTokenIsRejected()
    {
        // given
        var expiredToken = JwtTestSupport.ExpiredToken();

        // when
        var failure = await JwtTestSupport.ValidationFailureAsync(expiredToken);

        // then
        Assert.IsAssignableFrom<SecurityTokenExpiredException>(failure);
    }

    [Fact]
    public void WhenBuildingValidationParameters_ThenSubjectIsTheNameAndRolesClaimHoldsTheRoles()
    {
        // given
        var jwtOptions = TestFixtures.JwtOptions();

        // when
        var validationParameters = JwtConfiguration.ValidationParameters(jwtOptions);

        // then
        Assert.Equal(JwtConventions.SubjectClaim, validationParameters.NameClaimType);
        Assert.Equal(JwtConventions.RolesClaim, validationParameters.RoleClaimType);
        Assert.Equal([JwtConventions.SignatureAlgorithm], validationParameters.ValidAlgorithms);
        Assert.Equal(TestFixtures.TestIssuer, validationParameters.ValidIssuer);
        Assert.Equal(JwtConventions.ClockSkew, validationParameters.ClockSkew);
    }
}
