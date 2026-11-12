export function initialize(root) {
    const sidebar = root.querySelector('.ナビゲーション');
    const separator = root.querySelector('[data-sidebar-resize]');
    const toggles = [...root.querySelectorAll('[data-sidebar-toggle]')];
    const outerToggle = toggles.find(button => !sidebar?.contains(button));
    if (!sidebar || !separator || !outerToggle) throw new Error('Sidebar layout controls are unavailable.');
    let width = 320, collapsed = false, drag = null, changed = false;
    const maximum = () => Math.max(200, Math.min(600, window.innerWidth - 160));
    const notify = () => root.dispatchEvent(new CustomEvent('seamless-sidebar-change', { detail: { width, collapsed } }));
    const apply = () => {
        root.style.setProperty('--sidebar-width', `${width}px`);
        root.dataset.sidebarWidth = String(width); root.dataset.sidebarCollapsed = String(collapsed);
        const moveFocus = collapsed && (sidebar.contains(document.activeElement) || document.activeElement === separator);
        sidebar.inert = collapsed; sidebar.hidden = collapsed; separator.hidden = collapsed;
        for (const button of toggles) button.setAttribute('aria-expanded', String(!collapsed));
        separator.setAttribute('aria-valuemin', '200'); separator.setAttribute('aria-valuemax', String(maximum())); separator.setAttribute('aria-valuenow', String(Math.min(width, maximum())));
        if (moveFocus) outerToggle.focus({ preventScroll: true });
    };
    const commit = () => { changed = true; apply(); notify(); };
    const click = event => {
        const button = event.target.closest('[data-sidebar-toggle]');
        if (!button || !root.contains(button)) return;
        collapsed = !collapsed; commit();
    };
    const finishDrag = cancel => {
        if (!drag) return;
        const pointerId = drag.pointerId;
        if (cancel) width = drag.width;
        drag = null; delete root.dataset.sidebarResizing;
        if (separator.hasPointerCapture(pointerId)) separator.releasePointerCapture(pointerId);
        commit();
    };
    const down = event => {
        if (event.button !== 0 || drag || collapsed) return;
        event.preventDefault(); separator.focus({ preventScroll: true });
        drag = { pointerId: event.pointerId, x: event.clientX, width, displayedWidth: Math.min(width, maximum()) };
        separator.setPointerCapture(event.pointerId); root.dataset.sidebarResizing = 'true';
    };
    const move = event => {
        if (!drag || drag.pointerId !== event.pointerId) return;
        event.preventDefault(); width = Math.round(Math.max(200, Math.min(maximum(), drag.displayedWidth + event.clientX - drag.x))); apply();
    };
    const up = event => { if (drag?.pointerId === event.pointerId) finishDrag(false); };
    const cancel = event => { if (drag?.pointerId === event.pointerId) finishDrag(true); };
    const key = event => {
        if (drag && event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); finishDrag(true); return; }
        if (event.target !== separator || event.ctrlKey || event.metaKey || event.altKey || event.isComposing || event.keyCode === 229) return;
        if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
        event.preventDefault(); const step = event.shiftKey ? 32 : 8;
        if (event.key === 'Home') width = 200;
        else if (event.key === 'End') width = maximum();
        else if (event.key === 'ArrowLeft') width = Math.max(200, Math.min(width, maximum()) - step);
        else width = Math.min(maximum(), width + step);
        commit();
    };
    const restore = event => {
        const value = event.detail;
        if (changed || !value || !Number.isFinite(value.width) || value.width < 200 || value.width > 600 || typeof value.collapsed !== 'boolean') return;
        width = value.width; collapsed = value.collapsed; apply();
    };
    root.addEventListener('click', click); root.addEventListener('seamless-sidebar-restore', restore);
    separator.addEventListener('pointerdown', down); separator.addEventListener('pointermove', move); separator.addEventListener('pointerup', up); separator.addEventListener('pointercancel', cancel); separator.addEventListener('lostpointercapture', cancel);
    document.addEventListener('keydown', key, true); window.addEventListener('resize', apply); apply();
    return { dispose() { if (drag) finishDrag(true); root.removeEventListener('click', click); root.removeEventListener('seamless-sidebar-restore', restore); separator.removeEventListener('pointerdown', down); separator.removeEventListener('pointermove', move); separator.removeEventListener('pointerup', up); separator.removeEventListener('pointercancel', cancel); separator.removeEventListener('lostpointercapture', cancel); document.removeEventListener('keydown', key, true); window.removeEventListener('resize', apply); } };
}
