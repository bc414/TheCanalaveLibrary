/**
 * Sprite onerror fallback chain: animated webp → static png → unknown.png
 *
 * The <img> element must have:
 *   data-static  — URL of the static .png (fallback when animated .webp fails)
 *   data-unknown — URL of the unknown.png placeholder (final fallback)
 *
 * Usage:
 *   <img src="{animated.webp}"
 *        data-static="{static.png}"
 *        data-unknown="{unknown.png}"
 *        onerror="spriteFallback(this)" />
 *
 * Works in both SSR and interactive Blazor — onerror is evaluated by the browser,
 * not by Blazor, so no circuit or JS interop dependency.
 */
function spriteFallback(img) {
    var staticSrc = img.getAttribute('data-static');
    var unknownSrc = img.getAttribute('data-unknown');

    if (staticSrc && img.src !== staticSrc) {
        // First fallback: try the static .png
        img.src = staticSrc;
    } else if (unknownSrc && img.src !== unknownSrc) {
        // Final fallback: unknown.png
        img.src = unknownSrc;
        img.onerror = null; // prevent infinite loop
    } else {
        // Give up — no further fallback available
        img.onerror = null;
    }
}
