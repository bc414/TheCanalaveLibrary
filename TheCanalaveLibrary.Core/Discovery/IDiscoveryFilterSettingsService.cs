namespace TheCanalaveLibrary.Core;

/// <summary>
/// Self-referential read+write service for a user editing their own §8.7 per-search-mode
/// default-exclusion overrides (WU-DiscoveryOverrideUI, closes tracker item B7). The sanctioned
/// CQRS-lite exception — applicable only because reader and writer are identical by definition,
/// same rationale as <see cref="IUserSettingsService"/>. See <c>layer2-services.md</c>
/// §"Self-Referential Editing Exception."
///
/// <para>
/// Deliberately separate from <see cref="IDiscoveryDefaultsReadService"/>: that service is
/// anonymous-callable and consumed by four discovery surfaces plus two server services, and stays
/// a pure read — adding a write there would widen an anonymous read seam for no reason. This
/// service reads the same two tables but only ever resolves the current authenticated user.
/// </para>
///
/// All methods resolve the target user from <see cref="IActiveUserContext"/> and throw
/// <see cref="InvalidOperationException"/> when the caller is not authenticated.
/// </summary>
public interface IDiscoveryFilterSettingsService
{
    /// <summary>
    /// Returns the full override matrix for the current user: one <see cref="DiscoveryFilterModeDto"/>
    /// per confirmed-consumer search mode, each carrying every mappable filter key's system
    /// default, the user's effective value, and whether an override row exists.
    /// </summary>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<IReadOnlyList<DiscoveryFilterModeDto>> GetMyMatrixAsync();

    /// <summary>
    /// Sets the current user's override for one (search mode × filter key) cell.
    ///
    /// <para><b>Sparse model:</b> when <paramref name="isEnabled"/> matches the system default for
    /// that cell, the override row is deleted (absence = "use default"). Otherwise the row is
    /// upserted. Mirrors <c>INotificationWriteService.SetSettingAsync</c>'s sparse contract
    /// exactly.</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task SetOverrideAsync(string searchModeKey, string filterKey, bool isEnabled);
}
