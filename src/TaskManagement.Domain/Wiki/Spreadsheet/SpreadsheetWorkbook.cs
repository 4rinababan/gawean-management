using System.Text.Json.Serialization;

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

    /// <summary>Sparse per-cell visual formatting, independent of <see cref="Cells"/> — a cell can be
    /// formatted (e.g. part of a pasted colour band) without holding a value. Same key shape as Cells.</summary>
    public Dictionary<string, CellFormat> Formats { get; set; } = [];
}

/// <summary>Visual formatting for one cell. Never affects formula evaluation — <see cref="FormulaEngine"/>
/// only ever reads <see cref="SpreadsheetSheet.Cells"/>.</summary>
public sealed class CellFormat
{
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }

    /// <summary>CSS hex colour (e.g. "#92d050"), or null for no fill.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>CSS hex colour, or null for the default text colour.</summary>
    public string? TextColor { get; set; }

    /// <summary>"left" | "center" | "right", or null for the default (left).</summary>
    public string? Align { get; set; }

    /// <summary>Derived, not stored — every cell in Formats already carries at least one non-default
    /// value, so this only exists to tell a caller when it should remove the entry instead of adding it.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        !Bold && !Italic && !Underline && BackgroundColor is null && TextColor is null && Align is null;
}
