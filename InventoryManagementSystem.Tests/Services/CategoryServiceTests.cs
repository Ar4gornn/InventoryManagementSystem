using InventoryManagementSystem.Application;
using InventoryManagementSystem.Contracts;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using InventoryManagementSystem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagementSystem.Tests.Services;

public class CategoryServiceTests
{
    private static InventoryContext NewContext() =>
        new(new DbContextOptionsBuilder<InventoryContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CategoryService NewService(InventoryContext context) =>
        new(context, NullLogger<CategoryService>.Instance);

    [Fact]
    public async Task CreateAsync_persists_the_category_with_no_products()
    {
        using var context = NewContext();

        var created = await NewService(context).CreateAsync(new CategoryRequest
        {
            Name = "  Tools  ",
            Description = "  Hand and power tools  ",
        });

        Assert.Equal("Tools", created.Name);
        Assert.Equal("Hand and power tools", created.Description);
        Assert.Equal(0, created.ProductCount);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_name_with_409()
    {
        using var context = NewContext();
        var service = NewService(context);

        await service.CreateAsync(new CategoryRequest { Name = "Tools" });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(new CategoryRequest { Name = "Tools" }));

        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
        Assert.Single(await context.Categories.ToListAsync());
    }

    [Fact]
    public async Task UpdateAsync_allows_a_category_to_keep_its_own_name()
    {
        using var context = NewContext();
        var service = NewService(context);

        var created = await service.CreateAsync(new CategoryRequest { Name = "Tools" });

        // The uniqueness check must exclude the row being updated, or changing only
        // the description would collide with itself.
        var updated = await service.UpdateAsync(created.Id, new CategoryRequest
        {
            Name = "Tools",
            Description = "Now with a description",
        });

        Assert.Equal("Tools", updated.Name);
        Assert.Equal("Now with a description", updated.Description);
    }

    [Fact]
    public async Task UpdateAsync_rejects_taking_another_categorys_name()
    {
        using var context = NewContext();
        var service = NewService(context);

        await service.CreateAsync(new CategoryRequest { Name = "Tools" });
        var safety = await service.CreateAsync(new CategoryRequest { Name = "Safety" });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateAsync(safety.Id, new CategoryRequest { Name = "Tools" }));

        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_rejects_an_unknown_category_with_404()
    {
        using var context = NewContext();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            NewService(context).UpdateAsync(999, new CategoryRequest { Name = "Nothing" }));

        Assert.Equal(StatusCodes.Status404NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteAsync_removes_an_empty_category()
    {
        using var context = NewContext();
        var service = NewService(context);

        var created = await service.CreateAsync(new CategoryRequest { Name = "Tools" });

        await service.DeleteAsync(created.Id);

        Assert.Empty(await context.Categories.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_refuses_while_products_still_belong_to_it()
    {
        using var context = NewContext();
        var service = NewService(context);

        var created = await service.CreateAsync(new CategoryRequest { Name = "Tools" });

        context.Products.Add(new Product
        {
            Sku = "TL-DRL-001",
            Name = "Drill",
            CategoryId = created.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.DeleteAsync(created.Id));

        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
        Assert.Contains("1 product", ex.Message);
        Assert.Single(await context.Categories.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_rejects_an_unknown_category_with_404()
    {
        using var context = NewContext();

        var ex = await Assert.ThrowsAsync<DomainException>(() => NewService(context).DeleteAsync(999));

        Assert.Equal(StatusCodes.Status404NotFound, ex.StatusCode);
    }
}
