using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using c_sharp_jwt.Users;

namespace c_sharp_jwt.Data;

/// <summary>
/// The persistence half of the composition root. In the Spring sibling nothing corresponds to this class: the
/// <c>DataSource</c> and the <c>EntityManagerFactory</c> come from Boot's auto-configuration reading
/// <c>spring.datasource.*</c>, the repository is a proxy Spring Data generates from an interface, and the schema
/// is created by <c>ddl-auto: update</c> while the context refreshes. Here all three are written out — the
/// connection string is read explicitly, the repository is a real class, and the migration is a call in
/// <c>Program.cs</c>. See DEPENDENCY-INJECTION-COMPARISON.md §1, §8 and §4.
/// </summary>
public static class PersistenceConfiguration
{
    private const string ConnectionStringName = "Default";

    private const string MissingConnectionStringMessage =
        $"Missing ConnectionStrings:{ConnectionStringName} configuration";

    /// <summary>
    /// Takes the <see cref="IConfiguration"/> as a parameter because extension methods are plain statics —
    /// nothing is injected into them, and at registration time there is no provider to resolve it from. A
    /// Spring <c>@Bean</c> method would simply declare it as a parameter and have it injected.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
                               ?? throw new InvalidOperationException(MissingConnectionStringMessage);

        serviceCollection.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        serviceCollection.AddScoped<IUserRepository, UserRepository>();

        return serviceCollection;
    }

    /// <summary>
    /// The scope is not optional: <see cref="AppDbContext"/> is scoped, and <c>webApplication.Services</c> is the root
    /// provider, which refuses to hand out a scoped service. Spring never needs this because its persistence
    /// context hides behind a thread-bound proxy, so even a singleton bean gets a correct unit of work.
    /// </summary>
    public static WebApplication MigrateDatabase(this WebApplication webApplication)
    {
        using var scope = webApplication.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

        return webApplication;
    }
}
