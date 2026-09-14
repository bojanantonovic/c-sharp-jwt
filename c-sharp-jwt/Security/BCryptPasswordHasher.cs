namespace c_sharp_jwt.Security;

/// <inheritdoc />
public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string rawPassword) => BCrypt.Net.BCrypt.HashPassword(rawPassword);

    public bool Verify(string rawPassword, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(rawPassword, passwordHash);
}
