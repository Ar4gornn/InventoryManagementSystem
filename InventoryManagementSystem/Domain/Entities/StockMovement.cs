namespace InventoryManagementSystem.Domain.Entities;

/// <summary>
/// One append-only entry in a product's stock history. Rows are never updated or
/// deleted: a mistake is corrected by writing a compensating movement, which is
/// why the log can be trusted as the source of truth for stock on hand.
/// </summary>
public class StockMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public MovementType Type { get; set; }

    /// <summary>
    /// Signed change in units: positive adds stock, negative removes it. Never zero.
    /// Stock on hand is the SUM of this column for a product, so no separate
    /// quantity field can drift out of step with the history.
    /// </summary>
    public int QuantityDelta { get; set; }

    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }
}
