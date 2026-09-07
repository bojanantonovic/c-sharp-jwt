namespace c_sharp_jwt.Auth;

public class EmailAlreadyInUseException(string email) : Exception($"Email already in use: {email}");
