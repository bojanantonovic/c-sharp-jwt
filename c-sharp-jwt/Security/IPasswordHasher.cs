namespace c_sharp_jwt.Security;

/// <summary>
/// Abstracted so the algorithm can be swapped, and so the services using it stay unit-testable.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string rawPassword);

    bool Verify(string rawPassword, string passwordHash);
}
