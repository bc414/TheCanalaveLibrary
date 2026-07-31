namespace TheCanalaveLibrary.Core;

/// <summary>
/// The single source of tag-hierarchy roll-up semantics. Any consumer that needs "this tag plus
/// its children" — story filtering today, a discovery mart or an Explore axis tomorrow — asks
/// here rather than reimplementing the rule (hidden-deferrals-tracker B12 complaint 2, the drift
/// that let the two halves of the tag model diverge in the first place).
///
/// <para><b>Contract:</b> the returned map is a cached process-local snapshot. It is refreshed on
/// any <c>ITagWriteService</c> write and, independently, after a short absolute TTL — so a
/// hierarchy edit made outside the write service (seeder, direct SQL, or another web node) takes
/// effect within one TTL window rather than never. Reads are therefore <b>eventually consistent
/// with a bounded staleness window</b>, not read-your-own-write across processes. Tag edits are
/// rare moderator actions and one cycle of staleness is harmless — see layer2-services.md
/// §"Reference-Data Caching".</para>
/// </summary>
public interface ITagHierarchyReadService
{
    Task<TagExpansionMap> GetExpansionMapAsync(CancellationToken cancellationToken = default);
}
