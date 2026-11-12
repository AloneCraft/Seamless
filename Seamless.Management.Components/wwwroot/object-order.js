// Current-screen display order only. A group includes its DB, membership and render generation.
export function initialize(root, bridge) {
    let active = null, marker = null, suppressed = null;
    const mime = 'application/x-seamless-object-display-order';
    const enabled = row => row && root.contains(row) && row.dataset.objectOrderEnabled === 'true' && root.dataset.interactiveReady === 'true';
    const rowOf = target => target instanceof Element ? target.closest('[data-object-order-group]') : null;
    const clear = () => {
        marker?.removeAttribute('data-object-drop-position'); marker = null;
        active?.row.classList.remove('オブジェクト表示順ドラッグ中'); active = null;
    };
    const start = event => {
        const handle = event.target instanceof Element ? event.target.closest('[data-object-order-handle]') : null;
        if (!handle || !root.contains(handle)) return;
        const row = rowOf(handle);
        if (!enabled(row) || !event.dataTransfer) { event.preventDefault(); clear(); return; }
        clear();
        active = { row, group: row.dataset.objectOrderGroup, id: handle.dataset.objectOrderHandle };
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData(mime, active.id);
        row.classList.add('オブジェクト表示順ドラッグ中');
    };
    const destination = event => {
        const row = rowOf(event.target);
        if (!active || !enabled(active.row) || !enabled(row) || row === active.row || row.dataset.objectOrderGroup !== active.group || active.row.dataset.objectOrderGroup !== active.group) return null;
        return row;
    };
    const over = event => {
        const row = destination(event);
        marker?.removeAttribute('data-object-drop-position'); marker = null;
        if (!row) return;
        event.preventDefault(); event.dataTransfer.dropEffect = 'move';
        const bounds = row.getBoundingClientRect();
        row.dataset.objectDropPosition = event.clientY < bounds.top + bounds.height / 2 ? 'before' : 'after'; marker = row;
    };
    const drop = event => {
        const row = destination(event);
        if (!row) { clear(); return; }
        event.preventDefault(); event.stopPropagation();
        const bounds = row.getBoundingClientRect(), before = event.clientY < bounds.top + bounds.height / 2;
        const source = active, target = row.getAttribute('data-対象-id');
        suppressed = { id: source.id, until: performance.now() + 350 };
        clear();
        bridge.invokeMethodAsync('オブジェクト表示順ドロップAsync', source.group, source.id, target, before)
            .catch(() => bridge.invokeMethodAsync('オブジェクト表示順失敗Async').catch(() => {}));
    };
    const end = () => {
        if (active) suppressed = { id: active.id, until: performance.now() + 350 };
        clear();
    };
    const key = event => { if (active && event.key === 'Escape') clear(); };
    const click = event => {
        if (!suppressed) return;
        const handle = event.target instanceof Element ? event.target.closest('[data-object-order-handle]') : null;
        if (performance.now() < suppressed.until && handle?.dataset.objectOrderHandle === suppressed.id) { event.preventDefault(); event.stopImmediatePropagation(); }
        suppressed = null;
    };
    const pointer = () => { suppressed = null; };
    root.addEventListener('dragstart', start, true); root.addEventListener('dragover', over, true);
    root.addEventListener('drop', drop, true); root.addEventListener('dragend', end, true);
    root.addEventListener('keydown', key, true); root.addEventListener('click', click, true);
    root.addEventListener('pointerdown', pointer, true);
    return { dispose() {
        clear(); suppressed = null;
        root.removeEventListener('dragstart', start, true); root.removeEventListener('dragover', over, true);
        root.removeEventListener('drop', drop, true); root.removeEventListener('dragend', end, true);
        root.removeEventListener('keydown', key, true); root.removeEventListener('click', click, true);
        root.removeEventListener('pointerdown', pointer, true);
    } };
}
