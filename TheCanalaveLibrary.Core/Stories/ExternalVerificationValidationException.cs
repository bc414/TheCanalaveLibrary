namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>IExternalVerificationWriteService</c> (Feature 53) when a request breaks a business
/// rule the caller can act on — a malformed profile URL, a missing handle, a platform that does not
/// support verification, or a link whose account tier is not verified yet. Mirrors
/// <see cref="ModerationValidationException"/>.
/// <para>Being a <see cref="CanalaveValidationException"/> is the point: the endpoint maps it to 400 and
/// the message reaches the user verbatim. The <see cref="InvalidOperationException"/> these rules used
/// to throw mapped to 401 — a "session expired" for a typo (WU-ModerationIntegrity, 2026-09-30; the D9
/// sub-edge, fixed at the throw sites).</para>
/// </summary>
public class ExternalVerificationValidationException(List<string> errors)
    : CanalaveValidationException(string.Join("; ", errors), errors);
