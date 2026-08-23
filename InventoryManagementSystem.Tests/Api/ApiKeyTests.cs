using System.Net;
using System.Net.Http.Json;

namespace InventoryManagementSystem.Tests.Api;

/// <summary>
/// The authorization rule, exercised over real HTTP: reads are open, writes need
/// a valid key. None of this is reachable from a service-level test.
/// </summary>
public class ApiKeyTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ApiKeyTests(InventoryApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client() => _factory.CreateClient();

    private HttpClient AuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", InventoryApiFactory.ApiKey);
        return client;
    }

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/categories")]
    [InlineData("/api/products?page=1&pageSize=5")]
    public async Task Reads_need_no_key(string url)
    {
        var response = await Client().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_write_without_a_key_is_401()
    {
        var response = await Client().PostAsJsonAsync("/api/categories", new { name = "Rejected" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_write_with_the_wrong_key_is_401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "not-the-key");

        var response = await client.PostAsJsonAsync("/api/categories", new { name = "Rejected" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_key_that_is_a_prefix_of_the_real_one_is_still_401()
    {
        // Guards against a comparison that stops at the shorter length.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", InventoryApiFactory.ApiKey[..5]);

        var response = await client.PostAsJsonAsync("/api/categories", new { name = "Rejected" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_write_with_the_right_key_is_allowed_through()
    {
        var response = await AuthorizedClient()
            .PostAsJsonAsync("/api/categories", new { name = $"Allowed {Guid.NewGuid():N}" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task Every_mutating_verb_is_guarded_not_just_post(string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), "/api/categories/1")
        {
            Content = JsonContent.Create(new { name = "Rejected" }),
        };

        var response = await Client().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_key_check_runs_before_the_resource_is_looked_up()
    {
        // A 404 here would tell an unauthenticated caller which ids exist.
        var response = await Client().DeleteAsync("/api/products/999999");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
