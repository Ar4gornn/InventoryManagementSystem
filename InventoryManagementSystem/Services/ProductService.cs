using InventoryManagementSystem.Application;
using InventoryManagementSystem.Contracts;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementSystem.Services;

public class ProductService : IProductService
{
    private readonly InventoryContext _context;
    private readonly ILogger<ProductService> _logger;

    public ProductService(InventoryContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
    {
        // Order before projecting. Sorting the projected DTO makes EF try to translate
        // an ORDER BY over a constructed record wrapping the stock subquery, which it
        // cannot do - it throws at runtime rather than falling back to the client.
        return await Project(_context.Products.AsNoTracking().OrderBy(p => p.Sku))
            .ToListAsync(ct);
    }

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await Project(_context.Products.AsNoTracking().Where(p => p.Id == id))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var sku = request.Sku.Trim();

        // Checked here rather than relying on the unique index, so the caller gets a
        // 409 with a useful message instead of a database exception surfacing as a 500.
        if (await _context.Products.AnyAsync(p => p.Sku == sku, ct))
        {
            throw DomainException.Conflict($"A product with SKU '{sku}' already exists.");
        }

        if (!await _context.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw DomainException.NotFound($"Category {request.CategoryId} does not exist.");
        }

        var product = new Product
        {
            Sku = sku,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CategoryId = request.CategoryId,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created product {Sku} (id {Id})", product.Sku, product.Id);

        return await GetByIdAsync(product.Id, ct)
               ?? throw new InvalidOperationException("Product vanished immediately after creation.");
    }

    public async Task<ProductDto> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _context.Products.SingleOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw DomainException.NotFound($"Product {id} does not exist.");

        if (!await _context.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw DomainException.NotFound($"Category {request.CategoryId} does not exist.");
        }

        // Sku is deliberately not updatable: it identifies the product in the movement
        // history and in any external system that has already recorded it.
        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.CategoryId = request.CategoryId;

        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(product.Id, ct)
               ?? throw new InvalidOperationException("Product vanished immediately after update.");
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await _context.Products.SingleOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw DomainException.NotFound($"Product {id} does not exist.");

        // The database would cascade the movements away. Refuse instead: the stock
        // history is the record of what actually happened, and deleting a product is
        // not a good enough reason to destroy it.
        if (await _context.StockMovements.AnyAsync(m => m.ProductId == id, ct))
        {
            throw DomainException.Conflict(
                $"Product {id} has stock movements and cannot be deleted. " +
                "Its history would be lost.");
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted product {Sku} (id {Id})", product.Sku, id);
    }

    /// <summary>
    /// Shapes a product query into the DTO, summing the movement log for stock on
    /// hand. Kept in one place so every read computes quantity the same way.
    /// </summary>
    private static IQueryable<ProductDto> Project(IQueryable<Product> query) =>
        query.Select(p => new ProductDto(
            p.Id,
            p.Sku,
            p.Name,
            p.Description,
            p.CategoryId,
            p.Category!.Name,
            p.Movements.Sum(m => (int?)m.QuantityDelta) ?? 0,
            p.CreatedAt));
}
