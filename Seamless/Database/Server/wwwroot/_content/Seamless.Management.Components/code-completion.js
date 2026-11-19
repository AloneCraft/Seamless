let nextOptionId = 0;
function caretBounds(input) {
    const style = getComputedStyle(input);
    const mirror = document.createElement('div');
    Object.assign(mirror.style, {
        position: 'fixed', top: '0', left: '0', visibility: 'hidden', pointerEvents: 'none',
        whiteSpace: 'pre', fontFamily: style.fontFamily, fontSize: style.fontSize, fontWeight: style.fontWeight,
        fontStyle: style.fontStyle, lineHeight: style.lineHeight, fontVariantLigatures: style.fontVariantLigatures,
        tabSize: style.tabSize, letterSpacing: style.letterSpacing, wordSpacing: style.wordSpacing
    });
    const marker = document.createElement('span');
    marker.textContent = '\u200b';
    mirror.append(input.value.slice(0, input.selectionEnd), marker);
    document.body.append(mirror);
    const point = marker.getBoundingClientRect();
    mirror.remove();
    const bounds = input.getBoundingClientRect();
    return {
        x: bounds.left + input.clientLeft + parseFloat(style.paddingLeft) + point.left - input.scrollLeft,
        y: bounds.top + input.clientTop + parseFloat(style.paddingTop) + point.top - input.scrollTop,
        height: parseFloat(style.lineHeight), bounds
    };
}
export function 初期化(root, input, receiver, failureMessage) {
    let disposed = false;
    let composing = false;
    let compositionEnded = false;
    let inFlight = false;
    let selected = 0;
    let generation = 0;
    let pendingRequest = null;
    let requested = null;
    let pendingChoice = null;
    const list = () => root.querySelector('[role="listbox"]');
    const options = () => Array.from(root.querySelectorAll('[data-code-completion-index]:not([hidden])'));
    const ownsFocus = () => root.contains(document.activeElement);
    const place = () => {
        const popup = list();
        if (!popup || popup.hidden || document.activeElement !== input) return;
        const { x, y, height, bounds } = caretBounds(input);
        const margin = 8;
        const viewportWidth = document.documentElement.clientWidth;
        const viewportHeight = document.documentElement.clientHeight;
        if (x < Math.max(0, bounds.left) || x > Math.min(viewportWidth, bounds.right)
            || y + height < Math.max(0, bounds.top) || y > Math.min(viewportHeight, bounds.bottom)) {
            delete popup.dataset.caretPositioned;
            return;
        }
        const size = popup.getBoundingClientRect();
        let left = x + margin;
        let top = y;
        if (left + size.width > viewportWidth - margin) {
            left = x - margin - size.width;
            if (left < margin) { left = Math.max(margin, viewportWidth - size.width - margin); top = y + height; }
        }
        top = Math.max(margin, Math.min(top, viewportHeight - size.height - margin));
        popup.style.left = `${left}px`;
        popup.style.top = `${top}px`;
        popup.dataset.caretPositioned = 'true';
    };
    const report = error => {
        if (disposed) return;
        root.dataset.completionError = 'true';
        let alert = root.querySelector('[data-completion-error]');
        if (!alert) {
            alert = document.createElement('p'); alert.setAttribute('role', 'alert');
            alert.dataset.completionError = 'true'; root.append(alert);
        }
        alert.textContent = failureMessage;
        console.error('Code completion interop failed', error);
    };
    const cancel = () => {
        generation++; pendingRequest = null; requested = null; pendingChoice = null;
        const popup = list(); if (popup) popup.hidden = true;
        input.removeAttribute('aria-activedescendant');
        return receiver.invokeMethodAsync('補完取消Async').catch(report);
    };
    const update = () => {
        if (disposed) return;
        const popup = list();
        if (popup) {
            const prefix = requested?.prefix;
            const entries = Array.from(popup.querySelectorAll('[data-code-completion-index]'));
            const name = entry => entry.dataset.completionName.replace(/^@/, '');
            const exact = prefix != null && entries.some(entry => name(entry).startsWith(prefix));
            for (const entry of entries) {
                const candidate = name(entry);
                entry.hidden = prefix != null && !(exact
                    ? candidate.startsWith(prefix)
                    : candidate.toLowerCase().startsWith(prefix.toLowerCase()));
                if (entry.hidden) entry.setAttribute('aria-selected', 'false');
            }
            popup.hidden = !requested || !matches(requested) || options().length === 0;
        }
        const entries = options();
        selected = Math.min(selected, Math.max(0, entries.length - 1));
        entries.forEach((entry, index) => {
            if (!entry.id) entry.id = `completion-option-${++nextOptionId}`;
            entry.setAttribute('aria-selected', String(index === selected));
        });
        if (entries.length && !list()?.hidden) input.setAttribute('aria-activedescendant', entries[selected].id);
        else input.removeAttribute('aria-activedescendant');
        place();
    };
    const matches = request => !disposed && !composing && !input.readOnly && document.activeElement === input
        && generation === request.generation && input.value === request.text
        && root.dataset.completionScope === request.scope
        && input.selectionStart === request.position && input.selectionEnd === request.end;
    const capture = () => ({
        text: input.value, position: input.selectionStart, end: input.selectionStart,
        scope: root.dataset.completionScope, generation
    });
    const request = async snapshot => {
        if (!matches(snapshot)) return;
        if (inFlight) { pendingRequest = snapshot; return; }
        inFlight = true; requested = snapshot;
        if (snapshot.prefix == null) selected = 0;
        try {
            const method = snapshot.prefix == null ? '補完要求Async' : '補完再要求Async';
            await receiver.invokeMethodAsync(method, snapshot.position, snapshot.text);
            snapshot.ready = true;
        }
        catch (error) { report(error); }
        finally {
            inFlight = false;
            const next = pendingRequest; pendingRequest = null;
            if (next) void request(next);
            else if (pendingChoice?.snapshot === snapshot && snapshot.ready) {
                const choice = pendingChoice; pendingChoice = null;
                void apply(choice);
            }
        }
    };
    const apply = async ({ snapshot, name, kind }) => {
        if (disposed || composing || !ownsFocus() || !snapshot || snapshot !== requested) return;
        if (!matches(snapshot)) { await cancel(); return; }
        if (!snapshot.ready) { pendingChoice = { snapshot, name, kind }; return; }
        if (inFlight) return;
        inFlight = true;
        try {
            const result = await receiver.invokeMethodAsync('補完候補を選ぶAsync', name, kind, input.value);
            if (result && matches(snapshot)) {
                // 解析側の応答中に入力やキャレットが変わっていない場合だけ、通常の入力イベントへ渡す。
                input.value = result.text;
                input.setSelectionRange(result.position, result.position);
                input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertReplacementText' }));
            }
        } catch (error) { report(error); }
        finally {
            inFlight = false;
            const next = pendingRequest; pendingRequest = null;
            if (next) void request(next);
        }
    };
    const choose = entry => {
        if (entry) return apply({ snapshot: requested, name: entry.dataset.completionName, kind: entry.dataset.completionKind });
    };
    const keydown = async event => {
        if (disposed || event.target !== input || input.readOnly || composing || event.isComposing || event.keyCode === 229) return;
        if (compositionEnded && ['Enter', 'Escape'].includes(event.key)) { compositionEnded = false; return; }
        compositionEnded = false;
        if (event.ctrlKey && !event.altKey && !event.metaKey && event.code === 'Space') {
            event.preventDefault(); event.stopPropagation();
            await request(capture());
            return;
        }
        if (event.key === 'Escape' && (inFlight || pendingRequest)) {
            event.preventDefault(); event.stopPropagation(); await cancel(); return;
        }
        const popup = list();
        if (!popup || popup.hidden) return;
        const entries = options();
        if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); await cancel(); }
        else if (['ArrowDown', 'ArrowUp'].includes(event.key)) {
            event.preventDefault(); event.stopPropagation();
            const delta = event.key === 'ArrowDown' ? 1 : -1;
            selected = (selected + delta + entries.length) % entries.length;
            update(); entries[selected]?.scrollIntoView({ block: 'nearest' });
        } else if (event.key === 'Enter' || event.key === 'Tab') {
            event.preventDefault(); event.stopPropagation(); await choose(entries[selected]);
        } else if (['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) { await cancel(); }
    };
    const pointerdown = event => {
        const entry = event.target.closest('[data-code-completion-index]');
        if (event.target === input && (list() || inFlight)) { void cancel(); return; }
        if (!entry || !root.contains(entry)) return;
        event.preventDefault(); void choose(entry);
    };
    const requestAfterInput = data => {
        const snapshot = capture();
        const offset = snapshot.position;
        const dot = snapshot.text[offset - 1] === '.' && (data == null || data.endsWith('.'));
        const alias = snapshot.text.slice(Math.max(0, offset - 2), offset) === '::' && (data == null || data.endsWith(':'));
        if (dot || alias) requestAnimationFrame(() => { void request(snapshot); });
    };
    const inputChanged = event => {
        const previous = requested;
        generation++; pendingRequest = null; requested = null; pendingChoice = null;
        const popup = list(); if (popup) popup.hidden = true;
        input.removeAttribute('aria-activedescendant');
        if (composing || event.isComposing || event.inputType !== 'insertText') return;
        const snapshot = capture();
        const added = snapshot.position - (previous?.position ?? snapshot.position);
        if (popup && previous && previous.scope === snapshot.scope && added > 0) {
            const before = snapshot.text.slice(0, snapshot.position);
            const inserted = before.slice(previous.position);
            if (/^[\p{ID_Continue}@]+$/u.test(inserted)
                && previous.text === before.slice(0, previous.position) + snapshot.text.slice(snapshot.position)) {
                snapshot.prefix = (before.match(/[\p{ID_Continue}@]+$/u)?.[0] ?? '').replace(/^@/, '');
                selected = 0; requested = snapshot;
                update();
                options()[0]?.scrollIntoView({ block: 'nearest' });
                requestAnimationFrame(() => { void request(snapshot); });
                return;
            }
        }
        requestAfterInput(event.data);
    };
    const compositionStart = () => { composing = true; compositionEnded = false; void cancel(); };
    const compositionEnd = event => {
        composing = false; compositionEnded = true;
        if (event.data) requestAfterInput(event.data);
    };
    const focusout = event => { if (!root.contains(event.relatedTarget)) void cancel(); };
    input.addEventListener('keydown', keydown);
    input.addEventListener('input', inputChanged);
    input.addEventListener('compositionstart', compositionStart);
    input.addEventListener('compositionend', compositionEnd);
    root.addEventListener('pointerdown', pointerdown);
    root.addEventListener('focusout', focusout);
    window.addEventListener('scroll', place, true);
    window.addEventListener('resize', place);
    return {
        update,
        dispose() {
            disposed = true; generation++; pendingRequest = null; requested = null; pendingChoice = null;
            input.removeEventListener('keydown', keydown);
            input.removeEventListener('input', inputChanged);
            input.removeEventListener('compositionstart', compositionStart);
            input.removeEventListener('compositionend', compositionEnd);
            root.removeEventListener('pointerdown', pointerdown);
            root.removeEventListener('focusout', focusout);
            window.removeEventListener('scroll', place, true);
            window.removeEventListener('resize', place);
            input.removeAttribute('aria-activedescendant');
        }
    };
}
