using Ganss.Xss;
using AppSanitizer = TaskManagement.Application.Abstractions.IHtmlSanitizer;

namespace TaskManagement.Infrastructure.Content;

/// <summary>
/// Allow-list sanitiser for issue descriptions. Only the formatting the editor can produce survives:
/// no script, no event handlers, no external/inline resources beyond images we serve ourselves.
/// </summary>
public sealed class RichTextSanitizer : AppSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public RichTextSanitizer()
    {
        _sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "br", "span", "div",
                "strong", "b", "em", "i", "u", "s", "sub", "sup",
                "h1", "h2", "h3", "h4",
                "ul", "ol", "li",
                "blockquote", "pre", "code", "hr",
                "a", "img",
                "table", "thead", "tbody", "tr", "th", "td",
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "href", "title", "target", "rel",
                "src", "alt", "width", "height",
                "class", "style",
                "colspan", "rowspan",
            },
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "color", "background-color", "text-align", "font-size", "font-family", "font-weight",
                "font-style", "text-decoration",
            },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" },
            AllowedAtRules = new HashSet<AngleSharp.Css.Dom.CssRuleType>(),
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "src" },
        });

        // Links out of the app open in a new tab and must not hand the opener over.
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Html.Dom.IHtmlAnchorElement anchor)
            {
                anchor.SetAttribute("target", "_blank");
                anchor.SetAttribute("rel", "noopener noreferrer nofollow");
            }
        };
    }

    public string? Sanitize(string? html)
        => string.IsNullOrWhiteSpace(html) ? null : _sanitizer.Sanitize(html);
}
