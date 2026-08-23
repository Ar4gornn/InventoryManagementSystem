using System.ComponentModel.DataAnnotations;

namespace InventoryManagementSystem.Contracts;

/// <summary>
/// A product as returned by the API. <see cref="QuantityOnHand"/> is computed from
/// the movement log on read; it is not stored anywhere.
/// </summary>
public record ProductDto(
    int Id,
    string Sku,
    string Name,
    string? Description,
    int CategoryId,
    string CategoryName,
    int QuantityOnHand,
    DateTime CreatedAt);

public class CreateProductRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Sku is required.")]
    [MaxLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required.")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "CategoryId is required.")]
    public int CategoryId { get; set; }
}

public class UpdateProductRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required.")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "CategoryId is required.")]
    public int CategoryId { get; set; }
}
