using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="VouchList"/> (WU21). VouchList is a pure display composite — it
/// injects no service. Tests cover: UserCard rendered per vouch; VouchText rendered via RichTextView
/// when present; empty message shown when no vouches; remove control present only when IsEditable;
/// OnRemoveVouch EventCallback fires with the correct user id.
///
/// Tier: <b>RazorComponents</b> (bUnit, no host or DB).
/// </summary>
public class VouchListTests : BunitContext
{
    // ── empty state ───────────────────────────────────────────────────────────────

    [Fact]
    public void VouchList_WhenEmpty_RendersEmptyMessage()
    {
        IRenderedComponent<VouchList> cut = Render<VouchList>(p => p
            .Add(c => c.Vouches, [])
            .Add(c => c.EmptyMessage, "No vouches yet."));

        cut.Markup.Should().Contain("No vouches yet.");
        cut.FindAll("li").Should().BeEmpty();
    }

    // ── UserCard renders per vouch ────────────────────────────────────────────────

    [Fact]
    public void VouchList_RendersOneListItemPerVouch()
    {
        IReadOnlyList<VouchDisplayDto> vouches =
        [
            MakeVouch(1, "Ash", null),
            MakeVouch(2, "Misty", null)
        ];

        IRenderedComponent<VouchList> cut = Render<VouchList>(p => p
            .Add(c => c.Vouches, vouches));

        cut.FindAll("li").Should().HaveCount(2);
    }

    [Fact]
    public void VouchList_RendersUsernamesForEachVouch()
    {
        IReadOnlyList<VouchDisplayDto> vouches =
        [
            MakeVouch(1, "Brock", null),
            MakeVouch(2, "Gary", null)
        ];

        IRenderedComponent<VouchList> cut = Render<VouchList>(p => p
            .Add(c => c.Vouches, vouches));

        cut.Markup.Should().Contain("Brock");
        cut.Markup.Should().Contain("Gary");
    }

    // ── VouchText renders via RichTextView ────────────────────────────────────────

    [Fact]
    public void VouchList_WhenVouchTextPresent_RendersContent()
    {
        IReadOnlyList<VouchDisplayDto> vouches =
        [
            MakeVouch(1, "Ash", "<p>Great trainer.</p>")
        ];

        IRenderedComponent<VouchList> cut = Render<VouchList>(p => p
            .Add(c => c.Vouches, vouches));

        cut.Markup.Should().Contain("Great trainer.",
            "VouchText is rich HTML rendered by RichTextView — the text content must appear");
    }

    // ── IsEditable controls remove buttons ───────────────────────────────────────

    [Fact]
    public void VouchList_WhenEditable_RendersRemoveButtonPerRow()
    {
        IReadOnlyList<VouchDisplayDto> vouches =
        [
            MakeVouch(1, "Officer Jenny", null),
            MakeVouch(2, "Nurse Joy", null)
        ];

        IRenderedComponent<VouchList> cut = Render<VouchList>(p => p
            .Add(c => c.Vouches, vouches)
            .Add(c => c.IsEditable, true));

        // Count occurrences of the remove-button aria-label prefix in the raw markup.
        // Split on the phrase produces (count + 1) parts — subtract 1 to get the count.
        int removeButtonCount = cut.Markup.Split("Remove vouch for").Length - 1;
        removeButtonCount.Should().Be(2,
            "one 'Remove' button per row must appear in the markup when IsEditable is true");
    }

    // ── OnRemoveVouch callback ────────────────────────────────────────────────────

    [Fact]
    public async Task VouchList_ClickingRemove_FiresOnRemoveVouchWithCorrectUserId()
    {
        int? received = null;
        IReadOnlyList<VouchDisplayDto> vouches = [MakeVouch(userId: 77, username: "Erika", text: null)];

        IRenderedComponent<VouchList> cut = Render<VouchList>(p => p
            .Add(c => c.Vouches, vouches)
            .Add(c => c.IsEditable, true)
            .Add(c => c.OnRemoveVouch, (int id) => { received = id; }));

        // DOM order for one vouch + IsEditable: [0] UserCard "More options", [1] Remove button.
        // Use direct index access to avoid AngleSharp compound-selector bugs.
        IElement removeButton = cut.FindAll("button")[1];
        await removeButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        received.Should().Be(77, "OnRemoveVouch must be invoked with the vouched user's id");
    }

    // ── helper ────────────────────────────────────────────────────────────────────

    private static VouchDisplayDto MakeVouch(int userId, string username, string? text) =>
        new(
            User: new UserCardDto(userId, username, null, "/img/default-avatar.svg", []),
            VouchText: text,
            DateVouched: new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc)
        );
}
