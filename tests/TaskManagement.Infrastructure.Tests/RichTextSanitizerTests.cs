using TaskManagement.Infrastructure.Content;

namespace TaskManagement.Infrastructure.Tests;

/// <summary>
/// Issue descriptions are user-authored HTML rendered back to other members of the workspace, so this
/// is a security boundary, not a formatting nicety. These cases are the ones that must never regress.
/// </summary>
public class RichTextSanitizerTests
{
    private readonly RichTextSanitizer _sanitizer = new();

    private string San(string html) => _sanitizer.Sanitize(html) ?? "";

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert('xss')>")]
    [InlineData("<div onclick=\"alert('xss')\">click</div>")]
    [InlineData("<a href=\"javascript:alert('xss')\">go</a>")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>")]
    [InlineData("<object data=\"evil.swf\"></object>")]
    [InlineData("<svg><script>alert(1)</script></svg>")]
    [InlineData("<form action=\"/steal\"><input name=\"p\"></form>")]
    [InlineData("<style>body{display:none}</style>")]
    [InlineData("<a href=\"data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==\">x</a>")]
    public void Dangerous_markup_is_stripped(string payload)
    {
        var result = San(payload);

        result.Should().NotContain("<script", "no script tags survive");
        result.Should().NotContainAny("onerror", "onclick", "javascript:", "<iframe", "<object", "<form", "data:text/html");
    }

    [Theory]
    [InlineData("<p><strong>bold</strong></p>", "strong")]
    [InlineData("<p><em>italic</em></p>", "em")]
    [InlineData("<p><u>underline</u></p>", "u")]
    [InlineData("<h2>Heading</h2>", "h2")]
    [InlineData("<ul><li>one</li></ul>", "<li>")]
    [InlineData("<ol><li>one</li></ol>", "<ol>")]
    [InlineData("<blockquote>quoted</blockquote>", "blockquote")]
    [InlineData("<pre><code>x = 1</code></pre>", "code")]
    public void The_editors_formatting_survives(string html, string expectedFragment)
        => San(html).Should().Contain(expectedFragment);

    [Fact]
    public void A_sql_query_survives_intact()
    {
        var sql = "SELECT t.id, t.name\nFROM   orders t\nWHERE  t.total &gt; 100\nORDER  BY t.name;";

        var result = San($"<pre><code class=\"language-sql\">{sql}</code></pre>");

        result.Should().Contain("<pre>").And.Contain("SELECT").And.Contain("ORDER  BY");
        result.Should().Contain("language-sql", "the class drives syntax highlighting on the view side");
    }

    [Fact]
    public void A_mermaid_erd_survives_with_its_relationship_syntax()
    {
        // The ERD syntax leans on |, {, } and -- , which a careless sanitiser would mangle.
        var erd = "erDiagram\n    CUSTOMER ||--o{ ORDER : places\n    ORDER {\n        int id PK\n    }";

        var result = San($"<pre><code class=\"language-mermaid\">{erd}</code></pre>");

        result.Should().Contain("erDiagram");
        result.Should().Contain("||--o{");
        result.Should().Contain("int id PK");
    }

    [Fact]
    public void Newlines_inside_a_code_block_are_preserved()
    {
        var result = San("<pre><code>line one\nline two\nline three</code></pre>");

        result.Should().Contain("line one\nline two\nline three",
            "Mermaid and code blocks are whitespace-significant");
    }

    [Fact]
    public void A_script_hidden_inside_a_code_block_is_still_removed()
    {
        var result = San("<pre><code>ok</code></pre><script>alert(1)</script>");

        result.Should().Contain("ok");
        result.Should().NotContain("<script");
    }

    [Fact]
    public void Colour_and_alignment_styles_are_kept()
    {
        var result = San("<p style=\"color: rgb(230, 0, 0); text-align: center;\">tinted</p>");

        result.Should().Contain("color");
        result.Should().Contain("text-align");
    }

    [Fact]
    public void Positioning_styles_that_could_overlay_the_page_are_dropped()
    {
        var result = San("<p style=\"position: fixed; top: 0; left: 0; width: 100vw; height: 100vh\">overlay</p>");

        result.Should().NotContain("position");
        result.Should().NotContain("100vh");
    }

    [Fact]
    public void Images_served_by_the_app_are_preserved()
    {
        var result = San("<p><img src=\"/acme/attachments/0195b0e1-0000-7000-8000-000000000000\" alt=\"diagram\"></p>");

        result.Should().Contain("<img");
        result.Should().Contain("/acme/attachments/");
    }

    [Fact]
    public void External_links_are_forced_to_open_safely()
    {
        var result = San("<a href=\"https://example.com\">docs</a>");

        result.Should().Contain("target=\"_blank\"");
        result.Should().Contain("noopener");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_input_becomes_null_so_an_empty_editor_clears_the_description(string? input)
        => _sanitizer.Sanitize(input).Should().BeNull();
}
