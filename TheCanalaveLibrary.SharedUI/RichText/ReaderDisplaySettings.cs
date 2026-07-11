using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// Slim presentation property bag for the viewer's reader-display preferences — deliberately NOT a
/// *Dto: it never crosses the service boundary. <see cref="ReaderSettings"/> (Core) is the single
/// unified type loaded for the viewer; a layout-level ancestor converts it to this bag once
/// (<see cref="ToReaderDisplaySettings"/>) and provides it via CascadingValue, so leaves like
/// RichTextView see only the fields they render — not the behavior settings
/// (AutoLoadNextChapter, CollapseCommentThreads, etc.) they have no business consuming.
/// See canalave-conventions/layer3.5-structure.md "Ambient Viewer Settings via Cascading Slim Bags".
/// </summary>
public class ReaderDisplaySettings
{
    public string FontName { get; set; } = "Georgia";
    public int FontSize { get; set; } = 16;
    public float LineHeight { get; set; } = 1.5f;
    public int TextWidth { get; set; } = 800;
    public bool JustifyText { get; set; } = false;

    /// <summary>
    /// Reader's Content Surface background override (Phase E, 2026-07-10). Consumed by
    /// ContentSurface (the material), not RichTextView (the typography) — the two halves of
    /// reader ownership live in the components that own each concern.
    /// </summary>
    public ReadingBackgroundEnum ReadingBackground { get; set; } = ReadingBackgroundEnum.SiteDefault;
}

public static class ReaderSettingsExtensions
{
    /// <summary>
    /// Narrows the viewer's full <see cref="ReaderSettings"/> to the slim display bag consumed by
    /// RichTextView. Conversion lives here (SharedUI), not in Core — it's a presentation concern.
    /// </summary>
    public static ReaderDisplaySettings ToReaderDisplaySettings(this ReaderSettings settings) => new()
    {
        FontName = settings.FontName,
        FontSize = settings.FontSize,
        LineHeight = settings.LineHeight,
        TextWidth = settings.TextWidth,
        JustifyText = settings.JustifyText,
        ReadingBackground = settings.ReadingBackground
    };
}
