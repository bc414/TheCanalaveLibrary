/**
 * Sprite error fallback chain: animated webp → static png → unknown.png → give up.
 *
 * The <img> element must have:
 *   data-static  — URL of the static .png (fallback when the animated .webp fails)
 *   data-unknown — URL of the theme's unknown.png placeholder (final fallback)
 *   data-sprite-fallback — the marker img-fallback.js's delegated 'error' listener dispatches on
 *
 * Usage (no inline onerror: security.md "no new raw inline on* attributes"):
 *   <img src="{animated.webp}"
 *        data-static="{static.png}"
 *        data-unknown="{unknown.png}"
 *        data-sprite-fallback />
 *
 * Works in both SSR and interactive Blazor — the error event is handled by the browser, not by
 * Blazor, so there is no circuit or JS interop dependency.
 *
 * The step is chosen by comparing RESOLVED urls. img.src is always absolute, while the data-*
 * values are whatever ISpriteReadService produced (root-relative with the default Sprites:BaseUrl).
 * The raw comparison this replaced never matched a relative value, so a missing static png re-set
 * the same src on every error: an endless 404 loop that never reached unknown.png (found by the
 * WU-ParityAndRemaining browser pass, 2026-10-13). Clearing img.onerror stopped nothing either,
 * because the delegated listener, not an onerror attribute, drives the chain.
 */
function spriteFallback(img) {
    var current = img.src;
    var staticSrc = resolveSpriteUrl(img.getAttribute('data-static'));
    var unknownSrc = resolveSpriteUrl(img.getAttribute('data-unknown'));

    if (staticSrc && current !== staticSrc && current !== unknownSrc) {
        // First fallback: the static .png (skipped when it is the url that just failed).
        img.src = staticSrc;
    } else if (unknownSrc && current !== unknownSrc) {
        // Final fallback: the theme's unknown.png.
        img.src = unknownSrc;
    }
    // Otherwise unknown.png itself failed: leave the broken image; nothing is left to try.
}

/** The absolute form of a sprite url as img.src reports it, or null for a missing attribute. */
function resolveSpriteUrl(url) {
    return url ? new URL(url, document.baseURI).href : null;
}
