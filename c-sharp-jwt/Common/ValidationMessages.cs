namespace c_sharp_jwt.Common;

public static class ValidationMessages
{
    public const string ValidationFailed = "Validation failed";
    public const string MustNotBeBlank = "must not be blank";
    public const string MustBeWellFormedEmail = "must be a well-formed email address";
    public const string PasswordTooShort = "Password must be at least 8 characters long";
    public const string InvalidValue = "Invalid value";
    public const string MissingRequestBody = "Request body is missing";
}
