using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Shared bUnit assertion (WU-A11y (Structure), 2026-07-31) — every rendered
/// <c>&lt;input&gt;</c>/<c>&lt;select&gt;</c>/<c>&lt;textarea&gt;</c> has an accessible name.
///
/// This complements, rather than duplicates, <c>scripts/check-a11y.ps1</c>'s gates A/C/E: the
/// PowerShell gates are static-markup regex checks (fast, run in CI, but see only the raw
/// <c>.razor</c> source). This helper renders the component and inspects the actual DOM, so it
/// additionally catches names supplied by a CHILD component (e.g. a picker's own internal
/// <c>aria-label</c> on a nested typeahead input) and breaks the moment a future field is added
/// without a label — the class of regression a source-level regex can't see because it has no
/// concept of "every input," only "every &lt;label&gt;."
///
/// A name is accessible here if the field has (a) a non-empty <c>aria-label</c>, (b) an <c>id</c>
/// matched by some <c>&lt;label for="..."&gt;</c> in the render, or (c) an ancestor
/// <c>&lt;label&gt;</c> element (the native wrapping-label pattern, e.g. checkboxes/radios and the
/// two Import components' hidden-InputFile-inside-a-styled-label idiom).
/// </summary>
internal static class AccessibleNameAssertions
{
    public static void AllFieldsHaveAccessibleNames<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        IReadOnlyList<IElement> fields = cut.FindAll("input, select, textarea");
        IReadOnlyList<IElement> labelsWithFor = cut.FindAll("label[for]");

        HashSet<string> labelledIds = labelsWithFor
            .Select(l => l.GetAttribute("for"))
            .Where(f => !string.IsNullOrEmpty(f))
            .Select(f => f!)
            .ToHashSet();

        List<string> unnamed = [];

        foreach (IElement field in fields)
        {
            // Hidden fields carry no user-facing label by definition (e.g. anti-forgery tokens,
            // ReturnUrl/RememberMe carriers on the Identity pages) — out of scope for this check.
            if (field.GetAttribute("type") == "hidden") continue;

            // Quill's own toolbar (EditorView's <ToolbarContent>, e.g. <select class="ql-header">)
            // is third-party markup Quill.js itself progressively enhances with its own
            // accessibility affordances client-side — bUnit never runs real JS, so it renders
            // "raw" and unlabelled here. Out of scope for a static-markup pass; accepted exception
            // (mirrors check-design-tokens.ps1's own named exemption list).
            if (IsInsideQuillToolbar(field)) continue;

            string? ariaLabel = field.GetAttribute("aria-label");
            if (!string.IsNullOrWhiteSpace(ariaLabel)) continue;

            string? id = field.GetAttribute("id");
            if (id is not null && labelledIds.Contains(id)) continue;

            if (HasLabelAncestor(field)) continue;

            unnamed.Add(Describe(field));
        }

        unnamed.Should().BeEmpty(
            "every input/select/textarea must have an accessible name (aria-label, a for=/id= " +
            "association, or a wrapping <label>) - WU-A11y (Structure)'s floor, gate E's source-level " +
            "counterpart in scripts/check-a11y.ps1");
    }

    private static bool IsInsideQuillToolbar(IElement field)
    {
        for (IElement? ancestor = field; ancestor is not null; ancestor = ancestor.ParentElement)
        {
            string cls = ancestor.GetAttribute("class") ?? string.Empty;
            if (cls.Contains("ql-toolbar") || cls.Contains("ql-formats")) return true;
        }
        return false;
    }

    private static bool HasLabelAncestor(IElement field)
    {
        for (IElement? ancestor = field.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
        {
            if (ancestor.TagName.Equals("LABEL", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string Describe(IElement field)
    {
        string tag = field.TagName.ToLowerInvariant();
        string? id = field.GetAttribute("id");
        string? placeholder = field.GetAttribute("placeholder");
        string? name = field.GetAttribute("name");
        return $"<{tag}> id=\"{id}\" name=\"{name}\" placeholder=\"{placeholder}\"";
    }
}
