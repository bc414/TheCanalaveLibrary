// Device-local editor draft safety (cross-cutting.md §"Error Handling Strategy" — editor draft
// safety). localStorage survives circuit teardown, network drop, reload, and browser crash, and
// behaves identically under InteractiveServer and WASM. All calls are guarded: localStorage can
// throw (private browsing, quota) — a failed save returns false so the caller can log it.
window.canalaveDraft = {
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
            // Nothing to do — worst case a stale draft lingers on the device.
        }
    }
};
