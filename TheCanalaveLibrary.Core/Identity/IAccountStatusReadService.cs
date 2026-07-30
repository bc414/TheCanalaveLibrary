namespace TheCanalaveLibrary.Core;

/// <summary>
/// Self-referential live read of the caller's own <see cref="User.AccountStatus"/>
/// (WU-AccountEnforcement). Deliberately separate from <see cref="IActiveUserContext"/>: that
/// interface is scoped to query-shaping/authorization fields resolved once per circuit from
/// claims, whereas this service exists precisely because <c>AccountStatus</c> must NOT be treated
/// that way — it's a display-only value that needs a fresh DB read on every call, not a claim
/// refresh. See <c>identity-and-authorization.md</c> §"Account Status Is Display-Only, Read Live".
/// <c>AccountStatusBanner</c> is the sole consumer, calling this on every in-app navigation
/// (the <c>MessagesNavLink</c> unread-badge pattern).
/// </summary>
public interface IAccountStatusReadService
{
    Task<AccountStatusDto> GetMyAccountStatusAsync();
}
