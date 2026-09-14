using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using c_sharp_jwt.Auth.Dto;
using c_sharp_jwt.Tests.Security;
using c_sharp_jwt.Users.Dto;

namespace c_sharp_jwt.Tests.Users;

public class UserEndpointsTests(JwtApplicationFactory factory)
    : IClassFixture<JwtApplicationFactory>, IAsyncLifetime
{
    private const string RegisterUrl = "/api/auth/register";
    private const string MeUrl = "/api/users/me";
    private const string BearerChallenge = "Bearer";
    private const string InvalidTokenError = "invalid_token";
    private const string MalformedToken = "not-a-jwt";
    private const string SignatureTamperSuffix = "tampered";

    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GivenValidToken_WhenRequestingProtectedEndpoint_ThenReturnsCurrentUserWithMappedRoles()
    {
        // given
        var token = await RegisterAndReturnTokenAsync();

        // when
        var response = await GetMeAsync(token);

        // then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Equal(TestFixtures.TestEmail, body.Email);
        Assert.Equal([TestFixtures.UserRole], body.Roles);
    }

    [Fact]
    public async Task GivenNoToken_WhenRequestingProtectedEndpoint_ThenReturnsUnauthorizedWithBearerChallenge()
    {
        // given
        var request = new HttpRequestMessage(HttpMethod.Get, MeUrl);

        // when
        var response = await _client.SendAsync(request);

        // then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(BearerChallenge, response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task GivenMalformedToken_WhenRequestingProtectedEndpoint_ThenReturnsUnauthorized()
    {
        // given
        var token = MalformedToken;

        // when
        var response = await GetMeAsync(token);

        // then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(InvalidTokenError, response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task GivenTokenWithTamperedSignature_WhenRequestingProtectedEndpoint_ThenReturnsUnauthorized()
    {
        // given
        var tamperedToken = await RegisterAndReturnTokenAsync() + SignatureTamperSuffix;

        // when
        var response = await GetMeAsync(tamperedToken);

        // then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(InvalidTokenError, response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task GivenTokenOfAnotherIssuer_WhenRequestingProtectedEndpoint_ThenReturnsUnauthorized()
    {
        // given
        var foreignToken = JwtTestSupport.TokenService(issuer: TestFixtures.OtherIssuer)
            .GenerateToken(TestFixtures.TestEmail, [TestFixtures.UserRole]);

        // when
        var response = await GetMeAsync(foreignToken);

        // then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GivenExpiredToken_WhenRequestingProtectedEndpoint_ThenReturnsUnauthorized()
    {
        // given
        var expiredToken = JwtTestSupport.ExpiredToken();

        // when
        var response = await GetMeAsync(expiredToken);

        // then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Documents the price of the self-contained token: nothing is looked up in the database while a request is
    /// authenticated, so a token stays usable until it expires, even after its user is gone.
    /// </summary>
    [Fact]
    public async Task GivenTokenOfDeletedUser_WhenRequestingProtectedEndpoint_ThenTokenIsStillAccepted()
    {
        // given
        var token = await RegisterAndReturnTokenAsync();
        await factory.ResetDatabaseAsync();

        // when
        var response = await GetMeAsync(token);

        // then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Equal(TestFixtures.TestEmail, body.Email);
    }

    private Task<HttpResponseMessage> GetMeAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, MeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue(AuthResponse.BearerTokenType, token);

        return _client.SendAsync(request);
    }

    private async Task<string> RegisterAndReturnTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            RegisterUrl, new RegisterRequest(TestFixtures.TestEmail, TestFixtures.TestPassword));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }
}
