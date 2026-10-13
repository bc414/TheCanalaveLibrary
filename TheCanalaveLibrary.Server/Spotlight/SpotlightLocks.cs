namespace TheCanalaveLibrary.Server;

/// <summary>
/// The Community Spotlight cluster's one Postgres advisory lock (Feature 55). Every transaction that
/// creates or releases a placement's hold on a slot takes it first, so their check-then-act reads run
/// serialized: <see cref="ServerSpotlightWriteService.RedeemSlotAsync"/> (block capacity and the
/// per-story cooldown are count-then-insert) and
/// <see cref="SpotlightPlacementRelease.ReleaseUnstartedForStoryAsync"/> (a booking must not land on a
/// story between its slot release and its delete). One shared literal, so the two can never drift onto
/// different lock keys. Transaction-scoped: released at commit or rollback.
/// </summary>
public static class SpotlightLocks
{
    public const string BookingLockSql = "SELECT pg_advisory_xact_lock(hashtext('canalave_spotlight_booking'))";
}
