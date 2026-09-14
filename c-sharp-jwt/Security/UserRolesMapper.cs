using c_sharp_jwt.Users;

namespace c_sharp_jwt.Security;

/// <summary>
/// The single place where a <see cref="User"/> becomes the roles a token carries, so that a token issued right
/// after registration cannot end up carrying different claims than one issued after a login.
/// </summary>
public static class UserRolesMapper
{
    public static IReadOnlyList<string> RolesOf(User user) => [user.Role.ToString()];
}
