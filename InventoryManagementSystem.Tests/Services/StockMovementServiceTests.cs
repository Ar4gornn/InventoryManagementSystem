using InventoryManagementSystem.Application;
using InventoryManagementSystem.Contracts;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using InventoryManagementSystem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagementSystem.Tests.Services;

/// <summary>
/// Every movement test runs against real SQLite, not the InMemory provider.
/// RecordAsync opens a serializable transaction to make the balance check and the
/// insert atomic, and InMemory does not support transactions at all - so InMemory
/// could only ever test a weaker version of this code.
/// </summary>
public class StockMovementServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<InventoryContext> _options;

    public StockMovementServiceTests()
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

    private static StockMovementService NewService(InventoryContext context) =>
        new(context, NullLogger<StockMovementService>.Instance);

    private async Task<int> SeedProductAsync()
    {
        using var context = NewContext();

        var category = new Category { Name = "Tools" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var product = new Product
        {
            Sku = "TL-DRL-001",
            Name = "Drill",
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow,
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        return product.Id;
    }

    [Fact]
    public async Task An_In_movement_adds_stock()
    {
        var productId = await SeedProductAsync();
        using var context = NewContext();

        var movement = await NewService(context).RecordAsync(productId, new CreateMovementRequest
        {
            Type = MovementType.In,
            Quantity = 25,
            Reason = "Opening stock",
        });

        Assert.Equal(25, movement.QuantityDelta);
        Assert.Equal(25, movement.RunningTotal);
    }

    [Fact]
    public async Task An_Out_movement_takes_a_positive_quantity_and_stores_a_negative_delta()
    {
        var productId = await SeedProductAsync();
        using var context = NewContext();
        var service = NewService(context);

        await service.RecordAsync(productId, new CreateMovementRequest
        {
            Type = MovementType.In,
            Quantity = 25,
        });

        // The caller says "remove 4", not "-4". The sign is the type's job.
        var movement = await service.RecordAsync(productId, new CreateMovementRequest
        {
            Type = MovementType.Out,
            Quantity = 4,
        });

        Assert.Equal(-4, movement.QuantityDelta);
        Assert.Equal(21, movement.RunningTotal);
    }

    [Fact]
    public async Task An_Out_movement_that_would_go_negative_is_rejected_and_nothing_is_written()
    {
        var productId = await SeedProductAsync();
        using var context = NewContext();
        var service = NewService(context);

        await service.RecordAsync(productId, new CreateMovementRequest
        {
            Type = MovementType.In,
            Quantity = 10,
        });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RecordAsync(productId, new CreateMovementRequest
            {
                Type = MovementType.Out,
                Quantity = 11,
            }));

        Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);

        // Rejected, not clamped: stock is untouched and no row was written.
        var level = await service.GetStockLevelAsync(productId);
        Assert.Equal(10, level.QuantityOnHand);
        Assert.Equal(1, level.MovementCount);
    }

    [Fact]
    public async Task Stock_may_be_taken_to_exactly_zero()
    {
        var productId = await SeedProductAsync();
        using var context = NewContext();
        var service = NewService(context);

        await service.RecordAsync(productId, new CreateMovementRequest { Type = MovementType.In, Quantity = 10 });
        var movement = await service.RecordAsync(productId, new CreateMovementRequest { Type = MovementType.Out, Quantity = 10 });

        Assert.Equal(0, movement.RunningTotal);
    }

    [Fact]
    public async Task A_negative_Adjustment_is_allowed_while_stock_stays_non_negative()
    {
        var productId = await SeedProductAsync();
        using var context = NewContext();
        var service = NewService(context);

        await service.RecordAsync(productId, new CreateMovementRequest { Type = MovementType.In, Quantity = 500 });

        var movement = await service.RecordAsync(productId, new CreateMovementRequest
        {
            Type = MovementType.Adjustment,
            Quantity = -12,
            Reason = "Stock count correction",
        });

        Assert.Equal(-12, movement.QuantityDelta);
        Assert.Equal(488, movement.RunningTotal);
    }

    [Theory]
    [InlineData(MovementType.In, 0)]
    [InlineData(MovementType.In, -5)]
    [InlineData(MovementType.Out, 0)]
    [InlineData(MovementType.Out, -5)]
    [InlineData(MovementType.Adjustment, 0)]
    public async Task Quantities_that_do_not_match_the_type_are_rejected(MovementType type, int quantity)
    {
        var productId = await SeedProductAsync();
        using var context = NewContext();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            NewService(context).RecordAsync(productId, new CreateMovementRequest
            {
                Type = type,
                Quantity = quantity,
            }));

        Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task A_movement_dated_in_the_future_is_rejected()
    {
        var productId = await SeedProductAsync();
        using var context = NewContext();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            NewService(context).RecordAsync(productId, new CreateMovementRequest
            {
                Type = MovementType.In,
                Quantity = 5,
                OccurredAt = DateTime.UtcNow.AddDays(1),
            }));

        Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task Recording_against_an_unknown_product_is_a_404()
    {
        using var context = NewContext();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            NewService(context).RecordAsync(999, new CreateMovementRequest
            {
                Type = MovementType.In,
                Quantity = 5,
            }));

        Assert.Equal(StatusCodes.Status404NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetForProductAsync_returns_history_oldest_first_with_a_running_total()
    {
        var productId = await SeedProductAsync();
        using var context = NewContext();
        var service = NewService(context);

        var start = DateTime.UtcNow.AddDays(-10);

        await service.RecordAsync(productId, new CreateMovementRequest { Type = MovementType.In, Quantity = 25, OccurredAt = start });
        await service.RecordAsync(productId, new CreateMovementRequest { Type = MovementType.Out, Quantity = 4, OccurredAt = start.AddDays(1) });
        await service.RecordAsync(productId, new CreateMovementRequest { Type = MovementType.Adjustment, Quantity = -1, OccurredAt = start.AddDays(2) });

        var history = await service.GetForProductAsync(productId);

        Assert.Equal(new[] { 25, -4, -1 }, history.Select(m => m.QuantityDelta));
        Assert.Equal(new[] { 25, 21, 20 }, history.Select(m => m.RunningTotal));
    }

    [Fact]
    public async Task GetStockLevelAsync_reports_the_sum_and_the_movement_count()
    {
        var productId = await SeedProductAsync();
        using var context = NewContext();
        var service = NewService(context);

        await service.RecordAsync(productId, new CreateMovementRequest { Type = MovementType.In, Quantity = 25 });
        await service.RecordAsync(productId, new CreateMovementRequest { Type = MovementType.Out, Quantity = 4 });

        var level = await service.GetStockLevelAsync(productId);

        Assert.Equal(21, level.QuantityOnHand);
        Assert.Equal(2, level.MovementCount);
        Assert.Equal("TL-DRL-001", level.Sku);
    }
}
