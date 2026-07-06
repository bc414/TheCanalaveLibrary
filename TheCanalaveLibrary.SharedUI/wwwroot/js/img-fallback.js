/**
 * Delegated image-error fallbacks — the CSP-compatible replacement for inline onerror
 * attributes (security.md "Response Headers & CSP": inline on* attributes are inline script
 * under CSP; the standing rule is data-* markers + this one capture-phase listener).
 *
 * Markup contract (on <img>):
 *   data-fallback-src="{url}" — swap src to the fallback once (avatars → default-avatar.svg)
 *   data-hide-on-error        — hide the element (decorative images, e.g. badge art)
 *   data-sprite-fallback      — delegate to spriteFallback(img)'s webp → png → unknown chain
 *                               (sprite-fallback.js must load first; it manages its own state)
 *
 * 'error' events don't bubble, but a capture-phase listener on document still sees them —
 * works for SSR-rendered and circuit-rendered images alike, no per-element wiring. The
 * attach-time sweep covers images that already failed before this script loaded (static SSR).
 */
(function () {
    function applyFallback(img) {
        if (img.hasAttribute('data-fallback-src')) {
            var fallback = img.getAttribute('data-fallback-src');
            // Guard against a failing fallback image looping forever.
            if (fallback && !img.src.endsWith(fallback)) {
                img.src = fallback;
            }
        } else if (img.hasAttribute('data-hide-on-error')) {
            img.style.display = 'none';
        } else if (img.hasAttribute('data-sprite-fallback') && typeof spriteFallback === 'function') {
            spriteFallback(img);
        }
    }

    document.addEventListener('error', function (e) {
        if (e.target instanceof HTMLImageElement) {
            applyFallback(e.target);
        }
    }, true);

    // Sweep images that errored before this script attached (script loads at end of body;
    // statically-rendered images above it can fail first).
    var candidates = document.querySelectorAll(
        'img[data-fallback-src], img[data-hide-on-error], img[data-sprite-fallback]');
    for (var i = 0; i < candidates.length; i++) {
        var img = candidates[i];
        if (img.complete && img.naturalWidth === 0) {
            applyFallback(img);
        }
    }
})();
