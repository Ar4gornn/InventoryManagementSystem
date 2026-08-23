using InventoryManagementSystem.Contracts;

namespace InventoryManagementSystem.Application;

public interface IProductImportService
{
    /// <summary>
    /// Imports products from CSV with the header
    /// <c>sku,name,description,category,quantity</c>.
    /// </summary>
    Task<ImportResult> ImportAsync(Stream csv, CancellationToken ct = default);
}
