// Story view-ping trigger (Feature 45). Fires the .NET callback ONCE per registration on the
// first of: 5-second dwell timer, or first scroll — never on raw page load (filters bots and
// bounces). One instance per page; dispose() cleans up before the next story registers.
window.viewPing = (function () {
    let _scrollHandler = null;
    let _dwellTimer = null;

    function cleanup() {
        if (_scrollHandler) {
            window.removeEventListener('scroll', _scrollHandler);
            _scrollHandler = null;
        }
        if (_dwellTimer) {
            clearTimeout(_dwellTimer);
            _dwellTimer = null;
        }
    }

    return {
        register(dotnetRef) {
            cleanup(); // defensive: a lingering registration must never double-fire

            const fire = () => {
                cleanup();
                dotnetRef.invokeMethodAsync('OnViewPing');
            };

            _scrollHandler = fire;
            window.addEventListener('scroll', _scrollHandler, { passive: true, once: true });
            _dwellTimer = setTimeout(fire, 5000);
        },

        dispose() {
            cleanup();
        }
    };
})();
