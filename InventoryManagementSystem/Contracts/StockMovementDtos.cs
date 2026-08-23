using System.ComponentModel.DataAnnotations;
using InventoryManagementSystem.Domain.Entities;

namespace InventoryManagementSystem.Contracts;

/// <summary>
/// One entry in a product's stock history. <see cref="QuantityDelta"/> is the signed
/// change actually stored; <see cref="RunningTotal"/> is the stock on hand after this
/// movement, so a caller can read the history without re-summing it.
/// </summary>
public record StockMovementDto(
    int Id,
    int ProductId,
    MovementType Type,
    int QuantityDelta,
    int RunningTotal,
    string? Reason,
    DateTime OccurredAt);

/// <summary>
/// Records a movement. Quantity is expressed the way a human would say it.
/// </summary>
public class CreateMovementRequest
{
    /// <summary>In, Out or Adjustment.</summary>
    [Required]
    [EnumDataType(typeof(MovementType), ErrorMessage = "Type must be In, Out or Adjustment.")]
    public MovementType Type { get; set; }

    /// <summary>
    /// For <c>In</c> and <c>Out</c>: a positive number of units. Sending 5 with Out
    /// removes five - callers are never asked to send a negative.
    /// For <c>Adjustment</c>: a signed correction, positive or negative, never zero.
    /// </summary>
    public int Quantity { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>When it happened. Defaults to now if omitted. May not be in the future.</summary>
    public DateTime? OccurredAt { get; set; }
}

/// <summary>Current stock for a product, with the movement count behind it.</summary>
public record StockLevelDto(int ProductId, string Sku, int QuantityOnHand, int MovementCount);
