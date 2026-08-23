using InventoryManagementSystem.Application;
using Microsoft.AspNetCore.Http;
using InventoryManagementSystem.Contracts;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using InventoryManagementSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagementSystem.Tests.Services;

public class ProductServiceTests
{
    // A fresh database per test: no ordering dependencies, no shared state.
    private static InventoryContext NewContext() =>
        new(new DbContextOptionsBuilder<InventoryContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProductService NewService(InventoryContext context) =>
        new(context, NullLogger<ProductService>.Instance);

    private static async Task<Category> SeedCategoryAsync(InventoryContext context, string name = "Tools")
    {
        var category = new Category { Name = name };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    [Fact]
    public async Task CreateAsync_persists_the_product_and_starts_it_at_zero_stock()
    {
        using var context = NewContext();
        var category = await SeedCategoryAsync(context);
        var service = NewService(context);

        var created = await service.CreateAsync(new CreateProductRequest
        {
            Sku = "TL-HAM-001",
            Name = "Claw hammer",
            CategoryId = category.Id,
        });

        Assert.Equal("TL-HAM-001", created.Sku);
        Assert.Equal("Claw hammer", created.Name);
        Assert.Equal(category.Id, created.CategoryId);
        Assert.Equal("Tools", created.CategoryName);
        Assert.Equal(0, created.QuantityOnHand);
        Assert.Single(await context.Products.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_trims_whitespace_from_sku_and_name()
    {
        using var context = NewContext();
        var category = await SeedCategoryAsync(context);
        var service = NewService(context);

        var created = await service.CreateAsync(new CreateProductRequest
        {
            Sku = "  TL-SAW-002  ",
            Name = "  Hand saw  ",
            CategoryId = category.Id,
        });

        Assert.Equal("TL-SAW-002", created.Sku);
        Assert.Equal("Hand saw", created.Name);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_sku_with_409()
    {
        using var context = NewContext();
        var category = await SeedCategoryAsync(context);
        var service = NewService(context);

        await service.CreateAsync(new CreateProductRequest
        {
            Sku = "TL-HAM-001",
            Name = "Claw hammer",
            CategoryId = category.Id,
        });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(new CreateProductRequest
            {
                Sku = "TL-HAM-001",
                Name = "A different hammer",
                CategoryId = category.Id,
            }));

        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
        Assert.Single(await context.Products.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_rejects_an_unknown_category_with_404()
    {
        using var context = NewContext();
        var service = NewService(context);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(new CreateProductRequest
            {
                Sku = "TL-HAM-001",
                Name = "Claw hammer",
                CategoryId = 999,
            }));

        Assert.Equal(StatusCodes.Status404NotFound, ex.StatusCode);
        Assert.Empty(await context.Products.ToListAsync());
    }

    [Fact]
    public async Task GetByIdAsync_computes_stock_on_hand_from_the_movement_log()
    {
        using var context = NewContext();
        var category = await SeedCategoryAsync(context);

        var product = new Product
        {
            Sku = "TL-DRL-001",
            Name = "Drill",
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow,
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        context.StockMovements.AddRange(
            new StockMovement { ProductId = product.Id, Type = MovementType.In, QuantityDelta = 25, OccurredAt = DateTime.UtcNow },
            new StockMovement { ProductId = product.Id, Type = MovementType.Out, QuantityDelta = -4, OccurredAt = DateTime.UtcNow },
            new StockMovement { ProductId = product.Id, Type = MovementType.Adjustment, QuantityDelta = -1, OccurredAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var dto = await NewService(context).GetByIdAsync(product.Id);

        Assert.NotNull(dto);
        Assert.Equal(20, dto!.QuantityOnHand);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_an_unknown_id()
    {
        using var context = NewContext();

        Assert.Null(await NewService(context).GetByIdAsync(4242));
    }

    [Fact]
    public async Task UpdateAsync_changes_the_name_and_category_but_never_the_sku()
    {
        using var context = NewContext();
        var tools = await SeedCategoryAsync(context);
        var safety = await SeedCategoryAsync(context, "Safety");
        var service = NewService(context);

        var created = await service.CreateAsync(new CreateProductRequest
        {
            Sku = "TL-HAM-001",
            Name = "Claw hammer",
            CategoryId = tools.Id,
        });

        var updated = await service.UpdateAsync(created.Id, new UpdateProductRequest
        {
            Name = "Claw hammer, 16oz",
            Description = "Fibreglass handle",
            CategoryId = safety.Id,
        });

        Assert.Equal("Claw hammer, 16oz", updated.Name);
        Assert.Equal("Fibreglass handle", updated.Description);
        Assert.Equal(safety.Id, updated.CategoryId);
        Assert.Equal("TL-HAM-001", updated.Sku);
    }

    [Fact]
    public async Task UpdateAsync_rejects_an_unknown_product_with_404()
    {
        using var context = NewContext();
        var category = await SeedCategoryAsync(context);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            NewService(context).UpdateAsync(999, new UpdateProductRequest
            {
                Name = "Nothing",
                CategoryId = category.Id,
            }));

        Assert.Equal(StatusCodes.Status404NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteAsync_removes_a_product_that_has_no_history()
    {
        using var context = NewContext();
        var category = await SeedCategoryAsync(context);
        var service = NewService(context);

        var created = await service.CreateAsync(new CreateProductRequest
        {
            Sku = "TL-HAM-001",
            Name = "Claw hammer",
            CategoryId = category.Id,
        });

        await service.DeleteAsync(created.Id);

        Assert.Empty(await context.Products.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_refuses_to_destroy_a_product_that_has_stock_history()
    {
        using var context = NewContext();
        var category = await SeedCategoryAsync(context);
        var service = NewService(context);

        var created = await service.CreateAsync(new CreateProductRequest
        {
            Sku = "TL-DRL-001",
            Name = "Drill",
            CategoryId = category.Id,
        });

        context.StockMovements.Add(new StockMovement
        {
            ProductId = created.Id,
            Type = MovementType.In,
            QuantityDelta = 10,
            OccurredAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.DeleteAsync(created.Id));

        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
        Assert.Single(await context.Products.ToListAsync());
        Assert.Single(await context.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_rejects_an_unknown_product_with_404()
    {
        using var context = NewContext();

        var ex = await Assert.ThrowsAsync<DomainException>(() => NewService(context).DeleteAsync(999));

        Assert.Equal(StatusCodes.Status404NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetAllAsync_returns_products_ordered_by_sku()
    {
        using var context = NewContext();
        var category = await SeedCategoryAsync(context);
        var service = NewService(context);

        foreach (var sku in new[] { "ZZ-001", "AA-001", "MM-001" })
        {
            await service.CreateAsync(new CreateProductRequest
            {
                Sku = sku,
                Name = sku,
                CategoryId = category.Id,
            });
        }

        var all = await service.GetAllAsync();

        Assert.Equal(new[] { "AA-001", "MM-001", "ZZ-001" }, all.Select(p => p.Sku));
    }
}
