namespace InventoryManagementSystem.Domain.Entities;

/// <summary>
/// Why stock moved. Classification only - the arithmetic lives in
/// <see cref="StockMovement.QuantityDelta"/>, which is signed.
/// </summary>
public enum MovementType
{
    /// <summary>Stock arriving. Delta must be positive.</summary>
    In = 1,

    /// <summary>Stock leaving. Delta must be negative.</summary>
    Out = 2,

    /// <summary>A correction after a stock count. Delta may go either way, but not zero.</summary>
    Adjustment = 3,
}
