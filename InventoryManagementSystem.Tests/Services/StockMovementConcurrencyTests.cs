using InventoryManagementSystem.Application;
using InventoryManagementSystem.Contracts;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using InventoryManagementSystem.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagementSystem.Tests.Services;

/// <summary>
/// The race the balance check exists to survive: two Out movements arriving at once,
/// each of which fits on its own but which together would overdraw the stock.
/// </summary>
/// <remarks>
/// A file-backed database is required here - each caller needs its own connection,
/// and a SQLite :memory: database is private to the connection that opened it.
/// <para>
/// Honest limit: this proves the invariant holds under concurrency on SQLite. It does
/// not prove the serializable transaction is what enforces it. Rerunning this test
/// with the isolation level dropped to ReadCommitted still passed, because SQLite's
/// file-level locking makes a reader wait for an uncommitted writer, so the loser
/// re-reads the balance after the winner commits either way. The stronger isolation
/// is there for a provider with row-level MVCC, where that is not true - and this
/// test would not detect its removal.
/// </para>
/// </remarks>
public class StockMovementConcurrencyTests : IDisposable
{
    private enum Outcome
    {
        Succeeded,
        RefusedByBalanceCheck,
        FailedOnLock,
    }

    private readonly string _dbPath;
    private readonly string _connectionString;

    public StockMovementConcurrencyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"inv-conc-{Guid.NewGuid():N}.db");

        // A generous busy timeout so the second writer waits for the first to commit
        // rather than failing immediately.
        _connectionString = $"Data Source={_dbPath};Default Timeout=30";

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private InventoryContext NewContext() =>
        new(new DbContextOptionsBuilder<InventoryContext>()
            .UseSqlite(_connectionString)
            .Options);

    [Fact]
    public async Task Two_concurrent_Out_movements_cannot_drive_stock_negative()
    {
        int productId;

        using (var seed = NewContext())
        {
            var category = new Category { Name = "Tools" };
            seed.Categories.Add(category);
            await seed.SaveChangesAsync();

            var product = new Product
            {
                Sku = "TL-DRL-001",
                Name = "Drill",
                CategoryId = category.Id,
                CreatedAt = DateTime.UtcNow,
            };
            seed.Products.Add(product);
            await seed.SaveChangesAsync();
            productId = product.Id;

            seed.StockMovements.Add(new StockMovement
            {
                ProductId = productId,
                Type = MovementType.In,
                QuantityDelta = 10,
                OccurredAt = DateTime.UtcNow,
            });
            await seed.SaveChangesAsync();
        }

        // 10 on hand. Two callers each try to remove 6 at the same moment: one must
        // win, and the other must be refused *by the balance check* - not by a lock
        // error. Distinguishing the two is the whole point: SQLite's write lock stops
        // the overdraw either way, so a test that treats SQLITE_BUSY as an acceptable
        // refusal passes even when the transaction is too weak to be correct.
        async Task<Outcome> TryRemoveSix()
        {
            using var context = NewContext();
            var service = new StockMovementService(context, NullLogger<StockMovementService>.Instance);

            try
            {
                await service.RecordAsync(productId, new CreateMovementRequest
                {
                    Type = MovementType.Out,
                    Quantity = 6,
                });
                return Outcome.Succeeded;
            }
            catch (DomainException)
            {
                return Outcome.RefusedByBalanceCheck;
            }
            catch (SqliteException)
            {
                return Outcome.FailedOnLock;
            }
        }

        var results = await Task.WhenAll(TryRemoveSix(), TryRemoveSix());

        Assert.Equal(1, results.Count(r => r == Outcome.Succeeded));
        Assert.Equal(1, results.Count(r => r == Outcome.RefusedByBalanceCheck));
        Assert.DoesNotContain(Outcome.FailedOnLock, results);

        using var verify = NewContext();
        var finalStock = await verify.StockMovements
            .Where(m => m.ProductId == productId)
            .SumAsync(m => m.QuantityDelta);

        Assert.Equal(4, finalStock);
        Assert.True(finalStock >= 0, "stock must never be negative");
    }
}
