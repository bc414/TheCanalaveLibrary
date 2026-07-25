using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Display shape of one "Also posted on" link — projected into
/// <see cref="StoryDetailsDTO.ExternalLinks"/> for the story page's low-key links row (WU39,
/// settled 2026-07-24, audit/Moderation.md F53: no checkmark — <see cref="IsReviewed"/> gates a
/// muted "reviewed · author's account" sub-line instead, since a checkmark invites the
/// complacency the whole feature exists to avoid). <see cref="AuthorAccountHandle"/>/
/// <see cref="AuthorAccountProfileUrl"/> are null unless <see cref="IsReviewed"/> — never-requested,
/// pending, and rejected links are deliberately indistinguishable to the reader (a plain link);
/// reporting is driven by a reader's own outside knowledge, never by this internal state.
/// </summary>
public record StoryExternalLinkDto(
    string PlatformName,
    string Url,
    bool IsReviewed,
    string? AuthorAccountHandle,
    string? AuthorAccountProfileUrl);

/// <summary>
/// Edit shape of one link row on the story form. Platform is auto-detected from the pasted URL's
/// host via <see cref="ExternalPlatformDto.DomainPattern"/> (overridable dropdown). Mutable class —
/// bound by the form. The three read-back members (WU39) let the editor show per-link verification
/// status and gate the "Request verification" action — <c>StoryExternalLinkId</c> is 0 for an
/// unsaved row.
/// </summary>
public class StoryExternalLinkEditDto
{
    public int StoryExternalLinkId { get; set; }

    public short ExternalPlatformId { get; set; }

    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    public VerificationStatusEnum VerificationStatus { get; set; }

    public bool VerificationRequested { get; set; }

    [MaxLength(512)]
    public string? RejectionReason { get; set; }
}

/// <summary>One seeded platform row for the form dropdown + URL auto-detection.</summary>
public record ExternalPlatformDto(short ExternalPlatformId, string Name, string? DomainPattern);
