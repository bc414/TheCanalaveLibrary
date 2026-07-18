namespace TheCanalaveLibrary.Core;

/// <summary>
/// Base of the per-feature validation-exception family (MA-008 unification, 2026-07-18). One
/// property shape (Errors) and one base type lets ExceptionPresenter, EndpointHelpers, and the
/// client-side status translation handle every feature's validation rejection identically —
/// previously 13 drifted shapes matched by a name-suffix hack. Derived types keep their
/// feature-specific names because catch sites and client reconstruction are per-feature.
/// </summary>
public abstract class CanalaveValidationException : Exception
{
    protected CanalaveValidationException(string message, IReadOnlyList<string> errors) : base(message)
    {
        Errors = errors;
    }

    /// <summary>User-written, user-facing error lines for inline display.</summary>
    public IReadOnlyList<string> Errors { get; }
}
