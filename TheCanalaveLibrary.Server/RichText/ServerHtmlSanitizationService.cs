using AngleSharp.Dom;
using Ganss.Xss;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Configured <see cref="HtmlSanitizer"/> allow-list — the mirror of <c>EditorView</c>'s toolbar
/// (<c>SharedUI/RichText/EditorView.razor</c>; see canalave-conventions/layer2-services.md "The
/// allow-list is the inverse of the toolbar"). All configuration happens once here, at construction;
/// <see cref="HtmlSanitizer"/>.Sanitize is thread-safe thereafter provided properties aren't mutated
/// concurrently, so this is registered <c>AddSingleton</c> in Program.cs.
/// </summary>
public class ServerHtmlSanitizationService : IHtmlSanitizationService
{
    private readonly HtmlSanitizer _sanitizer;

    public ServerHtmlSanitizationService()
    {
        _sanitizer = new HtmlSanitizer();

        // Exactly what EditorView's toolbar (desktop set) can produce — extend both together.
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedTags.UnionWith(
        [
            "p", "br", "strong", "em", "u", "s", "h2", "h3", "blockquote", "ul", "ol", "li", "a"
        ]);

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto"]);

        _sanitizer.AllowedCssProperties.Clear();
        _sanitizer.AllowedClasses.Clear();

        // Quill's link button only ever sets href — rel/target are normalized here rather than
        // trusted from client-supplied markup.
        _sanitizer.PostProcessNode += (_, args) =>
        {
            if (args.Node is IElement { TagName: "A" } anchor && anchor.HasAttribute("href"))
            {
                anchor.SetAttribute("target", "_blank");
                anchor.SetAttribute("rel", "noopener noreferrer");
            }
        };
    }

    public string Sanitize(string? html) =>
        string.IsNullOrEmpty(html) ? string.Empty : _sanitizer.Sanitize(html);
}
