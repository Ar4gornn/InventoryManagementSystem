using InventoryManagementSystem.Contracts;

namespace InventoryManagementSystem.Application;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);

    Task<CategoryDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken ct = default);

    Task<CategoryDto> UpdateAsync(int id, CategoryRequest request, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}
