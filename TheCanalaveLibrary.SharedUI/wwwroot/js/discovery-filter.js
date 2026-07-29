// Device-local /discover filter restore (decision row 13 — roadmap.md §Resolved). The last
// applied filter survives navigating to a story and back, a reload, and circuit teardown, and
// behaves identically under InteractiveServer and WASM.
//
// Stores IDs ONLY — never display strings, never listing payloads. Blazor rehydrates chips and
// ship labels through the existing batch reads on load, and prunes anything the viewer can no
// longer see. Same contract as manual-tree-search.js's tree persistence.
//
// This is NOT the sharing mechanism: nothing here is synced, shared, or server-visible. Named,
// shareable tag combinations are SavedTagSelection.
//
// All calls are guarded: localStorage can throw (private browsing, quota) — a failed save returns
// false so the caller can decide whether to care.
window.canalaveDiscoveryFilter = {
    save: function (key, value) {
        try {
            localStorage.setItem(key, value);
            return true;
        } catch {
            return false;
        }
    },
    load: function (key) {
        try {
            return localStorage.getItem(key);
        } catch {
            return null;
        }
    },
    clear: function (key) {
        try {
            localStorage.removeItem(key);
        } catch {
            // Nothing to do — worst case a stale filter lingers on the device until the next save.
        }
    }
};
