using System.ComponentModel.DataAnnotations;

namespace InventoryManagementSystem.Contracts;

/// <summary>
/// A category as returned by the API. <see cref="ProductCount"/> is counted on read
/// so callers can see what a delete would be blocked by.
/// </summary>
public record CategoryDto(
    int Id,
    string Name,
    string? Description,
    int ProductCount);

public class CategoryRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
