using System;

namespace c_sharp_jwt.Auth;

public class EmailAlreadyInUseException(string email) : Exception(MessagePrefix + email)
{
    public const string MessagePrefix = "Email already in use: ";
}
