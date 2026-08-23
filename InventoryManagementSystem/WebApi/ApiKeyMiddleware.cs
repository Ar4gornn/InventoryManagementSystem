using System.Security.Cryptography;
using System.Text;

namespace InventoryManagementSystem.WebApi;

/// <summary>
/// Requires an API key on anything that changes data. Reads stay open so Swagger is
/// useful without a key, and so a browser can look around.
/// </summary>
/// <remarks>
/// This is authorization, not authentication - there is one shared key and no notion
/// of who is calling. It is enough to stop an unauthenticated write, which is what a
/// public API most needs, and it is honest about being no more than that.
/// </remarks>
public class ApiKeyMiddleware
{
    public const string HeaderName = "X-Api-Key";

    private static readonly string[] MutatingMethods = ["POST", "PUT", "PATCH", "DELETE"];

    private readonly RequestDelegate _next;
    private readonly string _expectedKey;

    public ApiKeyMiddleware(RequestDelegate next, string expectedKey)
    {
        _next = next;
        _expectedKey = expectedKey;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!MutatingMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(provided) || !KeysMatch(provided, _expectedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(
                $$"""
                {"title":"Unauthorized","status":401,"detail":"A valid {{HeaderName}} header is required for this request."}
                """);
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Fixed-time comparison. A plain string equality check leaks the position of the
    /// first wrong character through timing, which is enough to recover a key.
    /// </summary>
    private static bool KeysMatch(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
}
