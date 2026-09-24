using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using c_sharp_jwt.Data;

namespace c_sharp_jwt.Users;

/// <inheritdoc />
public class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public Task CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Add(user);

        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
