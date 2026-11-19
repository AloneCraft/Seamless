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
    const list = () => root.querySelector('[role="listbox"]');
    const options = () => Array.from(root.querySelectorAll('[data-code-completion-index]'));
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
        generation++; pendingRequest = null; requested = null;
        const popup = list(); if (popup) popup.hidden = true;
        input.removeAttribute('aria-activedescendant');
        return receiver.invokeMethodAsync('補完取消Async').catch(report);
    };
    const update = () => {
        if (disposed) return;
        const popup = list();
        if (popup && (!requested || !matches(requested))) popup.hidden = true;
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
    const capture = tables => ({
        text: input.value, position: input.selectionStart, end: tables ? input.selectionEnd : input.selectionStart,
        scope: root.dataset.completionScope, generation, tables
    });
    const request = async snapshot => {
        if (!matches(snapshot)) return;
        if (inFlight) { pendingRequest = snapshot; return; }
        inFlight = true; selected = 0; requested = snapshot;
        try {
            if (snapshot.tables) {
                await receiver.invokeMethodAsync('テーブル候補要求Async', snapshot.position, snapshot.end, snapshot.text);
            } else {
                await receiver.invokeMethodAsync('補完要求Async', snapshot.position, snapshot.text);
            }
        }
        catch (error) { report(error); }
        finally {
            inFlight = false;
            const next = pendingRequest; pendingRequest = null;
            if (next) void request(next);
        }
    };
    const apply = async index => {
        if (disposed || inFlight || composing || !ownsFocus() || !requested) return;
        const snapshot = requested;
        if (!matches(snapshot)) { await cancel(); return; }
        inFlight = true;
        try {
            const result = await receiver.invokeMethodAsync('補完適用Async', index, input.value);
            if (result && matches(snapshot)) {
                // 解析側の応答中に入力やキャレットが変わっていない場合だけ、通常の入力イベントへ渡す。
                input.value = result.text;
                input.setSelectionRange(result.position, result.position);
                input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertReplacementText' }));
            }
        } catch (error) { report(error); }
        finally { inFlight = false; }
    };
    const keydown = async event => {
        if (disposed || event.target !== input || input.readOnly || composing || event.isComposing || event.keyCode === 229) return;
        if (compositionEnded && ['Enter', 'Escape'].includes(event.key)) { compositionEnded = false; return; }
        compositionEnded = false;
        if (event.ctrlKey && !event.altKey && !event.metaKey && event.code === 'Space') {
            event.preventDefault(); event.stopPropagation();
            await request(capture(false));
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
            event.preventDefault(); event.stopPropagation(); await apply(selected);
        } else if (['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) { await cancel(); }
    };
    const pointerdown = event => {
        const entry = event.target.closest('[data-code-completion-index]');
        if (event.target === input && (list() || inFlight)) { void cancel(); return; }
        if (!entry || !root.contains(entry)) return;
        event.preventDefault(); selected = Number(entry.dataset.codeCompletionIndex); void apply(selected);
    };
    const click = event => {
        if (!event.target.closest('[data-table-picker]') || input.readOnly || composing) return;
        input.focus({ preventScroll: true });
        void request(capture(true));
    };
    const inputChanged = event => {
        generation++; pendingRequest = null; requested = null;
        const popup = list(); if (popup) popup.hidden = true;
        input.removeAttribute('aria-activedescendant');
        if (composing || compositionEnded || event.isComposing || event.inputType !== 'insertText' || event.data !== '.') return;
        const snapshot = capture(false);
        requestAnimationFrame(() => { if (matches(snapshot)) void request(snapshot); });
    };
    const compositionStart = () => { composing = true; compositionEnded = false; void cancel(); };
    const compositionEnd = () => { composing = false; compositionEnded = true; };
    const focusout = event => { if (!root.contains(event.relatedTarget)) void cancel(); };
    input.addEventListener('keydown', keydown);
    input.addEventListener('input', inputChanged);
    input.addEventListener('compositionstart', compositionStart);
    input.addEventListener('compositionend', compositionEnd);
    root.addEventListener('pointerdown', pointerdown);
    root.addEventListener('click', click);
    root.addEventListener('focusout', focusout);
    window.addEventListener('scroll', place, true);
    window.addEventListener('resize', place);
    return {
        update,
        dispose() {
            disposed = true; generation++; pendingRequest = null; requested = null;
            input.removeEventListener('keydown', keydown);
            input.removeEventListener('input', inputChanged);
            input.removeEventListener('compositionstart', compositionStart);
            input.removeEventListener('compositionend', compositionEnd);
            root.removeEventListener('pointerdown', pointerdown);
            root.removeEventListener('click', click);
            root.removeEventListener('focusout', focusout);
            window.removeEventListener('scroll', place, true);
            window.removeEventListener('resize', place);
            input.removeAttribute('aria-activedescendant');
        }
    };
}
