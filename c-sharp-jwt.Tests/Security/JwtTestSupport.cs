using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using c_sharp_jwt.Security;

namespace c_sharp_jwt.Tests.Security;

/// <summary>
/// Builds the real <see cref="TokenService"/> and the real validation parameters from
/// <see cref="JwtConfiguration"/>, so the tests exercise the production wiring instead of a look-alike setup.
/// </summary>
internal static class JwtTestSupport
{
    private static readonly TimeSpan ExpiredTokenAge = TimeSpan.FromHours(2);
    private static readonly TimeSpan ExpiredTokenLifetime = TimeSpan.FromHours(1);

    private static readonly JsonWebTokenHandler TokenHandler = new();

    internal static ITokenService TokenService(string secret = TestFixtures.TestSecret,
                                               string issuer = TestFixtures.TestIssuer,
                                               long expirationMs = TestFixtures.ValidExpirationMs) =>
        new TokenService(Options.Create(TestFixtures.JwtOptions(secret, issuer, expirationMs)));

    internal static Task<TokenValidationResult> ValidateAsync(string token,
                                                              string secret = TestFixtures.TestSecret,
                                                              string issuer = TestFixtures.TestIssuer) =>
        TokenHandler.ValidateTokenAsync(
            token, JwtConfiguration.ValidationParameters(TestFixtures.JwtOptions(secret, issuer)));

    internal static async Task<TokenValidationResult> ValidatedAsync(string token)
    {
        var result = await ValidateAsync(token);
        Assert.True(result.IsValid, "Expected the token to be accepted, but it was rejected");

        return result;
    }

    internal static async Task<Exception> ValidationFailureAsync(string token,
                                                                 string secret = TestFixtures.TestSecret,
                                                                 string issuer = TestFixtures.TestIssuer)
    {
        var result = await ValidateAsync(token, secret, issuer);
        Assert.False(result.IsValid, "Expected the token to be rejected, but it was accepted");

        return result.Exception;
    }

    /// <summary>
    /// <see cref="TokenService"/> cannot issue an already expired token, so the handler is driven directly here.
    /// </summary>
    internal static string ExpiredToken(string secret = TestFixtures.TestSecret,
                                        string issuer = TestFixtures.TestIssuer)
    {
        var issuedAt = DateTime.UtcNow.Subtract(ExpiredTokenAge);

        return TokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.Add(ExpiredTokenLifetime),
            Claims = new Dictionary<string, object> { [JwtConventions.SubjectClaim] = TestFixtures.TestEmail },
            SigningCredentials = JwtConfiguration.SigningCredentials(TestFixtures.JwtOptions(secret, issuer))
        });
    }
}
