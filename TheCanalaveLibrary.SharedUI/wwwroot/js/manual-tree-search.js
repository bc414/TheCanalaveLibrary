// Manual Tree Search client gestures + persistence (Feature 33 / WU40 — layer3.5-structure.md
// §"The shared tree canvas"). Per-frame gestures (drag-to-pan, zoom, floating-panel drag) run
// entirely here and NEVER touch the SignalR circuit; Blazor owns structure (nodes, lines,
// layout), this module owns only CSS transforms/positions and localStorage.
//
// Pan state lives per-canvas in a WeakMap. Blazor re-renders replace the canvas's CHILDREN but
// keep the element itself, so the JS-owned style.transform survives diffing — Blazor must never
// set `style` on the pan target or panel elements (it would race this module).
window.canalaveManualTree = (function () {
    const panState = new WeakMap();   // canvasEl -> {x, y, scale}
    const attached = new WeakSet();   // guards double-attach across re-renders

    function apply(canvasEl) {
        const s = panState.get(canvasEl);
        if (s) canvasEl.style.transform = `translate(${s.x}px, ${s.y}px) scale(${s.scale})`;
    }

    return {
        // Drag-to-pan: pointer-drag anywhere on wrapEl except a node chip translates canvasEl.
        attachPan: function (wrapEl, canvasEl, startX, startY) {
            if (!wrapEl || !canvasEl || attached.has(wrapEl)) return;
            attached.add(wrapEl);
            panState.set(canvasEl, { x: startX || 0, y: startY || 0, scale: 1 });
            apply(canvasEl);

            let drag = null;
            wrapEl.addEventListener("pointerdown", function (e) {
                if (e.target.closest("[data-tree-node]")) return; // node clicks are Blazor's
                const s = panState.get(canvasEl);
                drag = { sx: e.clientX, sy: e.clientY, ox: s.x, oy: s.y };
                wrapEl.setPointerCapture(e.pointerId);
                wrapEl.classList.add("cursor-grabbing");
            });
            wrapEl.addEventListener("pointermove", function (e) {
                if (!drag) return;
                const s = panState.get(canvasEl);
                s.x = drag.ox + (e.clientX - drag.sx);
                s.y = drag.oy + (e.clientY - drag.sy);
                apply(canvasEl);
            });
            const end = function (e) {
                if (drag && wrapEl.hasPointerCapture(e.pointerId)) wrapEl.releasePointerCapture(e.pointerId);
                drag = null;
                wrapEl.classList.remove("cursor-grabbing");
            };
            wrapEl.addEventListener("pointerup", end);
            wrapEl.addEventListener("pointercancel", end);
        },

        zoom: function (canvasEl, delta) {
            const s = panState.get(canvasEl);
            if (!s) return;
            s.scale = Math.min(1.6, Math.max(0.5, s.scale + delta));
            apply(canvasEl);
        },

        resetPan: function (canvasEl, x, y) {
            const s = panState.get(canvasEl);
            if (!s) return;
            s.x = x || 0; s.y = y || 0; s.scale = 1;
            apply(canvasEl);
        },

        // Floating panel (Deep Dive): drag by its handle; resize is native CSS `resize`.
        // The panel is absolutely positioned within its offset parent (the canvas area).
        attachPanelDrag: function (panelEl, handleEl) {
            if (!panelEl || !handleEl || attached.has(handleEl)) return;
            attached.add(handleEl);

            let drag = null;
            handleEl.addEventListener("pointerdown", function (e) {
                if (e.target.closest("button, a, input, select")) return;
                drag = { sx: e.clientX, sy: e.clientY, ox: panelEl.offsetLeft, oy: panelEl.offsetTop };
                handleEl.setPointerCapture(e.pointerId);
                e.preventDefault(); // don't start text selection while dragging
            });
            handleEl.addEventListener("pointermove", function (e) {
                if (!drag) return;
                // Once the user drags, left/top own the position — clear the initial right-anchor.
                panelEl.style.right = "auto";
                panelEl.style.left = (drag.ox + e.clientX - drag.sx) + "px";
                panelEl.style.top = Math.max(0, drag.oy + e.clientY - drag.sy) + "px";
            });
            const end = function (e) {
                if (drag && handleEl.hasPointerCapture(e.pointerId)) handleEl.releasePointerCapture(e.pointerId);
                drag = null;
            };
            handleEl.addEventListener("pointerup", end);
            handleEl.addEventListener("pointercancel", end);
        },

        // localStorage tree documents (IDs + edges only — display data always rehydrates
        // server-side on load; settled 2026-07-12). Same guard discipline as canalaveDraft:
        // localStorage can throw (private browsing, quota); a failed save returns false.
        saveTree: function (key, json) {
            try { localStorage.setItem(key, json); return true; } catch { return false; }
        },
        loadTree: function (key) {
            try { return localStorage.getItem(key); } catch { return null; }
        },
        clearTree: function (key) {
            try { localStorage.removeItem(key); } catch { /* stale tree lingers — harmless */ }
        }
    };
})();
