namespace TaskManagement.Domain.Common;

/// <summary>
/// Sparse lexicographic ranking for ordering board cards without renumbering siblings on every drag.
/// A rank is a base-36 string; <see cref="Between"/> returns a value that sorts strictly between two ranks.
/// </summary>
public static class LexoRank
{
    private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const int Base = 36;

    public static readonly string Min = "1";
    public static readonly string Max = "z";

    /// <summary>Evenly spaced initial ranks for a fresh list of <paramref name="count"/> items.</summary>
    public static IReadOnlyList<string> Initial(int count)
    {
        var result = new List<string>(count);
        var previous = Min;
        for (var i = 0; i < count; i++)
        {
            previous = Between(previous, Max);
            result.Add(previous);
        }

        return result;
    }

    /// <summary>Returns a rank strictly between <paramref name="before"/> and <paramref name="after"/>. Pass null/empty for an open end.</summary>
    public static string Between(string? before, string? after)
    {
        before = string.IsNullOrEmpty(before) ? "" : before;
        after = string.IsNullOrEmpty(after) ? "" : after;

        if (!string.IsNullOrEmpty(after) && string.CompareOrdinal(before, after) >= 0)
            throw new DomainException($"Cannot rank between '{before}' and '{after}': out of order.");

        var result = new System.Text.StringBuilder();
        var position = 0;
        while (true)
        {
            var lo = position < before.Length ? DigitValue(before[position]) : 0;
            var hi = position < after.Length ? DigitValue(after[position]) : Base;

            if (lo == hi)
            {
                result.Append(Digits[lo]);
                position++;
                continue;
            }

            var mid = (lo + hi) / 2;
            if (mid > lo)
            {
                result.Append(Digits[mid]);
                return result.ToString();
            }

            // Digits are adjacent: keep the lower digit and descend another level.
            result.Append(Digits[lo]);
            position++;
            after = ""; // Below this prefix the upper bound is unbounded.
        }
    }

    private static int DigitValue(char c)
    {
        var index = Digits.IndexOf(char.ToLowerInvariant(c));
        return index < 0 ? throw new DomainException($"'{c}' is not a valid rank digit.") : index;
    }
}
