namespace TaskManagement.Domain.Wiki.Spreadsheet;

/// <summary>
/// The full content of a <see cref="WikiPageType.Spreadsheet"/> page, serialized as JSON into
/// <see cref="WikiPage.Content"/>. Plain data classes (not domain entities) — a workbook is edited and
/// replaced as a whole, not mutated cell-by-cell through EF change tracking.
/// </summary>
public sealed class SpreadsheetWorkbook
{
    public List<SpreadsheetSheet> Sheets { get; set; } = [];

    public static SpreadsheetWorkbook NewDefault() => new()
    {
        Sheets = [new SpreadsheetSheet { Name = "Sheet1" }],
    };
}

public sealed class SpreadsheetSheet
{
    public string Name { get; set; } = "Sheet1";

    public int RowCount { get; set; } = 50;

    public int ColumnCount { get; set; } = 12;

    /// <summary>Sparse: only cells the user actually typed into are present. Key is an "A1"-style
    /// reference (case-insensitive, stored upper-case). A value starting with '=' is a formula.</summary>
    public Dictionary<string, string> Cells { get; set; } = [];
}
