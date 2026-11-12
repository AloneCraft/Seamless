let nextOptionId = 0;
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
        const entries = options();
        selected = Math.min(selected, Math.max(0, entries.length - 1));
        entries.forEach((entry, index) => {
            if (!entry.id) entry.id = `completion-option-${++nextOptionId}`;
            entry.setAttribute('aria-selected', String(index === selected));
        });
        if (entries.length && !list()?.hidden) input.setAttribute('aria-activedescendant', entries[selected].id);
        else input.removeAttribute('aria-activedescendant');
    };
    const matches = request => !disposed && !composing && !input.readOnly && document.activeElement === input
        && generation === request.generation && input.value === request.text
        && input.selectionStart === request.position && input.selectionEnd === request.position;
    const request = async snapshot => {
        if (!matches(snapshot)) return;
        if (inFlight) { pendingRequest = snapshot; return; }
        inFlight = true; selected = 0; requested = snapshot;
        try { await receiver.invokeMethodAsync('補完要求Async', snapshot.position, snapshot.text); }
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
            await request({ text: input.value, position: input.selectionStart, generation });
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
    const inputChanged = event => {
        generation++; pendingRequest = null; requested = null;
        const popup = list(); if (popup) popup.hidden = true;
        input.removeAttribute('aria-activedescendant');
        if (composing || compositionEnded || event.isComposing || event.inputType !== 'insertText' || event.data !== '.') return;
        const snapshot = { text: input.value, position: input.selectionStart, generation };
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
    root.addEventListener('focusout', focusout);
    return {
        update,
        dispose() {
            disposed = true; generation++; pendingRequest = null; requested = null;
            input.removeEventListener('keydown', keydown);
            input.removeEventListener('input', inputChanged);
            input.removeEventListener('compositionstart', compositionStart);
            input.removeEventListener('compositionend', compositionEnd);
            root.removeEventListener('pointerdown', pointerdown);
            root.removeEventListener('focusout', focusout);
            input.removeAttribute('aria-activedescendant');
        }
    };
}
