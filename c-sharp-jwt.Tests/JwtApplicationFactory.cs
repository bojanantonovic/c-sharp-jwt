using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using c_sharp_jwt.Data;

namespace c_sharp_jwt.Tests;

/// <summary>
/// Boots the real application — the real endpoints, the real bearer handler, the real exception handler — against
/// a private in-memory database and a fixed JWT configuration, so a test never depends on appsettings.json.
/// </summary>
public class JwtApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new(TestDatabase.InMemoryConnectionString);

    public JwtApplicationFactory() => _connection.Open();

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.ExecuteDeleteAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = TestDatabase.InMemoryConnectionString,
                ["Jwt:Secret"] = TestFixtures.TestSecret,
                ["Jwt:Issuer"] = TestFixtures.TestIssuer,
                ["Jwt:ExpirationMs"] = TestFixtures.ValidExpirationMs.ToString(),
                ["Cors:AllowedOrigins:0"] = TestFixtures.AllowedOrigin
            }));

        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistrations(services);
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    /// <summary>
    /// Drops everything <c>AddDbContext&lt;AppDbContext&gt;</c> registered, including the option configuration
    /// entries EF Core adds beside the context itself, so the replacement is the only registration left.
    /// </summary>
    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(AppDbContext)
                                 || (descriptor.ServiceType.IsGenericType
                                     && descriptor.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext))))
            .ToList();

        foreach (var registration in registrations)
        {
            services.Remove(registration);
        }
    }
}
