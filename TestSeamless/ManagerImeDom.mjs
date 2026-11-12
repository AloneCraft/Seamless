// 所有しているPlaywright Pageで実行する。OSのIMEや物理キー入力の検証とは区別する。
export async function verifyManagerIme(page, moduleUrl) {
    return await page.evaluate(async url => {
        const interaction = await import(url);
        const root = document.createElement('div');
        root.innerHTML = '<input aria-label="IME inline probe"><div><input aria-label="IME menu probe"><button role="menuitem">Run</button></div>';
        document.body.append(root);
        const inline = root.firstElementChild;
        const menu = root.lastElementChild;
        const search = menu.querySelector('input');
        const results = [];
        const counts = { commit: 0, cancel: 0, command: 0, close: 0 };
        const appKey = event => {
            if (event.target !== inline) return;
            if (event.key === 'Enter') counts.commit++;
            if (event.key === 'Escape') counts.cancel++;
        };
        const check = (name, actual, expected) => {
            if (actual !== expected) throw new Error(`${name}: expected ${expected}, got ${actual}`);
            results.push({ name, actual });
        };
        const compose = (input, type) => input.dispatchEvent(new CompositionEvent(type, { bubbles: true, data: '入力' }));
        const key = (input, name, extra = {}) => {
            const event = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key: name, ...extra });
            input.dispatchEvent(event);
            check(`${input.getAttribute('aria-label')} ${name} preserves IME default`, event.defaultPrevented, false);
        };
        const inlineGuard = interaction.IME入力を保護?.(inline) ?? { dispose() {} };
        let menuGuard;
        document.addEventListener('keydown', appKey);
        try {
            inline.value = 'dbo0';
            compose(inline, 'compositionstart');
            key(inline, 'Enter', { isComposing: true });
            key(inline, 'Escape', { isComposing: true });
            // compositionstartの状態でも守り、isComposingだけには依存しない。
            key(inline, 'Enter');
            check('inline composition commit count', counts.commit, 0);
            check('inline composition cancel count', counts.cancel, 0);
            compose(inline, 'compositionend');
            key(inline, 'Enter', { keyCode: 229 });
            check('inline compositionend 229 commit count', counts.commit, 0);
            check('inline value retained', inline.value, 'dbo0');
            key(inline, 'Enter');
            key(inline, 'Escape');
            check('inline next Enter commits once', counts.commit, 1);
            check('inline next Escape cancels once', counts.cancel, 1);
            inlineGuard.dispose();
            key(inline, 'Enter', { isComposing: true });
            check('inline disposal removes guard', counts.commit, 2);

            menu.querySelector('button').addEventListener('click', () => counts.command++);
            menuGuard = interaction.メニューを開く(menu, inline, 1, 1, false, {
                invokeMethodAsync(name) { if (name === '閉じるAsync') counts.close++; return Promise.resolve(); }
            });
            compose(search, 'compositionstart');
            key(search, 'Enter', { isComposing: true });
            key(search, 'Escape', { isComposing: true });
            key(search, 'Enter');
            check('menu composition command count', counts.command, 0);
            check('menu composition close count', counts.close, 0);
            compose(search, 'compositionend');
            key(search, 'Enter', { keyCode: 229 });
            key(search, 'Escape', { keyCode: 229 });
            check('menu compositionend 229 command count', counts.command, 0);
            check('menu compositionend 229 close count', counts.close, 0);
            // 通常キーではメニューの既存preventDefaultと実行・閉鎖を確認する。
            search.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key: 'Enter' }));
            search.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key: 'Escape' }));
            check('menu next Enter executes once', counts.command, 1);
            check('menu next Escape closes once', counts.close, 1);
            menuGuard.dispose();
            menuGuard = null;
            search.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key: 'Enter' }));
            check('menu disposal removes command handler', counts.command, 1);
            return { passed: true, physicalImeVerified: false, results };
        } finally {
            inlineGuard.dispose();
            menuGuard?.dispose();
            document.removeEventListener('keydown', appKey);
            root.remove();
        }
    }, moduleUrl);
}
