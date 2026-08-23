using System.Data;
using InventoryManagementSystem.Application;
using InventoryManagementSystem.Contracts;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementSystem.Services;

public class StockMovementService : IStockMovementService
{
    private readonly InventoryContext _context;
    private readonly ILogger<StockMovementService> _logger;

    public StockMovementService(InventoryContext context, ILogger<StockMovementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetForProductAsync(int productId, CancellationToken ct = default)
    {
        await EnsureProductExists(productId, ct);

        var movements = await _context.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.Id)
            .ToListAsync(ct);

        // The running total is a sequential fold, so it is computed here rather than
        // asked of the database - SQLite window functions would work, but this stays
        // portable and the list is already materialised.
        var runningTotal = 0;
        var result = new List<StockMovementDto>(movements.Count);

        foreach (var m in movements)
        {
            runningTotal += m.QuantityDelta;
            result.Add(new StockMovementDto(
                m.Id, m.ProductId, m.Type, m.QuantityDelta, runningTotal, m.Reason, m.OccurredAt));
        }

        return result;
    }

    public async Task<StockLevelDto> GetStockLevelAsync(int productId, CancellationToken ct = default)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new
            {
                p.Id,
                p.Sku,
                Quantity = p.Movements.Sum(m => (int?)m.QuantityDelta) ?? 0,
                Count = p.Movements.Count(),
            })
            .SingleOrDefaultAsync(ct)
            ?? throw DomainException.NotFound($"Product {productId} does not exist.");

        return new StockLevelDto(product.Id, product.Sku, product.Quantity, product.Count);
    }

    public async Task<StockMovementDto> RecordAsync(
        int productId,
        CreateMovementRequest request,
        CancellationToken ct = default)
    {
        var delta = ToSignedDelta(request);
        var occurredAt = request.OccurredAt ?? DateTime.UtcNow;

        if (occurredAt > DateTime.UtcNow)
        {
            throw new DomainException("OccurredAt cannot be in the future.");
        }

        // Read the balance and write the movement in one transaction, so no other
        // caller can slip a movement in between the two and overdraw the stock.
        //
        // Serializable maps to BEGIN IMMEDIATE in Microsoft.Data.Sqlite, taking the
        // write lock up front. On SQLite this is belt and braces: its file-level
        // locking already serialises readers against an uncommitted writer, and a
        // concurrency test could not produce an overdraw even with ReadCommitted.
        // It matters on a provider with row-level MVCC such as PostgreSQL, where
        // readers do not block and two transactions genuinely can read the same
        // balance and both decide there is enough stock.
        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        await EnsureProductExists(productId, ct);

        var currentStock = await _context.StockMovements
            .Where(m => m.ProductId == productId)
            .SumAsync(m => (int?)m.QuantityDelta, ct) ?? 0;

        var resulting = currentStock + delta;

        if (resulting < 0)
        {
            // Rejected, never clamped. Silently writing a smaller movement would make
            // the history disagree with what the caller was told happened.
            throw new DomainException(
                $"Stock cannot go negative. Product {productId} has {currentStock} on hand " +
                $"and this movement would leave {resulting}.");
        }

        var movement = new StockMovement
        {
            ProductId = productId,
            Type = request.Type,
            QuantityDelta = delta,
            Reason = request.Reason?.Trim(),
            OccurredAt = occurredAt,
        };

        _context.StockMovements.Add(movement);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "Recorded {Type} of {Delta} for product {ProductId}; stock now {Stock}",
            movement.Type, delta, productId, resulting);

        return new StockMovementDto(
            movement.Id, movement.ProductId, movement.Type, movement.QuantityDelta,
            resulting, movement.Reason, movement.OccurredAt);
    }

    /// <summary>
    /// Turns the caller's quantity into the signed delta stored on the row, and
    /// rejects combinations that do not make sense.
    /// </summary>
    private static int ToSignedDelta(CreateMovementRequest request) => request.Type switch
    {
        MovementType.In when request.Quantity > 0 => request.Quantity,
        MovementType.In => throw new DomainException("An In movement needs a quantity above zero."),

        MovementType.Out when request.Quantity > 0 => -request.Quantity,
        MovementType.Out => throw new DomainException(
            "An Out movement needs a positive quantity - the direction comes from the type."),

        MovementType.Adjustment when request.Quantity != 0 => request.Quantity,
        MovementType.Adjustment => throw new DomainException(
            "An Adjustment needs a non-zero quantity, positive or negative."),

        _ => throw new DomainException("Type must be In, Out or Adjustment."),
    };

    private async Task EnsureProductExists(int productId, CancellationToken ct)
    {
        if (!await _context.Products.AnyAsync(p => p.Id == productId, ct))
        {
            throw DomainException.NotFound($"Product {productId} does not exist.");
        }
    }
}
