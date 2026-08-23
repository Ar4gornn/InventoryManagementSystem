using InventoryManagementSystem.Contracts;

namespace InventoryManagementSystem.Application;

public interface IStockMovementService
{
    Task<IReadOnlyList<StockMovementDto>> GetForProductAsync(int productId, CancellationToken ct = default);

    Task<StockLevelDto> GetStockLevelAsync(int productId, CancellationToken ct = default);

    Task<StockMovementDto> RecordAsync(int productId, CreateMovementRequest request, CancellationToken ct = default);
}
