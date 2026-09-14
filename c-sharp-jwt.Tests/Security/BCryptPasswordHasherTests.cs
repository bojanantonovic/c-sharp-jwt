using Xunit;
using c_sharp_jwt.Security;

namespace c_sharp_jwt.Tests.Security;

public class BCryptPasswordHasherTests
{
    private readonly IPasswordHasher _passwordHasher = new BCryptPasswordHasher();

    [Fact]
    public void WhenHashing_ThenTheRawPasswordIsNotStoredAndVerifies()
    {
        // given
        var rawPassword = TestFixtures.TestPassword;

        // when
        var passwordHash = _passwordHasher.Hash(rawPassword);

        // then
        Assert.NotEqual(rawPassword, passwordHash);
        Assert.True(_passwordHasher.Verify(rawPassword, passwordHash));
    }

    [Fact]
    public void GivenAnotherPassword_WhenVerifying_ThenItIsRejected()
    {
        // given
        var passwordHash = _passwordHasher.Hash(TestFixtures.TestPassword);

        // when
        var verified = _passwordHasher.Verify(TestFixtures.WrongPassword, passwordHash);

        // then
        Assert.False(verified);
    }

    [Fact]
    public void WhenHashingTheSamePasswordTwice_ThenTheHashesDifferBecauseOfTheSalt()
    {
        // given
        var rawPassword = TestFixtures.TestPassword;

        // when
        var firstHash = _passwordHasher.Hash(rawPassword);
        var secondHash = _passwordHasher.Hash(rawPassword);

        // then
        Assert.NotEqual(firstHash, secondHash);
    }
}
