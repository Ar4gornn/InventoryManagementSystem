using InventoryManagementSystem.Contracts;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using InventoryManagementSystem.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagementSystem.Tests.Services;

/// <summary>
/// Paging and filtering against real SQLite - these are the queries most likely to
/// fail translation, and the InMemory provider would not notice.
/// </summary>
public class ProductPagingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<InventoryContext> _options;

    public ProductPagingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<InventoryContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new InventoryContext(_options);
        context.Database.EnsureCreated();

        var tools = new Category { Name = "Tools" };
        var safety = new Category { Name = "Safety" };
        context.Categories.AddRange(tools, safety);
        context.SaveChanges();

        ToolsId = tools.Id;
        SafetyId = safety.Id;

        for (var i = 1; i <= 25; i++)
        {
            context.Products.Add(new Product
            {
                Sku = $"TL-{i:D3}",
                Name = $"Tool number {i}",
                CategoryId = tools.Id,
                CreatedAt = DateTime.UtcNow,
            });
        }

        context.Products.Add(new Product
        {
            Sku = "SF-GOG-001",
            Name = "Safety goggles",
            CategoryId = safety.Id,
            CreatedAt = DateTime.UtcNow,
        });

        context.SaveChanges();
    }

    private int ToolsId { get; }

    private int SafetyId { get; }

    public void Dispose() => _connection.Dispose();

    private ProductService NewService() =>
        new(new InventoryContext(_options), NullLogger<ProductService>.Instance);

    [Fact]
    public async Task TotalCount_reports_matches_not_the_size_of_the_page()
    {
        var page = await NewService().GetAsync(new ProductQuery { Page = 1, PageSize = 10 });

        Assert.Equal(10, page.Items.Count);
        Assert.Equal(26, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task The_last_page_is_partial_and_reports_no_next_page()
    {
        var page = await NewService().GetAsync(new ProductQuery { Page = 3, PageSize = 10 });

        Assert.Equal(6, page.Items.Count);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task A_page_beyond_the_end_is_empty_rather_than_an_error()
    {
        var page = await NewService().GetAsync(new ProductQuery { Page = 99, PageSize = 10 });

        Assert.Empty(page.Items);
        Assert.Equal(26, page.TotalCount);
    }

    [Fact]
    public async Task PageSize_is_capped()
    {
        var page = await NewService().GetAsync(new ProductQuery { PageSize = 100_000 });

        Assert.Equal(PageQuery.MaxPageSize, page.PageSize);
        Assert.True(page.Items.Count <= PageQuery.MaxPageSize);
    }

    [Fact]
    public async Task Page_below_one_is_clamped_rather_than_producing_a_negative_skip()
    {
        // Without clamping this becomes Skip(-10), which SQLite rejects outright.
        var page = await NewService().GetAsync(new ProductQuery { Page = 0, PageSize = 10 });

        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.Items.Count);
    }

    [Fact]
    public async Task Filtering_by_category_narrows_the_total_as_well_as_the_page()
    {
        var page = await NewService().GetAsync(new ProductQuery { CategoryId = SafetyId });

        Assert.Equal(1, page.TotalCount);
        Assert.Equal("SF-GOG-001", page.Items.Single().Sku);
    }

    [Fact]
    public async Task Search_matches_the_name_case_insensitively()
    {
        var page = await NewService().GetAsync(new ProductQuery { Search = "goggles" });

        Assert.Equal("SF-GOG-001", page.Items.Single().Sku);
    }

    [Fact]
    public async Task Search_also_matches_the_sku()
    {
        var page = await NewService().GetAsync(new ProductQuery { Search = "SF-GOG" });

        Assert.Single(page.Items);
    }

    [Fact]
    public async Task Search_and_category_filter_apply_together()
    {
        var page = await NewService().GetAsync(new ProductQuery
        {
            Search = "goggles",
            CategoryId = ToolsId,
        });

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Search_that_matches_nothing_returns_an_empty_page()
    {
        var page = await NewService().GetAsync(new ProductQuery { Search = "nothing matches this" });

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }
}
