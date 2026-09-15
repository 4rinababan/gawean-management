using System.Globalization;

namespace TaskManagement.Domain.Wiki.Spreadsheet;

public enum CellValueKind { Empty, Number, Text, Error }

/// <summary>The evaluated result of one cell — a plain value, or a spreadsheet-style error code (#REF!, #DIV/0!, ...).</summary>
public readonly struct CellValue
{
    private CellValue(CellValueKind kind, double number, string text)
    {
        Kind = kind;
        Number = number;
        Text = text;
    }

    public CellValueKind Kind { get; }
    public double Number { get; }
    public string Text { get; }

    public static readonly CellValue Empty = new(CellValueKind.Empty, 0, "");
    public static CellValue Of(double number) => new(CellValueKind.Number, number, "");
    public static CellValue Of(string text) => new(CellValueKind.Text, 0, text);
    public static CellValue Error(string code) => new(CellValueKind.Error, 0, code);

    public bool IsError => Kind == CellValueKind.Error;

    /// <summary>Numeric coercion for arithmetic: empty cells count as 0, text that isn't a valid
    /// number can't be added/multiplied — matches how a plain formula engine, not a lenient one, should behave.</summary>
    public double AsNumberOrThrow() => Kind switch
    {
        CellValueKind.Number => Number,
        CellValueKind.Empty => 0,
        // Propagate the original code (e.g. #CIRCULAR!) rather than masking it with a generic #VALUE! —
        // this only matters once an error value flows into another operation, e.g. =A1+1 where A1 errors.
        CellValueKind.Error => throw new FormulaException(Text),
        CellValueKind.Text when double.TryParse(Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) => n,
        _ => throw new FormulaException("#VALUE!"),
    };

    /// <summary>How this value renders in the grid — the cell shows this, never the raw formula, once evaluated.</summary>
    public override string ToString() => Kind switch
    {
        CellValueKind.Number => Number.ToString("0.##########", CultureInfo.InvariantCulture),
        CellValueKind.Text => Text,
        CellValueKind.Error => Text,
        _ => "",
    };
}

/// <summary>Carries a spreadsheet error code (e.g. "#DIV/0!") up to the cell that triggered it.</summary>
public sealed class FormulaException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
