using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Display shape of one "Also posted on" link (Feature 53 reframe, WU38d) — projected into
/// <see cref="StoryDetailsDTO.ExternalLinks"/> for the story page's low-key links row.
/// <see cref="IsVerified"/> drives the author-verified checkmark (and nothing else renders for
/// unverified links — the visible absence is the community's anti-theft signal).
/// </summary>
public record StoryExternalLinkDto(string PlatformName, string Url, bool IsVerified);

/// <summary>
/// Edit shape of one link row on the story form. Platform is auto-detected from the pasted URL's
/// host via <see cref="ExternalPlatformDto.DomainPattern"/> (overridable dropdown). Mutable class —
/// bound by the form.
/// </summary>
public class StoryExternalLinkEditDto
{
    public short ExternalPlatformId { get; set; }

    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;
}

/// <summary>One seeded platform row for the form dropdown + URL auto-detection.</summary>
public record ExternalPlatformDto(short ExternalPlatformId, string Name, string? DomainPattern);
