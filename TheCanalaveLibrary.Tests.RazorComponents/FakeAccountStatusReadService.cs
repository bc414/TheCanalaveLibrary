using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Controllable in-memory <see cref="IAccountStatusReadService"/> for bUnit tests
/// (WU-AccountEnforcement). <see cref="Status"/>/<see cref="SuspendedUntilUtc"/> are mutated by
/// the test between navigations to simulate a moderator action landing mid-session; set
/// <see cref="ThrowOnNextCall"/> to exercise <c>AccountStatusBanner</c>'s degrade-to-last-known
/// catch path.
/// </summary>
public sealed class FakeAccountStatusReadService : IAccountStatusReadService
{
    public AccountStatusEnum Status { get; set; } = AccountStatusEnum.Active;
    public DateTime? SuspendedUntilUtc { get; set; }
    public bool ThrowOnNextCall { get; set; }
    public int CallCount { get; private set; }

    public Task<AccountStatusDto> GetMyAccountStatusAsync()
    {
        CallCount++;
        if (ThrowOnNextCall)
        {
            ThrowOnNextCall = false;
            throw new InvalidOperationException("simulated read failure");
        }

        return Task.FromResult(new AccountStatusDto(Status, SuspendedUntilUtc));
    }
}
