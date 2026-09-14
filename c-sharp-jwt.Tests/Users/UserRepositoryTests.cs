using c_sharp_jwt.Data;
using c_sharp_jwt.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace c_sharp_jwt.Tests.Users;

/// <summary>
/// Runs against a real SQLite database, kept in memory, so that the mapping and the unique index are exercised
/// rather than mocked away.
/// </summary>
public class UserRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new(TestDatabase.InMemoryConnectionString);

    private AppDbContext _dbContext = null!;
    private IUserRepository _userRepository = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _dbContext.Database.MigrateAsync();
        _userRepository = new UserRepository(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GivenNewUser_WhenCreate_ThenItIsPersistedWithAGeneratedId()
    {
        // given
        var user = TestFixtures.User(role: Role.Admin);

        // when
        await _userRepository.CreateAsync(user);

        // then
        Assert.NotEqual(0, user.Id);
        var persistedUser = await _userRepository.FindByEmailAsync(TestFixtures.TestEmail);
        Assert.NotNull(persistedUser);
        Assert.Equal(TestFixtures.HashedPassword, persistedUser.PasswordHash);
        Assert.Equal(Role.Admin, persistedUser.Role);
    }

    [Fact]
    public async Task GivenUnknownEmail_WhenFindByEmail_ThenNothingIsFound()
    {
        // given
        await _userRepository.CreateAsync(TestFixtures.User());

        // when
        var persistedUser = await _userRepository.FindByEmailAsync(TestFixtures.UnknownEmail);

        // then
        Assert.Null(persistedUser);
    }

    [Fact]
    public async Task GivenExistingUser_WhenExistsByEmail_ThenOnlyThatEmailIsReported()
    {
        // given
        await _userRepository.CreateAsync(TestFixtures.User());

        // when
        var existingIsReported = await _userRepository.ExistsByEmailAsync(TestFixtures.TestEmail);
        var unknownIsReported = await _userRepository.ExistsByEmailAsync(TestFixtures.UnknownEmail);

        // then
        Assert.True(existingIsReported);
        Assert.False(unknownIsReported);
    }

    [Fact]
    public async Task GivenEmailAlreadyTaken_WhenCreate_ThenTheUniqueIndexRejectsIt()
    {
        // given
        await _userRepository.CreateAsync(TestFixtures.User());

        // when / then
        await Assert.ThrowsAsync<DbUpdateException>(() => _userRepository.CreateAsync(TestFixtures.User()));
    }

    [Fact]
    public async Task GivenUserWithRole_WhenPersisted_ThenTheRoleIsStoredAsItsName()
    {
        // given
        await _userRepository.CreateAsync(TestFixtures.User(role: Role.Admin));

        // when
        var storedRole = await _dbContext.Database
            .SqlQuery<string>($"SELECT Role AS Value FROM Users WHERE Email = {TestFixtures.TestEmail}")
            .SingleAsync();

        // then
        Assert.Equal(nameof(Role.Admin), storedRole);
    }
}
