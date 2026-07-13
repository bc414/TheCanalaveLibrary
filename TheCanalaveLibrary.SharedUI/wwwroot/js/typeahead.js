/**
 * CanalaveTypeahead's one JS touchpoint (Global Flip wave — the in-house replacement for the
 * archived Blazored.Typeahead): a delegated, capture-phase keydown listener that preventDefaults
 * Enter inside a typeahead input so picking a suggestion never implicit-submits the enclosing
 * form. preventDefault does NOT stop propagation, so Blazor's own @onkeydown handler still
 * receives the key and performs the highlighted-item selection.
 *
 * Same delegated data-* marker pattern as img-fallback.js (security.md: no inline on*
 * attributes under CSP; one document-level listener, no per-element wiring, no interop,
 * no disposal — works identically under static SSR, the circuit, and WASM).
 */
(function () {
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' &&
            e.target instanceof HTMLInputElement &&
            e.target.hasAttribute('data-typeahead-input')) {
            e.preventDefault();
        }
    }, true);
})();
