namespace InventoryManagementSystem.Domain.Entities;

/// <summary>
/// Something held in stock.
/// </summary>
/// <remarks>
/// There is deliberately no Quantity column. Stock on hand is the sum of this
/// product's <see cref="StockMovement"/> rows, so the movement log is the single
/// source of truth and the two can never disagree.
/// </remarks>
public class Product
{
    public int Id { get; set; }

    /// <summary>Stock keeping unit. Unique across all products.</summary>
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
}
