using c_sharp_jwt.Security;
using c_sharp_jwt.Users;

namespace c_sharp_jwt.Tests.Security;

public class UserRolesMapperTests
{
    [Theory]
    [InlineData(Role.User, TestFixtures.UserRole)]
    [InlineData(Role.Admin, TestFixtures.AdminRole)]
    public void GivenUserWithRole_WhenMapping_ThenRoleNameIsReturned(Role role, string expectedRoleName)
    {
        // given
        var user = TestFixtures.User(role: role);

        // when
        var roles = UserRolesMapper.RolesOf(user);

        // then
        Assert.Equal([expectedRoleName], roles);
    }
}
