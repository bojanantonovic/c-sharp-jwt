using c_sharp_jwt.Auth.Dto;
using c_sharp_jwt.Security;
using c_sharp_jwt.Users;

namespace c_sharp_jwt.Auth;

public class AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request,
                                                  CancellationToken cancellationToken = default)
    {
        if (await userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            throw new EmailAlreadyInUseException(request.Email);
        }

        var user = ToUser(request);
        await userRepository.CreateAsync(user, cancellationToken);

        return TokenFor(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        return TokenFor(user);
    }

    private User ToUser(RegisterRequest request) => new()
    {
        Email = request.Email,
        PasswordHash = passwordHasher.Hash(request.Password),
        Role = Role.User
    };

    private AuthResponse TokenFor(User user) =>
        AuthResponse.Of(tokenService.GenerateToken(user.Email, UserRolesMapper.RolesOf(user)), user.Email);
}
