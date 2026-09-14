using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using c_sharp_jwt.Auth;
using c_sharp_jwt.Auth.Dto;
using c_sharp_jwt.Security;
using c_sharp_jwt.Users;

namespace c_sharp_jwt.Tests.Auth;

public class AuthServiceTests
{
    private const string GeneratedToken = "generated-token";

    private readonly Mock<IUserRepository> _userRepository = new(MockBehavior.Strict);
    private readonly Mock<IPasswordHasher> _passwordHasher = new(MockBehavior.Strict);
    private readonly Mock<ITokenService> _tokenService = new(MockBehavior.Strict);

    private readonly AuthService _authService;

    public AuthServiceTests() =>
        _authService = new AuthService(_userRepository.Object, _passwordHasher.Object, _tokenService.Object);

    [Fact]
    public async Task GivenNewEmail_WhenRegister_ThenSavesUserAndReturnsToken()
    {
        // given
        var request = new RegisterRequest(TestFixtures.TestEmail, TestFixtures.TestPassword);
        _userRepository.Setup(repository => repository.ExistsByEmailAsync(TestFixtures.TestEmail, default))
            .ReturnsAsync(false);
        _passwordHasher.Setup(hasher => hasher.Hash(TestFixtures.TestPassword))
            .Returns(TestFixtures.HashedPassword);
        User? savedUser = null;
        _userRepository.Setup(repository => repository.CreateAsync(It.IsAny<User>(), default))
            .Callback<User, CancellationToken>((user, _) => savedUser = user)
            .Returns(Task.CompletedTask);
        _tokenService.Setup(service => service.GenerateToken(TestFixtures.TestEmail,
                                                            It.Is<IEnumerable<string>>(roles =>
                                                                roles.Single() == TestFixtures.UserRole)))
            .Returns(GeneratedToken);

        // when
        var response = await _authService.RegisterAsync(request);

        // then
        Assert.Equal(GeneratedToken, response.Token);
        Assert.Equal(AuthResponse.BearerTokenType, response.TokenType);
        Assert.Equal(TestFixtures.TestEmail, response.Email);
        Assert.NotNull(savedUser);
        Assert.Equal(TestFixtures.TestEmail, savedUser.Email);
        Assert.Equal(TestFixtures.HashedPassword, savedUser.PasswordHash);
        Assert.Equal(Role.User, savedUser.Role);
    }

    [Fact]
    public async Task GivenEmailAlreadyInUse_WhenRegister_ThenThrowsEmailAlreadyInUseException()
    {
        // given
        var request = new RegisterRequest(TestFixtures.TestEmail, TestFixtures.TestPassword);
        _userRepository.Setup(repository => repository.ExistsByEmailAsync(TestFixtures.TestEmail, default))
            .ReturnsAsync(true);

        // when
        var exception =
            await Assert.ThrowsAsync<EmailAlreadyInUseException>(() => _authService.RegisterAsync(request));

        // then
        Assert.Contains(TestFixtures.TestEmail, exception.Message);
        _userRepository.Verify(repository => repository.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
                               Times.Never);
        _passwordHasher.VerifyNoOtherCalls();
        _tokenService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenValidCredentials_WhenLogin_ThenReturnsToken()
    {
        // given
        var request = new LoginRequest(TestFixtures.TestEmail, TestFixtures.TestPassword);
        var user = TestFixtures.User(role: Role.Admin);
        _userRepository.Setup(repository => repository.FindByEmailAsync(TestFixtures.TestEmail, default))
            .ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.Verify(TestFixtures.TestPassword, TestFixtures.HashedPassword))
            .Returns(true);
        _tokenService.Setup(service => service.GenerateToken(TestFixtures.TestEmail,
                                                            It.Is<IEnumerable<string>>(roles =>
                                                                roles.Single() == TestFixtures.AdminRole)))
            .Returns(GeneratedToken);

        // when
        var response = await _authService.LoginAsync(request);

        // then
        Assert.Equal(GeneratedToken, response.Token);
        Assert.Equal(TestFixtures.TestEmail, response.Email);
    }

    [Fact]
    public async Task GivenWrongPassword_WhenLogin_ThenThrowsInvalidCredentialsExceptionWithoutIssuingToken()
    {
        // given
        var request = new LoginRequest(TestFixtures.TestEmail, TestFixtures.WrongPassword);
        _userRepository.Setup(repository => repository.FindByEmailAsync(TestFixtures.TestEmail, default))
            .ReturnsAsync(TestFixtures.User());
        _passwordHasher.Setup(hasher => hasher.Verify(TestFixtures.WrongPassword, TestFixtures.HashedPassword))
            .Returns(false);

        // when / then
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => _authService.LoginAsync(request));
        _tokenService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenUnknownEmail_WhenLogin_ThenThrowsInvalidCredentialsExceptionWithoutHashing()
    {
        // given
        var request = new LoginRequest(TestFixtures.UnknownEmail, TestFixtures.TestPassword);
        _userRepository.Setup(repository => repository.FindByEmailAsync(TestFixtures.UnknownEmail, default))
            .ReturnsAsync((User?)null);

        // when / then
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => _authService.LoginAsync(request));
        _passwordHasher.VerifyNoOtherCalls();
        _tokenService.VerifyNoOtherCalls();
    }
}
