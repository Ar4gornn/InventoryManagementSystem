using InventoryManagementSystem.Contracts;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using InventoryManagementSystem.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagementSystem.Tests.Services;

/// <summary>
/// The same service, exercised against real SQLite instead of the InMemory provider.
/// </summary>
/// <remarks>
/// These exist because InMemory is not a relational provider: it evaluates LINQ in
/// process, so a query it runs happily can still fail to translate to SQL. That is
/// not hypothetical - GetAllAsync ordered by a projected DTO passed every InMemory
/// test and threw InvalidOperationException against SQLite. Any query change should
/// be covered here, not only in the InMemory tests.
/// </remarks>
public class ProductServiceSqliteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<InventoryContext> _options;

    public ProductServiceSqliteTests()
    {
        // A SQLite in-memory database lives only as long as its connection is open,
        // so the connection is held for the lifetime of the test class.
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

    private static ProductService NewService(InventoryContext context) =>
        new(context, NullLogger<ProductService>.Instance);

    private async Task<int> SeedAsync()
    {
        using var context = NewContext();

        var category = new Category { Name = "Tools" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var drill = new Product
        {
            Sku = "TL-DRL-001",
            Name = "Drill",
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow,
        };
        var saw = new Product
        {
            Sku = "AA-SAW-001",
            Name = "Saw",
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow,
        };
        context.Products.AddRange(drill, saw);
        await context.SaveChangesAsync();

        context.StockMovements.AddRange(
            new StockMovement { ProductId = drill.Id, Type = MovementType.In, QuantityDelta = 25, OccurredAt = DateTime.UtcNow },
            new StockMovement { ProductId = drill.Id, Type = MovementType.Out, QuantityDelta = -4, OccurredAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        return category.Id;
    }

    [Fact]
    public async Task GetAllAsync_translates_to_sql_and_sums_stock()
    {
        await SeedAsync();
        using var context = NewContext();

        var all = (await NewService(context).GetAsync(new ProductQuery())).Items;

        Assert.Equal(new[] { "AA-SAW-001", "TL-DRL-001" }, all.Select(p => p.Sku));
        Assert.Equal(0, all.Single(p => p.Sku == "AA-SAW-001").QuantityOnHand);
        Assert.Equal(21, all.Single(p => p.Sku == "TL-DRL-001").QuantityOnHand);
    }

    [Fact]
    public async Task GetByIdAsync_translates_to_sql_and_sums_stock()
    {
        await SeedAsync();
        using var context = NewContext();

        var drillId = await context.Products.Where(p => p.Sku == "TL-DRL-001").Select(p => p.Id).SingleAsync();

        var dto = await NewService(context).GetByIdAsync(drillId);

        Assert.NotNull(dto);
        Assert.Equal(21, dto!.QuantityOnHand);
        Assert.Equal("Tools", dto.CategoryName);
    }

    [Fact]
    public async Task CreateAsync_round_trips_through_a_real_database()
    {
        var categoryId = await SeedAsync();
        using var context = NewContext();

        var created = await NewService(context).CreateAsync(new CreateProductRequest
        {
            Sku = "TL-HAM-001",
            Name = "Claw hammer",
            CategoryId = categoryId,
        });

        using var verify = NewContext();
        var stored = await verify.Products.SingleAsync(p => p.Id == created.Id);
        Assert.Equal("TL-HAM-001", stored.Sku);
        Assert.Equal(0, created.QuantityOnHand);
    }

    [Fact]
    public async Task The_database_rejects_a_zero_delta_movement()
    {
        await SeedAsync();
        using var context = NewContext();

        var productId = await context.Products.Select(p => p.Id).FirstAsync();

        context.StockMovements.Add(new StockMovement
        {
            ProductId = productId,
            Type = MovementType.Adjustment,
            QuantityDelta = 0,
            OccurredAt = DateTime.UtcNow,
        });

        // The check constraint is real schema, so it holds even if service-level
        // validation is ever bypassed. InMemory could not prove this.
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
