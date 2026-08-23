using InventoryManagementSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementSystem.Persistence;

/// <summary>
/// Puts a handful of rows in an empty database so Swagger shows something real
/// the first time it is opened, instead of empty arrays.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Idempotent: does nothing at all if any category already exists, so
    /// restarting the app never duplicates rows or overwrites real data.
    /// </summary>
    public static async Task EnsureSeededAsync(InventoryContext context, CancellationToken ct = default)
    {
        if (await context.Categories.AnyAsync(ct))
        {
            return;
        }

        var tools = new Category { Name = "Tools", Description = "Hand and power tools" };
        var fasteners = new Category { Name = "Fasteners", Description = "Screws, bolts and fixings" };
        var safety = new Category { Name = "Safety", Description = "Personal protective equipment" };

        context.Categories.AddRange(tools, fasteners, safety);

        // Fixed relative to a single "now" so the seeded history reads sensibly
        // whenever it happens to be created.
        var now = DateTime.UtcNow;
        var created = now.AddDays(-30);

        var drill = new Product
        {
            Sku = "TL-DRL-001",
            Name = "Cordless drill 18V",
            Description = "Two-speed, brushless, supplied without battery",
            Category = tools,
            CreatedAt = created,
        };

        var screws = new Product
        {
            Sku = "FS-SCR-050",
            Name = "Wood screw 4x50mm (box of 200)",
            Category = fasteners,
            CreatedAt = created,
        };

        var goggles = new Product
        {
            Sku = "SF-GOG-010",
            Name = "Safety goggles, clear",
            Category = safety,
            CreatedAt = created,
        };

        context.Products.AddRange(drill, screws, goggles);

        context.StockMovements.AddRange(
            new StockMovement
            {
                Product = drill,
                Type = MovementType.In,
                QuantityDelta = 25,
                Reason = "Opening stock",
                OccurredAt = now.AddDays(-30),
            },
            new StockMovement
            {
                Product = drill,
                Type = MovementType.Out,
                QuantityDelta = -4,
                Reason = "Sales order 1041",
                OccurredAt = now.AddDays(-6),
            },
            new StockMovement
            {
                Product = screws,
                Type = MovementType.In,
                QuantityDelta = 500,
                Reason = "Opening stock",
                OccurredAt = now.AddDays(-30),
            },
            new StockMovement
            {
                Product = screws,
                Type = MovementType.Adjustment,
                QuantityDelta = -12,
                Reason = "Stock count correction",
                OccurredAt = now.AddDays(-2),
            },
            new StockMovement
            {
                Product = goggles,
                Type = MovementType.In,
                QuantityDelta = 80,
                Reason = "Opening stock",
                OccurredAt = now.AddDays(-30),
            });

        await context.SaveChangesAsync(ct);
    }
}
