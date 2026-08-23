namespace InventoryManagementSystem.Domain.Entities;

/// <summary>
/// A grouping products belong to. Names are unique so a category can be referred
/// to by name in imports without ambiguity.
/// </summary>
public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
