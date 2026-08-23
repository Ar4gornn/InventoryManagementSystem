using System.Text;
using InventoryManagementSystem.Application;
using InventoryManagementSystem.Contracts;
using InventoryManagementSystem.Domain.Entities;
using InventoryManagementSystem.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementSystem.Services;

/// <summary>
/// Bulk product import from CSV.
/// </summary>
/// <remarks>
/// Rows are independent: one bad line does not abort the file, and the response says
/// which lines failed and why. The alternative - all or nothing - means a two hundred
/// row file is rejected because of one typo, which is worse for the person holding the
/// spreadsheet.
/// </remarks>
public class ProductImportService : IProductImportService
{
    private const string ExpectedHeader = "sku,name,description,category,quantity";
    private const int MaxRows = 5000;

    private readonly InventoryContext _context;
    private readonly ILogger<ProductImportService> _logger;

    public ProductImportService(InventoryContext context, ILogger<ProductImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(Stream csv, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csv, Encoding.UTF8);

        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null)
        {
            throw new DomainException("The file is empty.");
        }

        if (!HeaderMatches(headerLine))
        {
            throw new DomainException($"Expected the header '{ExpectedHeader}'.");
        }

        // Loaded once rather than queried per row: an import is the one place where
        // per-row round trips actually hurt.
        var categoriesByName = await _context.Categories
            .ToDictionaryAsync(c => c.Name.ToLowerInvariant(), c => c, ct);

        var existingSkus = await _context.Products
            .Select(p => p.Sku)
            .ToListAsync(ct);

        var seenSkus = new HashSet<string>(existingSkus, StringComparer.OrdinalIgnoreCase);

        var rows = new List<ImportRowResult>();
        var toAdd = new List<(Product Product, int Quantity)>();
        var lineNumber = 1;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (rows.Count >= MaxRows)
            {
                throw new DomainException($"The file has more than {MaxRows} rows.");
            }

            var fields = SplitCsvLine(line);
            var sku = fields.ElementAtOrDefault(0)?.Trim() ?? string.Empty;

            var error = ValidateRow(fields, sku, seenSkus, categoriesByName, out var product, out var quantity);

            if (error is not null)
            {
                rows.Add(new ImportRowResult(lineNumber, sku, false, error));
                continue;
            }

            seenSkus.Add(sku);
            toAdd.Add((product!, quantity));
            rows.Add(new ImportRowResult(lineNumber, sku, true, null));
        }

        if (toAdd.Count > 0)
        {
            foreach (var (product, _) in toAdd)
            {
                _context.Products.Add(product);
            }

            await _context.SaveChangesAsync(ct);

            // Opening stock is recorded as a movement, never as a column, so an
            // imported product's quantity has the same provenance as any other.
            foreach (var (product, quantity) in toAdd.Where(x => x.Quantity != 0))
            {
                _context.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    Type = MovementType.In,
                    QuantityDelta = quantity,
                    Reason = "CSV import - opening stock",
                    OccurredAt = DateTime.UtcNow,
                });
            }

            await _context.SaveChangesAsync(ct);
        }

        var imported = rows.Count(r => r.Imported);
        _logger.LogInformation("CSV import: {Imported} of {Total} rows", imported, rows.Count);

        return new ImportResult(rows.Count, imported, rows.Count - imported, rows);
    }

    private static bool HeaderMatches(string headerLine) =>
        string.Join(',', SplitCsvLine(headerLine).Select(f => f.Trim().ToLowerInvariant()))
            .Equals(ExpectedHeader, StringComparison.Ordinal);

    private static string? ValidateRow(
        IReadOnlyList<string> fields,
        string sku,
        HashSet<string> seenSkus,
        IReadOnlyDictionary<string, Category> categoriesByName,
        out Product? product,
        out int quantity)
    {
        product = null;
        quantity = 0;

        if (fields.Count < 5)
        {
            return $"Expected 5 columns, found {fields.Count}.";
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            return "Sku is required.";
        }

        if (seenSkus.Contains(sku))
        {
            return $"Sku '{sku}' already exists.";
        }

        var name = fields[1].Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required.";
        }

        var categoryName = fields[3].Trim();
        if (!categoriesByName.TryGetValue(categoryName.ToLowerInvariant(), out var category))
        {
            return $"Unknown category '{categoryName}'.";
        }

        var quantityField = fields[4].Trim();
        if (!string.IsNullOrEmpty(quantityField) && !int.TryParse(quantityField, out quantity))
        {
            return $"Quantity '{quantityField}' is not a whole number.";
        }

        if (quantity < 0)
        {
            return "Quantity cannot be negative.";
        }

        product = new Product
        {
            Sku = sku,
            Name = name,
            Description = string.IsNullOrWhiteSpace(fields[2]) ? null : fields[2].Trim(),
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow,
        };

        return null;
    }

    /// <summary>
    /// Splits one CSV line, honouring double quotes so a description containing a
    /// comma does not shift every field after it.
    /// </summary>
    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // "" inside a quoted field is an escaped quote.
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
