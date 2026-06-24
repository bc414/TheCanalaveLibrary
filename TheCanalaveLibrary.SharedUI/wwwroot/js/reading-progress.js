// Reading-progress scroll tracker. Registers a throttled scroll listener that computes what
// fraction of the chapter body element has been scrolled past and invokes a .NET callback.
// One instance per page; dispose() cleans up before the next page registers.
window.readingProgress = (function () {
    let _scrollHandler = null;
    let _resizeHandler = null;
    let _pendingTimer = null;

    return {
        register(dotnetRef, elementId) {
            const report = () => {
                const el = document.getElementById(elementId);
                if (!el) return;
                const chapterTop = el.getBoundingClientRect().top + window.scrollY;
                const chapterHeight = el.offsetHeight;
                if (chapterHeight <= 0) return;
                const scrolledPast = window.scrollY + window.innerHeight - chapterTop;
                const fraction = Math.min(1, Math.max(0, scrolledPast / chapterHeight));
                dotnetRef.invokeMethodAsync('OnScrollProgress', fraction);
            };

            const throttled = () => {
                if (_pendingTimer) return;
                _pendingTimer = setTimeout(() => { _pendingTimer = null; report(); }, 300);
            };

            _scrollHandler = throttled;
            _resizeHandler = throttled;
            window.addEventListener('scroll', _scrollHandler, { passive: true });
            window.addEventListener('resize', _resizeHandler, { passive: true });
            // Initial check after the element is fully painted.
            setTimeout(report, 400);
        },

        dispose() {
            if (_scrollHandler) {
                window.removeEventListener('scroll', _scrollHandler);
                _scrollHandler = null;
            }
            if (_resizeHandler) {
                window.removeEventListener('resize', _resizeHandler);
                _resizeHandler = null;
            }
            if (_pendingTimer) {
                clearTimeout(_pendingTimer);
                _pendingTimer = null;
            }
        }
    };
})();
