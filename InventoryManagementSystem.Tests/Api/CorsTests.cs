using System.Net;

namespace InventoryManagementSystem.Tests.Api;

/// <summary>
/// The browser contract for the web UI, which runs on a different origin.
/// </summary>
public class CorsTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public CorsTests(InventoryApiFactory factory)
    {
        _factory = factory;
    }

    private HttpResponseMessage Preflight(string origin, string method = "POST")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/categories");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-api-key");

        return _factory.CreateClient().SendAsync(request).GetAwaiter().GetResult();
    }

    [Fact]
    public void A_preflight_from_the_dev_server_origin_is_allowed()
    {
        var response = Preflight("http://localhost:5173");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void A_preflight_carries_no_allow_origin_for_an_unlisted_origin()
    {
        // Not an error status - the browser is what enforces this, by refusing to
        // proceed when the header is absent.
        var response = Preflight("http://evil.example.com");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void Preflight_is_not_blocked_by_the_api_key_check()
    {
        // OPTIONS carries no X-Api-Key header - the browser does not send one. If
        // the key check ran first, every cross-origin write would fail opaquely
        // instead of returning a clean 401.
        var response = Preflight("http://localhost:5173", "DELETE");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
