using InventoryManagementSystem.Contracts;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using InventoryManagementSystem.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagementSystem.Tests.Services;

/// <summary>
/// Category queries against real SQLite. The InMemory provider cannot prove that a
/// projection translates to SQL, nor that the unique index and the Restrict foreign
/// key are actually enforced by the schema.
/// </summary>
public class CategoryServiceSqliteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<InventoryContext> _options;

    public CategoryServiceSqliteTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<InventoryContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new InventoryContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private InventoryContext NewContext() => new(_options);

    private static CategoryService NewService(InventoryContext context) =>
        new(context, NullLogger<CategoryService>.Instance);

    [Fact]
    public async Task GetAllAsync_translates_to_sql_ordered_by_name_with_product_counts()
    {
        using (var seed = NewContext())
        {
            var tools = new Category { Name = "Tools" };
            var safety = new Category { Name = "Safety" };
            seed.Categories.AddRange(tools, safety);
            await seed.SaveChangesAsync();

            seed.Products.Add(new Product
            {
                Sku = "TL-DRL-001",
                Name = "Drill",
                CategoryId = tools.Id,
                CreatedAt = DateTime.UtcNow,
            });
            await seed.SaveChangesAsync();
        }

        using var context = NewContext();
        var all = (await NewService(context).GetAsync(new CategoryQuery())).Items;

        Assert.Equal(new[] { "Safety", "Tools" }, all.Select(c => c.Name));
        Assert.Equal(0, all.Single(c => c.Name == "Safety").ProductCount);
        Assert.Equal(1, all.Single(c => c.Name == "Tools").ProductCount);
    }

    [Fact]
    public async Task The_database_enforces_unique_category_names()
    {
        using var context = NewContext();

        context.Categories.Add(new Category { Name = "Tools" });
        await context.SaveChangesAsync();

        context.Categories.Add(new Category { Name = "Tools" });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task The_foreign_key_restricts_deleting_a_category_that_has_products()
    {
        int categoryId;

        using (var seed = NewContext())
        {
            var tools = new Category { Name = "Tools" };
            seed.Categories.Add(tools);
            await seed.SaveChangesAsync();
            categoryId = tools.Id;

            seed.Products.Add(new Product
            {
                Sku = "TL-DRL-001",
                Name = "Drill",
                CategoryId = tools.Id,
                CreatedAt = DateTime.UtcNow,
            });
            await seed.SaveChangesAsync();
        }

        using var context = NewContext();
        var category = await context.Categories.SingleAsync(c => c.Id == categoryId);
        context.Categories.Remove(category);

        // The service checks first and answers 409. This proves the schema would stop
        // it regardless, so products can never be orphaned by another code path.
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
