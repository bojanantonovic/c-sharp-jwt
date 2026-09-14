namespace c_sharp_jwt.Security;

/// <summary>
/// Validation is deliberately absent: it is done by the JWT bearer handler before a request reaches an endpoint.
/// </summary>
public interface ITokenService
{
    string GenerateToken(string subject, IEnumerable<string> roles);
}
