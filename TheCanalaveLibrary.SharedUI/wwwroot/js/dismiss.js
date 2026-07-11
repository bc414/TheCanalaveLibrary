// Uniform Overlay dismissal (design constitution, Phase C 2026-07-10 — layer4-style.md
// "Interaction States"). Two mechanisms, one file:
//
// 1. Blazor-stateful flyouts (UserMenu, CreateMenu, NotificationBell, card carets) render a
//    transparent full-viewport catcher `[data-flyout-catcher]` behind their panel while open;
//    clicking it invokes the component's own Close handler, so Blazor state stays truthful.
//    Escape "clicks" the topmost catcher for the same effect.
// 2. Native <details data-dropdown> disclosures (chapter select, mobile tab pickers) close on
//    any outside click and on Escape by removing their open attribute.
//
// Exclusive-open falls out of the catcher: opening a second flyout first lands on the previous
// flyout's catcher, closing it (standard two-click behavior).
document.addEventListener('click', function (e) {
    document.querySelectorAll('details[data-dropdown][open]').forEach(function (d) {
        if (!d.contains(e.target)) d.removeAttribute('open');
    });
});

document.addEventListener('keydown', function (e) {
    if (e.key !== 'Escape') return;
    const catchers = document.querySelectorAll('[data-flyout-catcher]');
    if (catchers.length > 0) {
        catchers[catchers.length - 1].click();
        return;
    }
    const open = document.querySelectorAll('details[data-dropdown][open]');
    if (open.length > 0) open[open.length - 1].removeAttribute('open');
});
