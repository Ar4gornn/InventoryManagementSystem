namespace InventoryManagementSystem.Contracts;

/// <summary>
/// What happened to one line of the uploaded file. Line numbers are 1-based and
/// include the header, so they match what the user sees in a text editor.
/// </summary>
public record ImportRowResult(int Line, string Sku, bool Imported, string? Error);

/// <summary>
/// The outcome of an import. Rows are reported individually: a file with three bad
/// lines out of two hundred imports the other one hundred and ninety seven and says
/// exactly which three failed and why.
/// </summary>
public record ImportResult(
    int TotalRows,
    int ImportedCount,
    int FailedCount,
    IReadOnlyList<ImportRowResult> Rows);
