using System.Text.Json.Serialization;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Used for validating the tags attached to a story. The priority is something specific to the relationship
/// between a tag and a story.
/// <para>
/// STJ polymorphism config (Global Flip): <c>CreateStoryDTO</c>/<c>StoryUpdateDTO</c> carry
/// <c>List&lt;IStoryTag&gt;</c> across the Layer-5 HTTP boundary, and interface-typed members can't
/// round-trip default-settings System.Text.Json without it — the WASM client's story create/edit
/// calls would throw <c>NotSupportedException</c> on deserialization. Only <see cref="StoryTagDTO"/>
/// is registered: it's the sole type that ever populates those DTO lists (see
/// <c>ServerStoryReadService.GetStoryForEditAsync</c>). The <c>StoryTag</c> ENTITY also implements
/// this interface but must never cross the wire — leaving it unregistered makes an accidental
/// entity-in-DTO serialization throw instead of silently leaking entity data.
/// </para>
/// </summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(StoryTagDTO), typeDiscriminator: "storyTag")]
public interface IStoryTag
{
    public int TagId { get; set; }

    public TagPriority Priority { get; set; }

    public TagTypeEnum TagTypeEnum { get; }
}
