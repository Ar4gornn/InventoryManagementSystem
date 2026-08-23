using InventoryManagementSystem.Application;
using InventoryManagementSystem.Contracts;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementSystem.Services;

public class CategoryService : ICategoryService
{
    private readonly InventoryContext _context;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(InventoryContext context, ILogger<CategoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<CategoryDto>> GetAsync(CategoryQuery query, CancellationToken ct = default)
    {
        var categories = _context.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            categories = categories.Where(c => EF.Functions.Like(c.Name, pattern));
        }

        var totalCount = await categories.CountAsync(ct);

        // Order before projecting: EF cannot translate an OrderBy over a projected
        // record that wraps a subquery.
        var items = await Project(categories
                .OrderBy(c => c.Name)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize))
            .ToListAsync(ct);

        return new PagedResult<CategoryDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await Project(_context.Categories.AsNoTracking().Where(c => c.Id == id))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();

        await EnsureNameIsFree(name, exceptId: null, ct);

        var category = new Category
        {
            Name = name,
            Description = request.Description?.Trim(),
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created category {Name} (id {Id})", category.Name, category.Id);

        return await GetByIdAsync(category.Id, ct)
               ?? throw new InvalidOperationException("Category vanished immediately after creation.");
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryRequest request, CancellationToken ct = default)
    {
        var category = await _context.Categories.SingleOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw DomainException.NotFound($"Category {id} does not exist.");

        var name = request.Name.Trim();

        // Excluding itself, or renaming a category to the name it already has would
        // collide with its own row.
        await EnsureNameIsFree(name, exceptId: id, ct);

        category.Name = name;
        category.Description = request.Description?.Trim();

        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct)
               ?? throw new InvalidOperationException("Category vanished immediately after update.");
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await _context.Categories.SingleOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw DomainException.NotFound($"Category {id} does not exist.");

        // The FK is Restrict, so the database would refuse this anyway - but it would
        // arrive as a DbUpdateException and a 500. Check first and answer with a 409
        // that says how many products are in the way.
        var productCount = await _context.Products.CountAsync(p => p.CategoryId == id, ct);
        if (productCount > 0)
        {
            throw DomainException.Conflict(
                $"Category {id} still has {productCount} product(s). " +
                "Move or delete them first.");
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted category {Name} (id {Id})", category.Name, id);
    }

    private async Task EnsureNameIsFree(string name, int? exceptId, CancellationToken ct)
    {
        var taken = await _context.Categories
            .AnyAsync(c => c.Name == name && (exceptId == null || c.Id != exceptId), ct);

        if (taken)
        {
            throw DomainException.Conflict($"A category named '{name}' already exists.");
        }
    }

    private static IQueryable<CategoryDto> Project(IQueryable<Category> query) =>
        query.Select(c => new CategoryDto(
            c.Id,
            c.Name,
            c.Description,
            c.Products.Count()));
}
