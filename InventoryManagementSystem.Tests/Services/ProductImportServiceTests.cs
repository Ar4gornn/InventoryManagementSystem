using System.Text;
using InventoryManagementSystem.Application;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using InventoryManagementSystem.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagementSystem.Tests.Services;

public class ProductImportServiceTests : IDisposable
{
    private const string Header = "sku,name,description,category,quantity";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<InventoryContext> _options;

    public ProductImportServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<InventoryContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new InventoryContext(_options);
        context.Database.EnsureCreated();
        context.Categories.AddRange(
            new Category { Name = "Tools" },
            new Category { Name = "Safety" });
        context.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private InventoryContext NewContext() => new(_options);

    private static ProductImportService NewService(InventoryContext context) =>
        new(context, NullLogger<ProductImportService>.Instance);

    private static Stream Csv(params string[] lines) =>
        new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', lines)));

    [Fact]
    public async Task A_clean_file_imports_every_row()
    {
        using var context = NewContext();

        var result = await NewService(context).ImportAsync(Csv(
            Header,
            "TL-001,Hammer,A hammer,Tools,10",
            "SF-001,Goggles,,Safety,5"));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, await context.Products.CountAsync());
    }

    [Fact]
    public async Task Opening_quantity_becomes_a_stock_movement_not_a_column()
    {
        using var context = NewContext();

        await NewService(context).ImportAsync(Csv(Header, "TL-001,Hammer,,Tools,10"));

        var product = await context.Products.SingleAsync();
        var movements = await context.StockMovements.Where(m => m.ProductId == product.Id).ToListAsync();

        var movement = Assert.Single(movements);
        Assert.Equal(MovementType.In, movement.Type);
        Assert.Equal(10, movement.QuantityDelta);
    }

    [Fact]
    public async Task Zero_quantity_creates_no_movement_at_all()
    {
        using var context = NewContext();

        await NewService(context).ImportAsync(Csv(Header, "TL-001,Hammer,,Tools,0"));

        Assert.Empty(await context.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task Bad_rows_fail_individually_and_good_rows_still_import()
    {
        using var context = NewContext();

        var result = await NewService(context).ImportAsync(Csv(
            Header,
            "TL-001,Hammer,,Tools,10",          // fine
            ",Nameless,,Tools,1",               // no sku
            "TL-002,,,Tools,1",                 // no name
            "TL-003,Saw,,Nonexistent,1",        // unknown category
            "TL-004,Drill,,Tools,not-a-number", // bad quantity
            "TL-005,Wrench,,Tools,-5",          // negative quantity
            "SF-001,Goggles,,Safety,5"));       // fine

        Assert.Equal(7, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(5, result.FailedCount);
        Assert.Equal(2, await context.Products.CountAsync());

        // Line numbers include the header, so they match a text editor.
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, result.Rows.Where(r => !r.Imported).Select(r => r.Line));
        Assert.Contains("Sku is required", result.Rows.Single(r => r.Line == 3).Error);
        Assert.Contains("Unknown category", result.Rows.Single(r => r.Line == 5).Error);
    }

    [Fact]
    public async Task A_sku_already_in_the_database_is_rejected()
    {
        using (var seed = NewContext())
        {
            var categoryId = await seed.Categories.Select(c => c.Id).FirstAsync();
            seed.Products.Add(new Product
            {
                Sku = "TL-001",
                Name = "Existing hammer",
                CategoryId = categoryId,
                CreatedAt = DateTime.UtcNow,
            });
            await seed.SaveChangesAsync();
        }

        using var context = NewContext();
        var result = await NewService(context).ImportAsync(Csv(Header, "TL-001,Hammer,,Tools,10"));

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains("already exists", result.Rows.Single().Error);
    }

    [Fact]
    public async Task A_sku_duplicated_within_the_same_file_is_rejected_the_second_time()
    {
        using var context = NewContext();

        var result = await NewService(context).ImportAsync(Csv(
            Header,
            "TL-001,Hammer,,Tools,10",
            "TL-001,Hammer again,,Tools,10"));

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Single(await context.Products.ToListAsync());
    }

    [Fact]
    public async Task A_quoted_field_containing_a_comma_does_not_shift_the_columns()
    {
        using var context = NewContext();

        var result = await NewService(context).ImportAsync(Csv(
            Header,
            "TL-001,Hammer,\"Heavy, steel, and long\",Tools,10"));

        Assert.Equal(1, result.ImportedCount);
        var product = await context.Products.SingleAsync();
        Assert.Equal("Heavy, steel, and long", product.Description);
    }

    [Fact]
    public async Task Blank_lines_are_skipped_rather_than_reported_as_failures()
    {
        using var context = NewContext();

        var result = await NewService(context).ImportAsync(Csv(
            Header,
            "TL-001,Hammer,,Tools,10",
            "",
            "SF-001,Goggles,,Safety,5",
            ""));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task A_wrong_header_is_rejected_before_anything_is_read()
    {
        using var context = NewContext();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            NewService(context).ImportAsync(Csv("name,sku,whatever", "TL-001,Hammer,x")));

        Assert.Contains("header", ex.Message);
        Assert.Empty(await context.Products.ToListAsync());
    }

    [Fact]
    public async Task An_empty_file_is_rejected()
    {
        using var context = NewContext();

        await Assert.ThrowsAsync<DomainException>(() =>
            NewService(context).ImportAsync(new MemoryStream()));
    }

    [Fact]
    public async Task A_row_with_too_few_columns_is_reported_not_crashed_on()
    {
        using var context = NewContext();

        var result = await NewService(context).ImportAsync(Csv(Header, "TL-001,Hammer"));

        Assert.Equal(1, result.FailedCount);
        Assert.Contains("5 columns", result.Rows.Single().Error);
    }
}
