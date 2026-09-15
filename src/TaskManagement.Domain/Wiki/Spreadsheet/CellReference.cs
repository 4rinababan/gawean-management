using System.Text.RegularExpressions;

namespace TaskManagement.Domain.Wiki.Spreadsheet;

/// <summary>Converts between "A1"-style references and zero-based (column, row) coordinates.</summary>
public static partial class CellReference
{
    [GeneratedRegex(@"^([A-Za-z]+)(\d+)$")]
    private static partial Regex RefPattern();

    public static bool TryParse(string reference, out int column, out int row)
    {
        var match = RefPattern().Match(reference);
        if (!match.Success)
        {
            column = row = -1;
            return false;
        }

        column = ColumnToIndex(match.Groups[1].Value);
        row = int.Parse(match.Groups[2].Value) - 1;
        return row >= 0;
    }

    public static string ToReference(int column, int row) => $"{IndexToColumn(column)}{row + 1}";

    /// <summary>"A" → 0, "B" → 1, ..., "Z" → 25, "AA" → 26 — base-26 with no zero digit.</summary>
    public static int ColumnToIndex(string letters)
    {
        var index = 0;
        foreach (var c in letters.ToUpperInvariant())
            index = index * 26 + (c - 'A' + 1);
        return index - 1;
    }

    public static string IndexToColumn(int index)
    {
        index++;
        var letters = "";
        while (index > 0)
        {
            var remainder = (index - 1) % 26;
            letters = (char)('A' + remainder) + letters;
            index = (index - 1) / 26;
        }
        return letters;
    }
}
