using System.Threading;
using System.Threading.Tasks;

namespace c_sharp_jwt.Users;

/// <summary>
/// Kept behind an interface so that the services using it can be unit-tested without a database.
/// </summary>
public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Persists a new user and assigns its generated identifier.</summary>
    Task CreateAsync(User user, CancellationToken cancellationToken = default);
}
