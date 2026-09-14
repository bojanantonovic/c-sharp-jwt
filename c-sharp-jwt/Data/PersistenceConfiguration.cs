using c_sharp_jwt.Users;
using Microsoft.EntityFrameworkCore;

namespace c_sharp_jwt.Data;

public static class PersistenceConfiguration
{
    public const string ConnectionStringName = "Default";

    private const string MissingConnectionStringMessage =
        $"Missing ConnectionStrings:{ConnectionStringName} configuration";

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
                               ?? throw new InvalidOperationException(MissingConnectionStringMessage);

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }

    public static WebApplication MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

        return app;
    }
}
