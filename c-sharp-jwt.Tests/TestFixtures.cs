using c_sharp_jwt.Security;
using c_sharp_jwt.Users;

namespace c_sharp_jwt.Tests;

public static class TestFixtures
{
    public const string TestEmail = "jane.doe@example.com";
    public const string OtherEmail = "other.user@example.com";
    public const string UnknownEmail = "unknown@example.com";
    public const string TestPassword = "s3curePassword!";
    public const string WrongPassword = "wrongPassword!";
    public const string HashedPassword = "hashed-password";
    public const string TestIssuer = "http://localhost:8080";
    public const string OtherIssuer = "http://localhost:9999";
    public const string TestSecret = "5367566B59703373367639792F423F4528482B4D6251655468576D5A713474";
    public const string OtherSecret = "4A404E635266556A586E3272357538782F413F4428472B4B6250645367566B";
    public const long ValidExpirationMs = 3_600_000L;
    public const string AllowedOrigin = "http://localhost:4200";
    public const string UserRole = "User";
    public const string AdminRole = "Admin";

    public static JwtOptions JwtOptions(string secret = TestSecret,
                                        string issuer = TestIssuer,
                                        long expirationMs = ValidExpirationMs) =>
        new() { Secret = secret, Issuer = issuer, ExpirationMs = expirationMs };

    public static User User(string email = TestEmail, Role role = Role.User) =>
        new() { Email = email, PasswordHash = HashedPassword, Role = role };
}
