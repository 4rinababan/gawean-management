using TaskManagement.Domain.Wiki.Spreadsheet;

namespace TaskManagement.Domain.Tests;

public class FormulaEngineTests
{
    private static SpreadsheetSheet SheetWith(params (string Cell, string Value)[] cells)
    {
        var sheet = new SpreadsheetSheet();
        foreach (var (cell, value) in cells)
            sheet.Cells[cell] = value;
        return sheet;
    }

    [Fact]
    public void Plain_number_evaluates_to_itself()
    {
        var sheet = SheetWith(("A1", "42"));
        FormulaEngine.Evaluate("A1", sheet).ToString().Should().Be("42");
    }

    [Fact]
    public void Plain_text_evaluates_to_itself()
    {
        var sheet = SheetWith(("A1", "hello"));
        FormulaEngine.Evaluate("A1", sheet).ToString().Should().Be("hello");
    }

    [Fact]
    public void Empty_cell_evaluates_to_empty_string()
    {
        var sheet = new SpreadsheetSheet();
        FormulaEngine.Evaluate("A1", sheet).ToString().Should().Be("");
    }

    [Theory]
    [InlineData("=1+2", "3")]
    [InlineData("=10-3", "7")]
    [InlineData("=4*5", "20")]
    [InlineData("=10/4", "2.5")]
    [InlineData("=2+3*4", "14")]
    [InlineData("=(2+3)*4", "20")]
    [InlineData("=-5+2", "-3")]
    public void Arithmetic_follows_normal_precedence(string formula, string expected)
    {
        var sheet = SheetWith(("A1", formula));
        FormulaEngine.Evaluate("A1", sheet).ToString().Should().Be(expected);
    }

    [Fact]
    public void Division_by_zero_is_a_spreadsheet_error()
    {
        var sheet = SheetWith(("A1", "=1/0"));
        FormulaEngine.Evaluate("A1", sheet).ToString().Should().Be("#DIV/0!");
    }

    [Fact]
    public void Formula_can_reference_another_cell()
    {
        var sheet = SheetWith(("A1", "5"), ("B1", "=A1+1"));
        FormulaEngine.Evaluate("B1", sheet).ToString().Should().Be("6");
    }

    [Fact]
    public void Formula_references_chain_through_multiple_cells()
    {
        var sheet = SheetWith(("A1", "1"), ("B1", "=A1+1"), ("C1", "=B1+1"));
        FormulaEngine.Evaluate("C1", sheet).ToString().Should().Be("3");
    }

    [Fact]
    public void Reference_to_empty_cell_counts_as_zero()
    {
        var sheet = SheetWith(("A1", "=B1+1"));
        FormulaEngine.Evaluate("A1", sheet).ToString().Should().Be("1");
    }

    [Fact]
    public void Direct_self_reference_is_circular()
    {
        var sheet = SheetWith(("A1", "=A1+1"));
        FormulaEngine.Evaluate("A1", sheet).ToString().Should().Be("#CIRCULAR!");
    }

    [Fact]
    public void Indirect_circular_reference_is_detected()
    {
        var sheet = SheetWith(("A1", "=B1+1"), ("B1", "=A1+1"));
        FormulaEngine.Evaluate("A1", sheet).ToString().Should().Be("#CIRCULAR!");
    }

    [Fact]
    public void Sum_over_a_range_adds_every_numeric_cell()
    {
        var sheet = SheetWith(("A1", "1"), ("A2", "2"), ("A3", "3"), ("B1", "=SUM(A1:A3)"));
        FormulaEngine.Evaluate("B1", sheet).ToString().Should().Be("6");
    }

    [Fact]
    public void Sum_over_a_range_skips_empty_cells()
    {
        var sheet = SheetWith(("A1", "1"), ("A3", "3"), ("B1", "=SUM(A1:A3)"));
        FormulaEngine.Evaluate("B1", sheet).ToString().Should().Be("4");
    }

    [Fact]
    public void Average_over_a_range()
    {
        var sheet = SheetWith(("A1", "2"), ("A2", "4"), ("B1", "=AVERAGE(A1:A2)"));
        FormulaEngine.Evaluate("B1", sheet).ToString().Should().Be("3");
    }

    [Fact]
    public void Min_and_max_over_a_range()
    {
        var sheet = SheetWith(("A1", "5"), ("A2", "1"), ("A3", "9"), ("B1", "=MIN(A1:A3)"), ("B2", "=MAX(A1:A3)"));
        FormulaEngine.Evaluate("B1", sheet).ToString().Should().Be("1");
        FormulaEngine.Evaluate("B2", sheet).ToString().Should().Be("9");
    }

    [Fact]
    public void Count_over_a_range_counts_non_empty_numeric_cells()
    {
        var sheet = SheetWith(("A1", "5"), ("A3", "9"), ("B1", "=COUNT(A1:A3)"));
        FormulaEngine.Evaluate("B1", sheet).ToString().Should().Be("2");
    }

    [Fact]
    public void Sum_can_combine_a_range_with_a_literal()
    {
        var sheet = SheetWith(("A1", "1"), ("A2", "2"), ("B1", "=SUM(A1:A2,10)"));
        FormulaEngine.Evaluate("B1", sheet).ToString().Should().Be("13");
    }

    [Fact]
    public void Text_that_is_not_numeric_fails_arithmetic_with_value_error()
    {
        var sheet = SheetWith(("A1", "hello"), ("B1", "=A1+1"));
        FormulaEngine.Evaluate("B1", sheet).ToString().Should().Be("#VALUE!");
    }

    [Theory]
    [InlineData("A", 0)]
    [InlineData("B", 1)]
    [InlineData("Z", 25)]
    [InlineData("AA", 26)]
    [InlineData("AB", 27)]
    public void Column_letters_round_trip_through_index(string letters, int index)
    {
        CellReference.ColumnToIndex(letters).Should().Be(index);
        CellReference.IndexToColumn(index).Should().Be(letters);
    }
}
