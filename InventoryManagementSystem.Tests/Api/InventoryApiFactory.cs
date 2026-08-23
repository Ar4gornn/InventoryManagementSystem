using InventoryManagementSystem.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagementSystem.Tests.Api;

/// <summary>
/// Boots the real application in memory so the middleware pipeline is exercised
/// for real, rather than asserted about.
/// </summary>
/// <remarks>
/// Service-level tests cannot cover the API key check or the exception-to-status
/// mapping, because both live in middleware that only runs on a real request.
/// This is what <c>public partial class Program</c> in Program.cs exists for.
///
/// The database is swapped for SQLite in-memory with the connection held open for
/// the factory's lifetime, so startup migrations apply to a throwaway schema and
/// the developer's inventory.db is never touched by a test run.
/// </remarks>
public class InventoryApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-key-not-a-real-secret";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    static InventoryApiFactory()
    {
        // Set as an environment variable rather than through
        // ConfigureAppConfiguration, because Program.cs reads the key from
        // builder.Configuration *before* builder.Build() - and the factory's
        // configuration callbacks do not run until Build(). The app would refuse
        // to start, which is exactly the behaviour it is supposed to have.
        //
        // This also matches how the key is really supplied in production.
        Environment.SetEnvironmentVariable("Security__ApiKey", ApiKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<InventoryContext>));

            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<InventoryContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
