using System.Globalization;

namespace TaskManagement.Domain.Wiki.Spreadsheet;

/// <summary>
/// Evaluates one cell of a <see cref="SpreadsheetSheet"/>, recursively resolving any cell references the
/// formula touches. Deliberately small: arithmetic (+ - * /, unary -, parentheses), cell references
/// (A1), ranges (A1:B5) as function arguments, and five aggregate functions (SUM, AVERAGE, COUNT, MIN,
/// MAX) — this is "basic formulas" as asked for, not a general-purpose spreadsheet engine.
/// </summary>
public static class FormulaEngine
{
    private static readonly string[] KnownFunctions = ["SUM", "AVERAGE", "COUNT", "MIN", "MAX"];

    public static CellValue Evaluate(string cellRef, SpreadsheetSheet sheet)
        => EvaluateInternal(cellRef.ToUpperInvariant(), sheet, []);

    private static CellValue EvaluateInternal(string cellRef, SpreadsheetSheet sheet, HashSet<string> visiting)
    {
        if (!visiting.Add(cellRef))
            return CellValue.Error("#CIRCULAR!");

        try
        {
            if (!sheet.Cells.TryGetValue(cellRef, out var raw) || string.IsNullOrWhiteSpace(raw))
                return CellValue.Empty;

            raw = raw.Trim();
            if (!raw.StartsWith('='))
            {
                return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)
                    ? CellValue.Of(n)
                    : CellValue.Of(raw);
            }

            var tokens = Tokenize(raw[1..]);
            var parser = new Parser(tokens, sheet, visiting);
            var result = parser.ParseExpression();
            return parser.AtEnd ? result : CellValue.Error("#ERROR!");
        }
        catch (FormulaException ex)
        {
            return CellValue.Error(ex.Code);
        }
        finally
        {
            visiting.Remove(cellRef);
        }
    }

    // --- Tokenizer -----------------------------------------------------------------------------

    private enum TokenType { Number, Ref, Range, Func, Plus, Minus, Star, Slash, LParen, RParen, Comma }

    private readonly record struct Token(TokenType Type, string Text);

    private static List<Token> Tokenize(string formula)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < formula.Length)
        {
            var c = formula[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            switch (c)
            {
                case '+': tokens.Add(new(TokenType.Plus, "+")); i++; continue;
                case '-': tokens.Add(new(TokenType.Minus, "-")); i++; continue;
                case '*': tokens.Add(new(TokenType.Star, "*")); i++; continue;
                case '/': tokens.Add(new(TokenType.Slash, "/")); i++; continue;
                case '(': tokens.Add(new(TokenType.LParen, "(")); i++; continue;
                case ')': tokens.Add(new(TokenType.RParen, ")")); i++; continue;
                case ',': tokens.Add(new(TokenType.Comma, ",")); i++; continue;
            }

            if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                while (i < formula.Length && (char.IsDigit(formula[i]) || formula[i] == '.')) i++;
                tokens.Add(new(TokenType.Number, formula[start..i]));
                continue;
            }

            if (char.IsLetter(c))
            {
                var start = i;
                while (i < formula.Length && char.IsLetterOrDigit(formula[i])) i++;
                var word = formula[start..i];

                if (i < formula.Length && formula[i] == ':')
                {
                    i++;
                    var start2 = i;
                    while (i < formula.Length && char.IsLetterOrDigit(formula[i])) i++;
                    tokens.Add(new(TokenType.Range, $"{word}:{formula[start2..i]}"));
                    continue;
                }

                if (i < formula.Length && formula[i] == '(' && KnownFunctions.Contains(word.ToUpperInvariant()))
                {
                    tokens.Add(new(TokenType.Func, word.ToUpperInvariant()));
                    continue;
                }

                tokens.Add(new(TokenType.Ref, word.ToUpperInvariant()));
                continue;
            }

            throw new FormulaException("#ERROR!");
        }
        return tokens;
    }

    // --- Recursive-descent parser + evaluator, combined (no separate AST — evaluates as it parses) ---

    private sealed class Parser(List<Token> tokens, SpreadsheetSheet sheet, HashSet<string> visiting)
    {
        private int _pos;

        public bool AtEnd => _pos >= tokens.Count;

        private Token? Current => _pos < tokens.Count ? tokens[_pos] : null;

        private Token Take()
        {
            if (_pos >= tokens.Count) throw new FormulaException("#ERROR!");
            return tokens[_pos++];
        }

        public CellValue ParseExpression()
        {
            var left = ParseTerm();
            while (Current is { Type: TokenType.Plus or TokenType.Minus })
            {
                var op = Take().Type;
                var right = ParseTerm();
                var result = op == TokenType.Plus
                    ? left.AsNumberOrThrow() + right.AsNumberOrThrow()
                    : left.AsNumberOrThrow() - right.AsNumberOrThrow();
                left = CellValue.Of(result);
            }
            return left;
        }

        private CellValue ParseTerm()
        {
            var left = ParseUnary();
            while (Current is { Type: TokenType.Star or TokenType.Slash })
            {
                var op = Take().Type;
                var right = ParseUnary();
                if (op == TokenType.Star)
                {
                    left = CellValue.Of(left.AsNumberOrThrow() * right.AsNumberOrThrow());
                }
                else
                {
                    var divisor = right.AsNumberOrThrow();
                    if (divisor == 0) throw new FormulaException("#DIV/0!");
                    left = CellValue.Of(left.AsNumberOrThrow() / divisor);
                }
            }
            return left;
        }

        private CellValue ParseUnary()
        {
            if (Current is { Type: TokenType.Minus })
            {
                Take();
                return CellValue.Of(-ParseUnary().AsNumberOrThrow());
            }
            return ParsePrimary();
        }

        private CellValue ParsePrimary()
        {
            var token = Take();
            switch (token.Type)
            {
                case TokenType.Number:
                    return CellValue.Of(double.Parse(token.Text, CultureInfo.InvariantCulture));

                case TokenType.Ref:
                    if (!CellReference.TryParse(token.Text, out _, out _))
                        throw new FormulaException("#REF!");
                    return EvaluateInternal(token.Text, sheet, visiting);

                case TokenType.LParen:
                    var inner = ParseExpression();
                    if (Current is not { Type: TokenType.RParen }) throw new FormulaException("#ERROR!");
                    Take();
                    return inner;

                case TokenType.Func:
                    return ParseFunctionCall(token.Text);

                default:
                    throw new FormulaException("#ERROR!");
            }
        }

        private CellValue ParseFunctionCall(string name)
        {
            if (Current is not { Type: TokenType.LParen }) throw new FormulaException("#ERROR!");
            Take();

            var values = new List<double>();
            if (Current is not { Type: TokenType.RParen })
            {
                values.AddRange(ParseArgument());
                while (Current is { Type: TokenType.Comma })
                {
                    Take();
                    values.AddRange(ParseArgument());
                }
            }

            if (Current is not { Type: TokenType.RParen }) throw new FormulaException("#ERROR!");
            Take();

            return name switch
            {
                "SUM" => CellValue.Of(values.Sum()),
                "AVERAGE" => values.Count == 0 ? CellValue.Error("#DIV/0!") : CellValue.Of(values.Average()),
                "COUNT" => CellValue.Of(values.Count),
                "MIN" => values.Count == 0 ? CellValue.Of(0) : CellValue.Of(values.Min()),
                "MAX" => values.Count == 0 ? CellValue.Of(0) : CellValue.Of(values.Max()),
                _ => throw new FormulaException("#NAME?"),
            };
        }

        /// <summary>A function argument is either a range (expanded to every non-empty numeric cell in
        /// it) or a single expression.</summary>
        private List<double> ParseArgument()
        {
            if (Current is { Type: TokenType.Range } range)
            {
                Take();
                return ExpandRange(range.Text);
            }

            var value = ParseExpression();
            return value.Kind == CellValueKind.Empty ? [] : [value.AsNumberOrThrow()];
        }

        private List<double> ExpandRange(string range)
        {
            var parts = range.Split(':');
            if (parts.Length != 2
                || !CellReference.TryParse(parts[0], out var c1, out var r1)
                || !CellReference.TryParse(parts[1], out var c2, out var r2))
            {
                throw new FormulaException("#REF!");
            }

            var (minC, maxC) = (Math.Min(c1, c2), Math.Max(c1, c2));
            var (minR, maxR) = (Math.Min(r1, r2), Math.Max(r1, r2));

            var values = new List<double>();
            for (var row = minR; row <= maxR; row++)
            {
                for (var col = minC; col <= maxC; col++)
                {
                    var cellValue = EvaluateInternal(CellReference.ToReference(col, row), sheet, visiting);
                    if (cellValue.Kind == CellValueKind.Number)
                        values.Add(cellValue.Number);
                    else if (cellValue.IsError)
                        throw new FormulaException(cellValue.Text);
                }
            }
            return values;
        }
    }
}
