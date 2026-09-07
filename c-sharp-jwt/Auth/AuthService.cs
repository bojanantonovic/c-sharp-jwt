using c_sharp_jwt.Auth.Dto;
using c_sharp_jwt.Data;
using c_sharp_jwt.Security;
using c_sharp_jwt.Users;
using Microsoft.EntityFrameworkCore;

namespace c_sharp_jwt.Auth;

public class AuthService(AppDbContext dbContext, JwtTokenService jwtTokenService)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await dbContext.Users.AnyAsync(user => user.Email == request.Email))
        {
            throw new EmailAlreadyInUseException(request.Email);
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Role.User
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var token = jwtTokenService.GenerateToken(user.Email);
        return AuthResponse.Of(token, user.Email);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(user => user.Email == request.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var token = jwtTokenService.GenerateToken(user.Email);
        return AuthResponse.Of(token, user.Email);
    }
}
