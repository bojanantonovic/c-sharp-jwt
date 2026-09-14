using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using c_sharp_jwt.Auth;
using c_sharp_jwt.Auth.Dto;
using c_sharp_jwt.Common;

namespace c_sharp_jwt.Tests.Auth;

public class AuthEndpointsTests(JwtApplicationFactory factory)
    : IClassFixture<JwtApplicationFactory>, IAsyncLifetime
{
    private const string RegisterUrl = "/api/auth/register";
    private const string LoginUrl = "/api/auth/login";
    private const string MalformedEmail = "not-an-email";
    private const string TooShortPassword = "short1";
    private const string MessageField = "message";
    private const string ErrorsField = "errors";
    private const string EmailField = "email";
    private const string PasswordField = "password";

    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GivenNewEmail_WhenRegister_ThenReturnsCreatedWithToken()
    {
        // given
        var request = new RegisterRequest(TestFixtures.TestEmail, TestFixtures.TestPassword);

        // when
        var response = await _client.PostAsJsonAsync(RegisterUrl, request);

        // then
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.Equal(AuthResponse.BearerTokenType, body.TokenType);
        Assert.Equal(TestFixtures.TestEmail, body.Email);
    }

    [Fact]
    public async Task GivenAlreadyRegisteredEmail_WhenRegisterAgain_ThenReturnsConflict()
    {
        // given
        await RegisterAndReturnTokenAsync();

        // when
        var response = await _client.PostAsJsonAsync(
            RegisterUrl, new RegisterRequest(TestFixtures.TestEmail, TestFixtures.TestPassword));

        // then
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(body);
        Assert.Contains(TestFixtures.TestEmail, body.Message);
    }

    [Fact]
    public async Task GivenRegisteredUser_WhenLoginWithCorrectPassword_ThenReturnsToken()
    {
        // given
        await RegisterAndReturnTokenAsync();

        // when
        var response = await _client.PostAsJsonAsync(
            LoginUrl, new LoginRequest(TestFixtures.TestEmail, TestFixtures.TestPassword));

        // then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
    }

    [Fact]
    public async Task GivenRegisteredUser_WhenLoginWithWrongPassword_ThenReturnsUnauthorized()
    {
        // given
        await RegisterAndReturnTokenAsync();

        // when
        var response = await _client.PostAsJsonAsync(
            LoginUrl, new LoginRequest(TestFixtures.TestEmail, TestFixtures.WrongPassword));

        // then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(body);
        Assert.Equal(InvalidCredentialsException.InvalidCredentialsMessage, body.Message);
    }

    [Fact]
    public async Task GivenUnknownEmail_WhenLogin_ThenReturnsTheSameUnauthorizedAsAWrongPassword()
    {
        // given
        await RegisterAndReturnTokenAsync();

        // when
        var response = await _client.PostAsJsonAsync(
            LoginUrl, new LoginRequest(TestFixtures.UnknownEmail, TestFixtures.TestPassword));

        // then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(body);
        Assert.Equal(InvalidCredentialsException.InvalidCredentialsMessage, body.Message);
    }

    [Fact]
    public async Task GivenMalformedEmail_WhenRegister_ThenReturnsBadRequestWithFieldError()
    {
        // given
        var request = new RegisterRequest(MalformedEmail, TestFixtures.TestPassword);

        // when
        var response = await _client.PostAsJsonAsync(RegisterUrl, request);

        // then
        await AssertFieldErrorAsync(response, EmailField, ValidationMessages.MustBeWellFormedEmail);
    }

    [Fact]
    public async Task GivenTooShortPassword_WhenRegister_ThenReturnsBadRequestWithFieldError()
    {
        // given
        var request = new RegisterRequest(TestFixtures.TestEmail, TooShortPassword);

        // when
        var response = await _client.PostAsJsonAsync(RegisterUrl, request);

        // then
        await AssertFieldErrorAsync(response, PasswordField, ValidationMessages.PasswordTooShort);
    }

    [Fact]
    public async Task GivenBlankPassword_WhenLogin_ThenReturnsBadRequestWithFieldError()
    {
        // given
        var request = new LoginRequest(TestFixtures.TestEmail, "   ");

        // when
        var response = await _client.PostAsJsonAsync(LoginUrl, request);

        // then
        await AssertFieldErrorAsync(response, PasswordField, ValidationMessages.MustNotBeBlank);
    }

    [Fact]
    public async Task GivenAnInvalidRequest_WhenRegister_ThenNothingIsPersisted()
    {
        // given
        await _client.PostAsJsonAsync(RegisterUrl, new RegisterRequest(MalformedEmail, TestFixtures.TestPassword));

        // when
        var response = await _client.PostAsJsonAsync(
            LoginUrl, new LoginRequest(MalformedEmail, TestFixtures.TestPassword));

        // then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task AssertFieldErrorAsync(HttpResponseMessage response, string field, string expectedMessage)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ValidationMessages.ValidationFailed, body.GetProperty(MessageField).GetString());
        Assert.Equal(expectedMessage, body.GetProperty(ErrorsField).GetProperty(field).GetString());
    }

    private async Task<string> RegisterAndReturnTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            RegisterUrl, new RegisterRequest(TestFixtures.TestEmail, TestFixtures.TestPassword));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }
}
