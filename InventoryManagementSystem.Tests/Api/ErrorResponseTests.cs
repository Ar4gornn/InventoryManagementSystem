using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace InventoryManagementSystem.Tests.Api;

/// <summary>
/// How the API answers when something is wrong. The service layer throws
/// DomainException; turning that into a status code is middleware's job, and only
/// a real request proves it happens.
/// </summary>
public class ErrorResponseTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ErrorResponseTests(InventoryApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", InventoryApiFactory.ApiKey);
        return client;
    }

    [Fact]
    public async Task An_unknown_product_is_404_with_problem_json()
    {
        var response = await _factory.CreateClient().GetAsync("/api/products/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("problem+json", response.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task A_duplicate_sku_surfaces_as_409_not_500()
    {
        var client = AuthorizedClient();
        var sku = $"DUP-{Guid.NewGuid():N}"[..12];

        var category = await client.PostAsJsonAsync(
            "/api/categories", new { name = $"Cat {Guid.NewGuid():N}" });
        var categoryId = (await category.Content.ReadFromJsonAsync<CategoryResponse>())!.Id;

        var first = await client.PostAsJsonAsync(
            "/api/products", new { sku, name = "First", categoryId });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            "/api/products", new { sku, name = "Second", categoryId });

        // The unique index would make this a DbUpdateException and a 500 if the
        // service did not check first.
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Taking_more_stock_than_exists_is_400_and_changes_nothing()
    {
        var client = AuthorizedClient();

        var category = await client.PostAsJsonAsync(
            "/api/categories", new { name = $"Cat {Guid.NewGuid():N}" });
        var categoryId = (await category.Content.ReadFromJsonAsync<CategoryResponse>())!.Id;

        var created = await client.PostAsJsonAsync("/api/products", new
        {
            sku = $"NEG-{Guid.NewGuid():N}"[..12],
            name = "Stock guard",
            categoryId,
        });
        var productId = (await created.Content.ReadFromJsonAsync<ProductResponse>())!.Id;

        await client.PostAsJsonAsync($"/api/products/{productId}/movements",
            new { type = "In", quantity = 5, reason = "Opening" });

        var response = await client.PostAsJsonAsync($"/api/products/{productId}/movements",
            new { type = "Out", quantity = 6, reason = "Too many" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The invariant is only meaningful if the rejected movement left no trace.
        var stock = await client.GetFromJsonAsync<StockResponse>(
            $"/api/products/{productId}/stock");
        Assert.Equal(5, stock!.QuantityOnHand);
    }

    [Fact]
    public async Task Model_validation_failures_are_400()
    {
        var response = await AuthorizedClient()
            .PostAsJsonAsync("/api/categories", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_import_with_no_file_is_400_rather_than_an_unhandled_exception()
    {
        // The controller's own guard, which the service-level import tests bypass
        // entirely by handing the service a stream directly.
        using var content = new MultipartFormDataContent();

        var response = await AuthorizedClient().PostAsync("/api/products/import", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_import_with_a_wrong_header_is_400_not_500()
    {
        using var content = new MultipartFormDataContent();
        var file = new StringContent("name,sku\nx,y", Encoding.UTF8, "text/csv");
        content.Add(file, "file", "wrong.csv");

        var response = await AuthorizedClient().PostAsync("/api/products/import", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Movement_type_is_accepted_by_name_not_only_by_number()
    {
        var client = AuthorizedClient();

        var category = await client.PostAsJsonAsync(
            "/api/categories", new { name = $"Cat {Guid.NewGuid():N}" });
        var categoryId = (await category.Content.ReadFromJsonAsync<CategoryResponse>())!.Id;

        var created = await client.PostAsJsonAsync("/api/products", new
        {
            sku = $"ENU-{Guid.NewGuid():N}"[..12],
            name = "Enum by name",
            categoryId,
        });
        var productId = (await created.Content.ReadFromJsonAsync<ProductResponse>())!.Id;

        var response = await client.PostAsJsonAsync($"/api/products/{productId}/movements",
            new { type = "In", quantity = 3, reason = "By name" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Reading_movements_for_an_unknown_product_is_404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/products/999999/movements");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_stock_for_an_unknown_product_is_404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/products/999999/stock");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record CategoryResponse(int Id, string Name);

    private record ProductResponse(int Id, string Sku);

    private record StockResponse(int ProductId, int QuantityOnHand);
}
