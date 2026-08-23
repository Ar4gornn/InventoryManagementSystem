namespace InventoryManagementSystem.Application;

/// <summary>
/// A rule the caller broke. Carries the status code the API should answer with, so
/// controllers translate outcomes instead of guessing them from exception types.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    public static DomainException NotFound(string message) =>
        new(message, StatusCodes.Status404NotFound);

    public static DomainException Conflict(string message) =>
        new(message, StatusCodes.Status409Conflict);
}
