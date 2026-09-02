using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using TaskManagement.Web.Components.Ui;

namespace TaskManagement.Web.Tests;

/// <summary>
/// Razor treats an unprefixed attribute on a <c>string</c> component parameter as a literal, so
/// <c>Value="_edit.Description"</c> silently passes the text "_edit.Description" instead of the field.
/// It compiles, renders, and looks like the feature is broken — it cost us the assignee picker and the
/// description editor. This scans the real .razor sources for that shape.
///
/// The set of string parameters comes from reflection, so int/enum parameters (where the unprefixed
/// form *is* an expression, e.g. StatTile's Value) are never flagged and the check needs no upkeep.
/// </summary>
public class RazorParameterBindingTests
{
    /// <summary>A value that looks like a C# field or member path rather than intentional literal text.</summary>
    private static readonly Regex LooksLikeCode = new(
        @"^(_[A-Za-z0-9_]+(\.[A-Za-z0-9_]+)*|[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parameters whose value really is a literal that happens to look like a path. PasskeySubmit comes
    /// from the Identity scaffolding, where Name/EmailName are HTML form-field names ("Input.Passkey").
    /// </summary>
    private static readonly HashSet<string> IntentionalLiterals =
    [
        "PasskeySubmit.Name",
        "PasskeySubmit.EmailName",
    ];

    [Fact]
    public void No_string_component_parameter_is_passed_a_bare_csharp_expression()
    {
        var stringParameters = typeof(Icon).Assembly.GetTypes()
            .Where(t => typeof(ComponentBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToDictionary(
                t => t.Name,
                t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null
                                && p.PropertyType == typeof(string))
                    .Select(p => p.Name)
                    .ToHashSet());

        var offences = new List<string>();

        foreach (var file in Directory.EnumerateFiles(ComponentsDirectory(), "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);

            foreach (var (component, parameters) in stringParameters)
            {
                if (parameters.Count == 0) continue;

                // Match the whole opening tag, allowing it to span lines and contain quoted values.
                foreach (Match tag in Regex.Matches(source, $@"<{component}\b((?:[^>""]|""[^""]*"")*?)/?>", RegexOptions.Singleline))
                {
                    foreach (var parameter in parameters)
                    {
                        if (IntentionalLiterals.Contains($"{component}.{parameter}")) continue;

                        // Require whitespace before the name so "@bind-Value=" is not read as "Value=" —
                        // the bind form is always an expression and is never affected by this bug.
                        var attribute = Regex.Match(tag.Groups[1].Value, $@"(?<=\s){parameter}=""([^""]*)""");
                        if (!attribute.Success) continue;

                        var value = attribute.Groups[1].Value;
                        if (LooksLikeCode.IsMatch(value))
                        {
                            var line = source[..tag.Index].Count(c => c == '\n') + 1;
                            offences.Add($"{Path.GetFileName(file)}:{line} <{component} {parameter}=\"{value}\"> — did you mean \"@{value}\"?");
                        }
                    }
                }
            }
        }

        offences.Should().BeEmpty(
            "a string parameter given a bare C# expression is passed as literal text:\n" + string.Join("\n", offences));
    }

    private static string ComponentsDirectory([CallerFilePath] string thisFile = "")
    {
        var dir = Path.GetDirectoryName(thisFile)!;          // tests/TaskManagement.Web.Tests
        var root = Path.GetFullPath(Path.Combine(dir, "..", ".."));
        var components = Path.Combine(root, "src", "TaskManagement.Web", "Components");

        Directory.Exists(components).Should().BeTrue($"expected Razor sources at {components}");
        return components;
    }
}
