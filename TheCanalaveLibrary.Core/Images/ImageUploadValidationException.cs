namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by the server's upload-hardening step (<c>ImageUploadProcessor</c> and
/// <c>ImageUploadRules</c>, security.md §"Upload Content Pipeline") when it refuses an upload for a
/// reason the uploader can act on: a type outside the JPEG/PNG/WebP allow-list, bytes that are not a
/// recognized or readable image, the byte cap, or the source-dimension cap. One type serves both
/// upload slots (cover art, profile picture) because the pipeline is shared (<c>Images/</c> is a
/// cross-cutting cluster). It inherits <see cref="CanalaveValidationException"/>, so
/// <see cref="ExceptionPresenter"/> shows its message on the circuit and <c>EndpointHelpers</c>
/// answers 400 with it as <c>ProblemDetails.Detail</c>, which the WASM client rethrows as its
/// feature's validation exception. It replaced a BCL <see cref="ArgumentException"/>, whose message
/// was developer text: the circuit showed the generic error and logged at Error, and WASM showed
/// "(Parameter 'buffer')" (WU-DependencyCVEs browser pass, 2026-10-12). A decoder's exception rides
/// along as <see cref="Exception.InnerException"/> for the storage span; it never reaches the user.
/// </summary>
public sealed class ImageUploadValidationException(string message, Exception? innerException = null)
    : CanalaveValidationException(message, [message], innerException);
