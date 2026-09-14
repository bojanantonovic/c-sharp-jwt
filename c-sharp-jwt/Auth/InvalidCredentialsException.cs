namespace c_sharp_jwt.Auth;

/// <summary>
/// Raised for an unknown email as well as for a wrong password, so that a caller cannot tell the two apart.
/// </summary>
public class InvalidCredentialsException() : Exception(InvalidCredentialsMessage)
{
    public const string InvalidCredentialsMessage = "Invalid email or password";
}
