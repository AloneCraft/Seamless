const 操作メニュー所有者 = new WeakMap();

export function IME入力を保護(根) {
    let 変換対象 = null;
    const 変換開始 = 引数 => { 変換対象 = 引数.target; };
    const 変換終了 = 引数 => { if (変換対象 === 引数.target) 変換対象 = null; };
    const 変換中か = 引数 => 引数.isComposing || 引数.keyCode === 229 || 変換対象 === 引数.target;
    const キー = 引数 => {
        // IMEの確定・取消自体はブラウザーへ渡し、アプリの確定・取消だけを止める。
        if ((引数.key === 'Enter' || 引数.key === 'Escape') && 変換中か(引数)) 引数.stopPropagation();
    };
    根.addEventListener('compositionstart', 変換開始, true);
    根.addEventListener('compositionend', 変換終了, true);
    根.addEventListener('keydown', キー, true);
    return {
        変換中か,
        dispose() { 変換対象 = null; 根.removeEventListener('compositionstart', 変換開始, true); 根.removeEventListener('compositionend', 変換終了, true); 根.removeEventListener('keydown', キー, true); }
    };
}

export function メニューを開く(要素, 起点, X, Y, 中央か, 参照, 戻り先 = 起点) {
    let 破棄済みか = false;
    const 元の行 = 起点?.closest?.('[data-対象-id]');
    if (元の行) 操作メニュー所有者.set(要素, 元の行);
    const 元のフォーカス = document.activeElement;
    if (戻り先 === 起点 && 元の行?.contains(元のフォーカス)) 戻り先 = 元のフォーカス;
    const 親の行 = 元の行?.closest('[data-tree-children]')?.previousElementSibling;
    let 閉鎖時に戻すか = true, メニュー内フォーカスか = false;
    const フォーカス変更 = 引数 => { メニュー内フォーカスか = 要素.contains(引数.target); };
    const フォーカスを戻す = () => {
        for (const 候補 of [戻り先, 起点, 親の行]) {
            if (候補?.isConnected && !候補.disabled && !候補.closest('[hidden],[inert]')) { 候補.focus(); break; }
        }
    };
    const IME = IME入力を保護(要素);
    const 配置 = () => {
        if (破棄済みか) return;
        const 幅 = window.innerWidth, 高さ = window.innerHeight;
        要素.style.maxHeight = `${Math.max(100, 高さ - 24)}px`;
        const 枠 = 要素.getBoundingClientRect();
        let 左 = X, 上 = Y;
        if (中央か) { 左 = (幅 - 枠.width) / 2; 上 = Math.min(120, 高さ / 6); }
        else if (!X && !Y && 起点?.getBoundingClientRect) { const 対象 = 起点.getBoundingClientRect(); 左 = 対象.left; 上 = 対象.bottom + 6; }
        if (上 + 枠.height > 高さ - 12) 上 = Math.max(12, 上 - 枠.height - 10);
        要素.style.left = `${Math.max(12, Math.min(左, 幅 - 枠.width - 12))}px`;
        要素.style.top = `${Math.max(12, 上)}px`;
        要素.style.visibility = 'visible';
    };
    const 閉じる = (戻すか) => { if (破棄済みか) return; 閉鎖時に戻すか = 戻すか; if (戻すか) フォーカスを戻す(); 参照.invokeMethodAsync('閉じるAsync'); };
    const 外側 = (引数) => { if (!要素.contains(引数.target) && !起点?.contains?.(引数.target)) 閉じる(false); };
    const キー = (引数) => {
        if (IME.変換中か(引数)) return;
        if (引数.key === 'Escape') { 引数.preventDefault(); 引数.stopPropagation(); 閉じる(true); return; }
        if (!要素.contains(引数.target)) return;
        const 項目 = [...要素.querySelectorAll('[role="menuitem"]:not(:disabled)')];
        const 現在 = 項目.indexOf(document.activeElement);
        if (引数.key === 'ArrowDown' || 引数.key === 'ArrowUp') {
            引数.preventDefault(); let 次 = 現在 + 1;
            if (引数.key === 'ArrowUp') 次 = 現在 - 1;
            if (次 < 0) 次 = 項目.length - 1;
            項目[次 % 項目.length]?.focus();
        } else if (引数.key === 'Enter' && 引数.target.matches('input')) { 引数.preventDefault(); 項目[0]?.click(); }
        else if (引数.key === 'ArrowRight' && 引数.target.hasAttribute('data-サブメニュー')) { 引数.preventDefault(); 引数.target.click(); }
        else if (引数.key === 'ArrowLeft' && !引数.target.matches('input')) { 引数.preventDefault(); 参照.invokeMethodAsync('戻るAsync'); }
    };
    document.addEventListener('pointerdown', 外側, true);
    document.addEventListener('keydown', キー, true);
    document.addEventListener('focusin', フォーカス変更, true);
    window.addEventListener('resize', 配置);
    document.addEventListener('scroll', 配置, true);
    const 監視 = new ResizeObserver(配置); 監視.observe(要素);
    配置(); 要素.querySelector('input')?.focus();
    return { dispose() { if (破棄済みか) return; 破棄済みか = true; if (閉鎖時に戻すか && (要素.contains(document.activeElement) || メニュー内フォーカスか && document.activeElement === document.body)) フォーカスを戻す(); IME.dispose(); 監視.disconnect(); document.removeEventListener('pointerdown', 外側, true); document.removeEventListener('keydown', キー, true); document.removeEventListener('focusin', フォーカス変更, true); window.removeEventListener('resize', 配置); document.removeEventListener('scroll', 配置, true); } };
}

export function メニュー内へフォーカス(要素) {
    if (!要素.contains(document.activeElement)) 要素.querySelector('[role="menuitem"]:not(:disabled)')?.focus();
}

export function 型入力を初期化(根, 参照) {
    const 入力 = 根.querySelector('input');
    const IME = IME入力を保護(根);
    const 明示候補 = () => { 根.dataset.候補操作 = 'true'; };
    const 候補ボタン = 引数 => {
        if (引数.target.closest('.型候補ボタン')) 根.dataset.候補操作 = String(根.dataset.候補表示 !== 'true');
    };
    const 自動候補 = () => { delete 根.dataset.候補操作; };
    const 表示監視 = new MutationObserver(() => { if (根.dataset.候補表示 !== 'true') delete 根.dataset.候補操作; });
    表示監視.observe(根, { attributes: true, attributeFilter: ['data-候補表示'] });
    const キー = 引数 => {
        if (IME.変換中か(引数)) return;
        const 補完要求か = 引数.key === ' ' && (引数.ctrlKey || 引数.metaKey);
        const 確定か = (引数.key === 'Enter' || 引数.key === 'Tab') && 根.dataset.候補表示 === 'true';
        if (引数.key === 'ArrowDown' || 引数.key === 'ArrowUp' || 確定か || 補完要求か) {
            if (引数.altKey || 補完要求か) 明示候補();
            引数.preventDefault(); 引数.stopPropagation();
            let キー = 引数.key; if (確定か) キー = 'Enter';
            参照.invokeMethodAsync('キー入力Async', キー, 引数.altKey || 補完要求か, 入力.selectionStart ?? 入力.value.length);
        }
    };
    const フォーカス変更 = () => queueMicrotask(() => { if (!根.contains(document.activeElement)) { delete 根.dataset.候補操作; 参照.invokeMethodAsync('閉じるAsync'); } });
    根.addEventListener('keydown', キー); 根.addEventListener('focusout', フォーカス変更);
    根.addEventListener('click', 候補ボタン, true); 入力.addEventListener('input', 自動候補);
    return {
        caret() { return 入力.selectionStart ?? 入力.value.length; },
        inputState() { return { caret: 入力.selectionStart ?? 入力.value.length, focused: document.activeElement === 入力 }; },
        select(開始, 長さ) { 入力.focus(); 入力.setSelectionRange(開始, 開始 + 長さ); },
        dispose() { IME.dispose(); 表示監視.disconnect(); delete 根.dataset.候補操作; 根.removeEventListener('keydown', キー); 根.removeEventListener('focusout', フォーカス変更); 根.removeEventListener('click', 候補ボタン, true); 入力.removeEventListener('input', 自動候補); }
    };
}

export function 型候補を表示(一覧) {
    一覧.querySelector('[aria-selected="true"]')?.scrollIntoView({ block: 'nearest' });
}

export function 編集グリッドを初期化(根, データ入力か = false) {
    let 選択セル = null, 編集セル = null, 開始値 = [], 行位置 = 0, 列位置 = 0;
    const 領域 = データ入力か ? 根.closest('.結果グリッド領域') : null;
    let 追加前の最終行;
    const 行追加 = () => { 追加前の最終行 = 根.querySelector('[data-grid-placeholder]')?.previousElementSibling ?? null; };
    const 移動選択入力 = new WeakSet(), 変換中入力 = new WeakSet();
    const 文字編集入力 = new WeakSet();
    let 次行待ち = null;
    const 行一覧 = () => [...根.tBodies].flatMap(本体 => [...本体.rows]);
    const セル一覧 = 行 => [...行.cells].filter(セル => !セル.classList.contains('行操作列'));
    const 入力一覧 = セル => [...セル.querySelectorAll('input:not([type="hidden"]):not(:disabled),select:not(:disabled),textarea:not(:disabled)')];
    const 文字入力か = 入力 => 入力?.matches('input,textarea') && typeof 入力.selectionStart === 'number';
    const 全文選択か = 入力 => {
        if (!文字入力か(入力)) return false;
        if (入力.value.length === 0) return 移動選択入力.has(入力);
        return 入力.selectionStart === 0 && 入力.selectionEnd === 入力.value.length;
    };
    const 対象セル = 要素 => {
        const セル = 要素.closest('td[role="gridcell"]');
        if (セル?.closest('table') === 根) return セル;
        return null;
    };
    const 編集終了 = () => {
        編集セル?.removeAttribute('data-編集中');
        編集セル = null; 開始値 = [];
    };
    const 選択 = セル => {
        if (選択セル !== セル) {
            編集終了();
            選択セル?.setAttribute('aria-selected', 'false');
            if (選択セル) 選択セル.tabIndex = -1;
        }
        選択セル = セル;
        セル.tabIndex = 0; セル.setAttribute('aria-selected', 'true');
        行位置 = 行一覧().indexOf(セル.parentElement);
        列位置 = セル一覧(セル.parentElement).indexOf(セル);
    };
    const 移動 = セル => {
        if (!セル) return;
        選択(セル); 編集終了();
        const 入力 = 入力一覧(セル)[0];
        if (文字入力か(入力)) {
            文字編集入力.delete(入力);
            入力.focus(); 編集開始(セル, false); 入力.select(); 移動選択入力.add(入力);
        } else セル.focus();
        セル.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    };
    const 編集開始 = (セル, フォーカスか, 全選択か = true) => {
        const 入力 = 入力一覧(セル);
        if (!入力.length) return;
        選択(セル);
        if (編集セル !== セル) {
            const 対象 = new Set(入力);
            for (const 項目 of 入力.filter(項目 => 項目.type === 'radio' && 項目.name)) {
                for (const 同組 of 根.querySelectorAll('input[type="radio"]')) {
                    if (同組.name === 項目.name && 同組.form === 項目.form) 対象.add(同組);
                }
            }
            開始値 = [...対象].map(項目 => ({ 要素: 項目, 値: 項目.value, 選択中か: 項目.checked }));
            編集セル = セル; セル.setAttribute('data-編集中', 'true');
        }
        if (フォーカスか) {
            入力[0].focus();
            移動選択入力.delete(入力[0]);
            if (文字入力か(入力[0])) {
                if (全選択か) { 入力[0].select(); 移動選択入力.add(入力[0]); }
                else 入力[0].setSelectionRange(入力[0].value.length, 入力[0].value.length);
            }
        }
    };
    const フォーカス = 引数 => {
        if (引数.target.closest('[role="listbox"],[role="menu"]')) return;
        const セル = 対象セル(引数.target);
        if (!セル) return;
        選択(セル);
        if (引数.target.matches('input,select,textarea')) 編集開始(セル, false);
        else if (引数.target === セル) {
            編集終了();
            if (データ入力か && セル.querySelector('input:not([readonly])')) 編集開始(セル, true);
        }
    };
    const ポインター = 引数 => {
        if (文字入力か(引数.target)) 移動選択入力.delete(引数.target);
        const セル = 対象セル(引数.target);
        if (!セル) return;
        if (データ入力か && 引数.button === 0 && セル !== 選択セル && 引数.target.matches('input:not([readonly])')) {
            引数.preventDefault(); 移動(セル);
        } else if (データ入力か && 引数.button === 0 && 引数.target === document.activeElement && 文字入力か(引数.target)) {
            文字編集入力.add(引数.target);
        } else if (!引数.target.closest('input,select,textarea,button,label')) 移動(セル);
    };
    const 離脱 = 引数 => {
        移動選択入力.delete(引数.target);
        文字編集入力.delete(引数.target);
        queueMicrotask(() => {
            if (編集セル && !編集セル.contains(document.activeElement)) 編集終了();
            if (選択セル?.isConnected && !根.contains(document.activeElement)) 選択セル.tabIndex = 0;
        });
    };
    // 最初の入力がサーバーで行に反映される前に移動キーを押したときは、追加の描画を待つ。
    const 次の新規行へ = (セル, 列) => {
        const 行 = セル.parentElement;
        if (!データ入力か || !行.hasAttribute('data-grid-placeholder') || ![...行.querySelectorAll('input')].some(入力 => 入力.value.length)) return false;
        次行待ち = { セル, 行, 列 };
        return true;
    };
    const キー = (引数, 型移動か = false) => {
        if (引数.isComposing || 変換中入力.has(引数.target) || 引数.keyCode === 229 || 引数.defaultPrevented
            || 引数.target.closest('[role="listbox"],[role="menu"]') || (!型移動か && 引数.target.matches('[role="combobox"][aria-expanded="true"]'))) return;
        const セル = 対象セル(引数.target);
        if (データ入力か && !引数.target.closest('button')) {
            let 操作ボタン = null;
            if ((引数.ctrlKey || 引数.metaKey) && !引数.altKey && 引数.key.toLowerCase() === 's') {
                引数.preventDefault();
                操作ボタン = 領域?.querySelector('[data-grid-save]:enabled');
            } else if (!引数.altKey && 引数.key === 'Enter' && (引数.ctrlKey || 引数.metaKey || 引数.target === 根 && !行一覧().length)) {
                操作ボタン = 領域?.querySelector('[data-grid-add]:enabled');
            } else if (引数.altKey && !引数.ctrlKey && 引数.key === 'Delete') {
                操作ボタン = セル?.parentElement.querySelector('.行操作列 button:enabled:not(.セル競合 button)');
            }
            if (操作ボタン) {
                引数.preventDefault(); 引数.stopPropagation();
                操作ボタン.click(); return;
            }
            if (引数.ctrlKey || 引数.metaKey || 引数.altKey) return;
        }
        if (データ入力か && 引数.target.closest('button')) return;
        if (!セル) return;
        if ((引数.key === 'F10' && 引数.shiftKey) || 引数.key === 'ContextMenu') {
            const ハンドル = セル.parentElement.querySelector('[data-メニュー起点]');
            if (ハンドル) { 引数.preventDefault(); 引数.stopPropagation(); ハンドル.click(); }
            return;
        }
        if (引数.key === 'Escape' && 編集セル) {
            次行待ち = null;
            引数.preventDefault(); 引数.stopPropagation();
            for (const 記録 of 開始値) {
                if (記録.要素.value === 記録.値 && (!('checked' in 記録.要素) || 記録.要素.checked === 記録.選択中か)) continue;
                記録.要素.value = 記録.値;
                if ('checked' in 記録.要素) 記録.要素.checked = 記録.選択中か;
                記録.要素.dispatchEvent(new Event('input', { bubbles: true }));
                記録.要素.dispatchEvent(new Event('change', { bubbles: true }));
            }
            移動(セル); return;
        }
        if (引数.key === 'Enter' || 引数.key === 'F2') {
            引数.preventDefault(); 引数.stopPropagation();
            if (データ入力か && 引数.key === 'Enter') {
                const 行 = 行一覧();
                const 次行 = 行[行.indexOf(セル.parentElement) + (引数.shiftKey ? -1 : 1)];
                const 列 = セル一覧(セル.parentElement).indexOf(セル);
                if (!引数.shiftKey && !次行 && 次の新規行へ(セル, 列)) return;
                移動(次行 && セル一覧(次行)[列] || セル);
            } else if (編集セル && 引数.key === 'Enter') 移動(セル);
            else {
                編集開始(セル, true, false);
                if (データ入力か && 文字入力か(document.activeElement)) 文字編集入力.add(document.activeElement);
            }
            return;
        }
        if (引数.key === 'Tab') {
            const 競合ボタン = データ入力か && !引数.shiftKey && (セル.querySelector('.セル競合 button:enabled') || セル.parentElement.querySelector('.行操作列 .セル競合 button:enabled'));
            if (競合ボタン) {
                引数.preventDefault(); 競合ボタン.focus(); return;
            }
            const 全セル = 行一覧().flatMap(セル一覧);
            let 差 = 1; if (引数.shiftKey) 差 = -1;
            const 次 = 全セル[全セル.indexOf(セル) + 差];
            if (次) { 引数.preventDefault(); 引数.stopPropagation(); 移動(次); }
            else if (!引数.shiftKey && 次の新規行へ(セル, 0)) { 引数.preventDefault(); 引数.stopPropagation(); }
            else { 編集終了(); セル.tabIndex = -1; }
            return;
        }
        if (編集セル && 引数.target.type !== 'checkbox' && (データ入力か ? 文字編集入力.has(引数.target) : !全文選択か(引数.target))) return;
        if (引数.ctrlKey || 引数.metaKey || 引数.altKey) return;
        if (引数.shiftKey && 引数.key.startsWith('Arrow')) return;
        if (引数.key === ' ' && 引数.target === セル) {
            const 選択入力 = セル.querySelector('input[type="checkbox"]:not(:disabled),input[type="radio"]:not(:disabled)');
            if (選択入力) { 引数.preventDefault(); 選択入力.click(); }
            return;
        }
        const 行 = 行一覧(), 列 = セル一覧(セル.parentElement), 番号 = 列.indexOf(セル);
        let 次 = null;
        if (引数.key === 'ArrowLeft') 次 = 列[番号 - 1];
        else if (引数.key === 'ArrowRight') 次 = 列[番号 + 1];
        else if (引数.key === 'ArrowUp' || 引数.key === 'ArrowDown') {
            let 差 = 1; if (引数.key === 'ArrowUp') 差 = -1;
            const 次行 = 行[行.indexOf(セル.parentElement) + 差];
            if (次行) 次 = セル一覧(次行)[番号];
            else if (差 > 0) 次の新規行へ(セル, 番号);
        } else return;
        引数.preventDefault(); 引数.stopPropagation(); 移動(次);
    };
    // 型補完のbubble処理より先に、全文選択中の通常矢印だけをグリッドへ渡す。
    const 型セル移動 = 引数 => {
        if (!引数.key.startsWith('Arrow') || 引数.ctrlKey || 引数.metaKey || 引数.altKey || 引数.shiftKey) return;
        const 型 = 引数.target.closest('.型入力');
        if (!型 || 型.dataset.候補操作 === 'true' || !全文選択か(引数.target)) return;
        キー(引数, true);
    };
    const 更新 = () => {
        const 行 = 行一覧();
        for (const 見出し of 根.querySelectorAll('thead th')) 見出し.setAttribute('role', 'columnheader');
        for (const 対象行 of 根.rows) 対象行.setAttribute('role', 'row');
        for (const 対象行 of 行) {
            for (const セル of 対象行.cells) {
                let 役割 = 'gridcell'; if (セル.classList.contains('行操作列')) 役割 = 'rowheader';
                セル.setAttribute('role', 役割); セル.tabIndex = -1;
                for (const 入力 of セル.querySelectorAll('input,select,textarea,button,[tabindex]')) {
                    if (!データ入力か || !入力.closest('.セル競合')) 入力.tabIndex = -1;
                }
            }
        }
        根.setAttribute('aria-rowcount', String(行.length + 根.tHead.rows.length));
        根.setAttribute('aria-colcount', String(根.tHead.rows[0]?.cells.length || 0));
        if (選択セル?.isConnected) 選択(選択セル);
        else {
            const 前の選択か = 選択セル !== null;
            const 次行 = 行[Math.min(行位置, 行.length - 1)];
            const 次列 = 次行 && セル一覧(次行);
            const 次 = 次列?.[Math.min(列位置, 次列.length - 1)];
            選択セル = null; 編集終了();
            if (次) { 選択(次); if (前の選択か && document.activeElement === document.body) 移動(次); }
        }
        根.tabIndex = -1; if (!行.length) 根.tabIndex = 0;
        if (次行待ち) {
            const { セル, 行: 入力行, 列 } = 次行待ち;
            if (!セル.contains(document.activeElement)) 次行待ち = null;
            else if (入力行.nextElementSibling?.hasAttribute('data-grid-placeholder')) {
                次行待ち = null; 移動(セル一覧(入力行.nextElementSibling)[列]);
            }
        }
        if (追加前の最終行 !== undefined) {
            const 末尾 = 行.at(-1)?.hasAttribute('data-grid-placeholder') ? 行.at(-2) : 行.at(-1);
            if (末尾?.hasAttribute('data-grid-new') && 末尾 !== 追加前の最終行) {
                追加前の最終行 = undefined;
                if (領域?.contains(document.activeElement) || document.activeElement === document.body) 移動(セル一覧(末尾)[0]);
            }
        }
    };
    const 監視 = new MutationObserver(変更 => {
        if (変更.some(項目 => 項目.target.matches('tbody,tr,td'))) 更新();
    });
    監視.observe(根, { childList: true, subtree: true });
    const 入力変更 = 引数 => 移動選択入力.delete(引数.target);
    const 変換開始 = 引数 => 変換中入力.add(引数.target);
    const 変換終了 = 引数 => 変換中入力.delete(引数.target);
    根.addEventListener('focusin', フォーカス); 根.addEventListener('focusout', 離脱); 根.addEventListener('pointerdown', ポインター); 根.addEventListener('keydown', キー);
    根.addEventListener('keydown', 型セル移動, true);
    根.addEventListener('input', 入力変更); 根.addEventListener('compositionstart', 変換開始); 根.addEventListener('compositionend', 変換終了);
    const 追加クリック = 引数 => { if (引数.target.closest('[data-grid-add]:enabled')) 行追加(); };
    領域?.addEventListener('click', 追加クリック);
    更新();
    return { dispose() { 監視.disconnect(); 領域?.removeEventListener('click', 追加クリック); 根.removeEventListener('focusin', フォーカス); 根.removeEventListener('focusout', 離脱); 根.removeEventListener('pointerdown', ポインター); 根.removeEventListener('keydown', キー); 根.removeEventListener('keydown', 型セル移動, true); 根.removeEventListener('input', 入力変更); 根.removeEventListener('compositionstart', 変換開始); 根.removeEventListener('compositionend', 変換終了); } };
}

export function ツリーを初期化(根, IME) {
    const 行か = 要素 => 要素?.matches('.ツリー列行') && !!要素.closest('.DB一覧') || 要素?.matches('.対象行') && (!!要素.closest('.DB一覧') || 要素.getAttribute('data-対象-id') === 'databases');
    const 可視行 = () => [...根.querySelectorAll('.対象行,.ツリー列行')].filter(要素 => 行か(要素) && !要素.disabled && !要素.closest('[hidden],[inert]') && 要素.getClientRects().length > 0);
    const 親行 = 行 => {
        const 子一覧 = 行.closest('[data-tree-children]');
        if (子一覧) return 子一覧.previousElementSibling;
        if (行.closest('.DB一覧')) return 根.querySelector('[data-対象-id="databases"]');
        return null;
    };
    const フォーカス = 行 => { if (行) { 行.focus({ preventScroll: true }); 行.scrollIntoView({ block: 'nearest' }); } };
    const キー = 引数 => {
        const 行 = 引数.target.closest('.対象行,.ツリー列行');
        if (!行か(行) || 引数.defaultPrevented) return;
        if (IME.変換中か(引数)) { 引数.stopPropagation(); return; }
        if (引数.target.closest('input,textarea,select,[contenteditable="true"],.型入力,.ツリー列入力') || 引数.ctrlKey || 引数.metaKey || 引数.altKey) return;
        const メニューか = 引数.key === 'ContextMenu' || 引数.key === 'F10' && 引数.shiftKey;
        const 矢印か = ['ArrowDown', 'ArrowUp', 'ArrowRight', 'ArrowLeft'].includes(引数.key) && !引数.shiftKey;
        const 選択か = (引数.key === 'Enter' || 引数.key === ' ') && 引数.target === 行 && !引数.shiftKey;
        if (!メニューか && !矢印か && !選択か) return;
        if (メニューか && !行.querySelector('[data-メニュー起点]')) return;
        引数.preventDefault(); 引数.stopPropagation();
        if (メニューか) { 行.querySelector('[data-メニュー起点]')?.click(); return; }
        if (選択か) { 行.click(); return; }
        const 一覧 = 可視行(), 位置 = 一覧.indexOf(行);
        if (引数.key === 'ArrowDown') { フォーカス(一覧[位置 + 1]); return; }
        if (引数.key === 'ArrowUp') { フォーカス(一覧[位置 - 1]); return; }
        const 開閉 = 行.querySelector(':scope > .対象内容 > .枝の開閉');
        if (引数.key === 'ArrowRight') {
            if (開閉?.getAttribute('aria-expanded') === 'false') 開閉.click();
            else { const 子 = 一覧[位置 + 1]; if (子 && 親行(子) === 行) フォーカス(子); }
        } else if (開閉?.getAttribute('aria-expanded') === 'true') 開閉.click();
        else フォーカス(親行(行));
    };
    根.addEventListener('keydown', キー, true);
    return { dispose() { 根.removeEventListener('keydown', キー, true); } };
}

export function 列編集フォーカスを初期化(根) {
    const 木 = 根.querySelector('.DB一覧');
    if (!木) return { dispose() {} };
    const 編集一覧 = new Map();
    let 最後のフォーカス = document.activeElement;
    const フォーカス変更 = 引数 => { 最後のフォーカス = 引数.target; };
    const 編集元か = (要素, 列一覧, 行) => {
        if (!要素) return false;
        if (列一覧.contains(要素) || 行?.contains(要素)) return true;
        for (let 祖先 = 要素; 祖先; 祖先 = 祖先.parentElement) { if (操作メニュー所有者.get(祖先) === 行 && 行) return true; }
        return false;
    };
    const 更新 = () => {
        for (const [フォーム, 状態] of 編集一覧) {
            if (木.contains(フォーム)) continue;
            if (フォーム.contains(document.activeElement) || document.activeElement === document.body && フォーム.contains(最後のフォーカス)) {
                for (const 起点 of [状態.起点, 状態.行]) {
                    if (起点?.isConnected && !起点.disabled && !起点.closest('[hidden],[inert]')) { 起点.focus({ preventScroll: true }); break; }
                }
            }
            編集一覧.delete(フォーム);
        }
        for (const フォーム of 木.querySelectorAll('.ツリー列一覧 [data-column-editor]')) {
            if (編集一覧.has(フォーム)) continue;
            const 列一覧 = フォーム.closest('.ツリー列一覧');
            const 行 = 列一覧.closest('[data-tree-children]')?.previousElementSibling;
            let 元の要素 = document.activeElement;
            if (元の要素 === document.body) 元の要素 = 最後のフォーカス;
            let 起点 = 行;
            if (列一覧.contains(元の要素) && 元の要素.matches('.ツリー列行')) 起点 = 元の要素;
            編集一覧.set(フォーム, { 起点, 行 });
            const 入力 = フォーム.querySelector('[data-column-name-input]');
            if (入力 && document.activeElement !== 入力 && !入力.matches(':disabled') && !フォーム.closest('[hidden],[inert]') && 編集元か(元の要素, 列一覧, 行)) {
                入力.focus({ preventScroll: true }); 入力.select();
            }
        }
    };
    document.addEventListener('focusin', フォーカス変更, true);
    const 監視 = new MutationObserver(更新); 監視.observe(木, { childList: true, subtree: true });
    更新();
    return { dispose() { 監視.disconnect(); document.removeEventListener('focusin', フォーカス変更, true); 編集一覧.clear(); } };
}

export function DB表示順ドラッグを初期化(根, 参照, IME) {
    let 移動 = null, 幽霊 = null, 指示 = null, クリック抑止か = false;
    const 掃除 = () => {
        移動?.行.classList.remove('DB表示移動元');
        幽霊?.remove(); 指示?.remove(); 幽霊 = 指示 = null; 移動 = null;
        根.classList.remove('DB表示移動中', 'DB表示移動禁止');
    };
    const 取消 = () => { if (移動?.開始済みか) クリック抑止か = true; 掃除(); };
    const 押下 = 引数 => {
        クリック抑止か = false;
        const ハンドル = 引数.target.closest('[data-db-order-handle]');
        const 行 = ハンドル?.closest('[data-db-order-row]');
        if (!行 || 引数.button !== 0 || 引数.pointerType === 'touch' || 根.closest('[inert]') || IME.変換中か(引数) || 行.dataset.dbOrderEnabled !== 'true') return;
        移動 = { ID: 行.dataset.dbOrderRow, 行, ハンドル, X: 引数.clientX, Y: 引数.clientY, ポインター: 引数.pointerId, 開始済みか: false, 対象: null };
    };
    const 位置変更 = 引数 => {
        if (!移動 || 引数.pointerId !== 移動.ポインター) return;
        if (!移動.行.isConnected || 移動.行.dataset.dbOrderEnabled !== 'true' || 移動.行.closest('[inert]')) { 取消(); return; }
        if (!移動.開始済みか && Math.hypot(引数.clientX - 移動.X, 引数.clientY - 移動.Y) < 5) return;
        if (!移動.開始済みか) {
            移動.開始済みか = true; 根.classList.add('DB表示移動中'); 移動.行.classList.add('DB表示移動元');
            幽霊 = document.createElement('div'); 幽霊.className = '移動プレビュー'; 幽霊.textContent = 移動.行.getAttribute('aria-label'); document.body.append(幽霊);
            指示 = document.createElement('div'); 指示.className = '挿入位置'; document.body.append(指示);
        }
        引数.preventDefault(); 幽霊.style.left = `${引数.clientX + 14}px`; 幽霊.style.top = `${引数.clientY + 14}px`;
        const 行 = document.elementFromPoint(引数.clientX, 引数.clientY)?.closest('[data-db-order-row]');
        移動.対象 = null; 指示.style.display = 'none'; 根.classList.add('DB表示移動禁止');
        if (!行 || !根.contains(行) || 行 === 移動.行 || 行.dataset.dbOrderEnabled !== 'true') return;
        const 枠 = 行.getBoundingClientRect(); 移動.前か = 引数.clientY < 枠.top + 枠.height / 2; 移動.対象 = 行.dataset.dbOrderRow;
        指示.style.cssText = `display:block;left:${枠.left}px;top:${移動.前か ? 枠.top : 枠.bottom}px;width:${枠.width}px;height:3px`;
        根.classList.remove('DB表示移動禁止');
    };
    const 解放 = async 引数 => {
        if (!移動 || 引数.pointerId !== 移動.ポインター) return;
        位置変更(引数);
        const 現在 = 移動;
        if (!現在) return;
        if (現在.開始済みか) クリック抑止か = true;
        掃除();
        if (現在.開始済みか && 現在.対象) await 参照.invokeMethodAsync('DB表示順ドロップAsync', 現在.ID, 現在.対象, 現在.前か);
    };
    const クリック = 引数 => { if (クリック抑止か) { 引数.preventDefault(); 引数.stopPropagation(); クリック抑止か = false; } };
    const キー = 引数 => { if (引数.key === 'Escape' && 移動 && !IME.変換中か(引数)) { 引数.preventDefault(); 引数.stopPropagation(); 取消(); } };
    根.addEventListener('pointerdown', 押下); 根.addEventListener('click', クリック, true); 根.addEventListener('keydown', キー, true);
    document.addEventListener('pointermove', 位置変更, { passive: false }); document.addEventListener('pointerup', 解放); document.addEventListener('pointercancel', 取消); window.addEventListener('blur', 取消);
    return { dispose() { 掃除(); 根.removeEventListener('pointerdown', 押下); 根.removeEventListener('click', クリック, true); 根.removeEventListener('keydown', キー, true); document.removeEventListener('pointermove', 位置変更); document.removeEventListener('pointerup', 解放); document.removeEventListener('pointercancel', 取消); window.removeEventListener('blur', 取消); } };
}

export function 初期化(根, 参照) {
    const IME = IME入力を保護(根);
    const ツリー = ツリーを初期化(根, IME);
    const 列フォーカス = 列編集フォーカスを初期化(根);
    const DBドラッグ = DB表示順ドラッグを初期化(根, 参照, IME);
    const キー = (引数) => {
        if (IME.変換中か(引数)) return;
        if ((引数.ctrlKey || 引数.metaKey) && 引数.key.toLowerCase() === 'k') { 引数.preventDefault(); 参照.invokeMethodAsync('パレットを開くAsync'); }
        else if (引数.key === 'F10' && 引数.shiftKey && !引数.target.closest('input,textarea,select,[contenteditable="true"]')) { const 行 = 引数.target.closest('[data-対象-id]'); if (行) { 引数.preventDefault(); 引数.stopPropagation(); 行.querySelector('[data-メニュー起点]')?.click(); } }
        else if ((引数.key === 'Enter' || 引数.key === ' ') && 引数.target.hasAttribute('data-対象-id')) { 引数.preventDefault(); 引数.target.click(); }
    };
    const 離脱 = (引数) => { if (根.dataset.未保管 === 'true') { 引数.preventDefault(); 引数.returnValue = ''; } };
    window.addEventListener('beforeunload', 離脱);
    根.addEventListener('keydown', キー);
    return { dispose() { DBドラッグ.dispose(); 列フォーカス.dispose(); ツリー.dispose(); IME.dispose(); window.removeEventListener('beforeunload', 離脱); 根.removeEventListener('keydown', キー); } };
}
export function コード位置へ移動(入力,開始,長さ){
    if(!入力?.isConnected){return;}
    入力.focus({preventScroll:true});
    入力.setSelectionRange(開始,開始+長さ);
    const 行数=入力.value.slice(0,開始).split('\n').length-1;
    const 行高=parseFloat(getComputedStyle(入力).lineHeight)||20;
    入力.scrollTop=Math.max(0,行数*行高-行高*2);
    入力.scrollIntoView({block:'nearest'});
}

export function コード表示を初期化(根, 入力, 色, 番号) {
    let 破棄済みか = false;
    let 更新予約 = 0;
    const 色本文 = 色.querySelector('code');
    const スクロール = () => {
        色本文.style.transform = `translate(${-入力.scrollLeft}px,${-入力.scrollTop}px)`;
        番号.style.transform = `translateY(${-入力.scrollTop}px)`;
    };
    const 寸法を合わせる = () => {
        更新予約 = 0;
        if (破棄済みか) return;
        if (根.classList.contains('DBコード枠')) {
            const 文書 = 根.closest('.文書領域');
            const フッター = 根.closest('.作業領域')?.querySelector('.作業フッター');
            let 下余白 = 0;
            let フッター高 = 0;
            if (文書) 下余白 = parseFloat(getComputedStyle(文書).paddingBottom) || 0;
            if (フッター) フッター高 = フッター.getBoundingClientRect().height;
            // Document-relative top keeps the editor stable while the page itself is scrolled.
            const 上端 = 根.getBoundingClientRect().top + window.scrollY;
            const 高さ = Math.max(120, Math.floor(window.innerHeight - 上端 - フッター高 - 下余白 - 2));
            根.style.setProperty('--DBコード高さ', `${高さ}px`);
        }
        根.style.setProperty('--コード番号桁', String(番号.textContent.trim().split('\n').at(-1).length));
        色.style.width = `${入力.clientWidth}px`;
        色.style.height = `${入力.clientHeight}px`;
        番号.parentElement.style.height = `${入力.clientHeight}px`;
        スクロール();
    };
    const 寸法更新を予約 = () => {
        if (!破棄済みか && !更新予約) 更新予約 = requestAnimationFrame(寸法を合わせる);
    };
    const 更新 = () => {
        const 表示文字 = 色.textContent.replace(/\r\n/g, '\n');
        根.dataset.待機中 = String(表示文字 !== 入力.value + '\n');
        寸法更新を予約();
        スクロール();
    };
    const 寸法監視 = new ResizeObserver(寸法更新を予約);
    寸法監視.observe(根);
    寸法監視.observe(入力);
    const 文書見出し = 根.closest('.文書領域')?.querySelector('.文書見出し');
    if (文書見出し) 寸法監視.observe(文書見出し);
    window.addEventListener('resize', 寸法更新を予約);
    入力.addEventListener('scroll', スクロール, { passive: true });
    入力.addEventListener('input', 更新);
    更新();
    return { update: 更新, dispose() {
        破棄済みか = true;
        if (更新予約) cancelAnimationFrame(更新予約);
        寸法監視.disconnect();
        window.removeEventListener('resize', 寸法更新を予約);
        入力.removeEventListener('scroll', スクロール);
        入力.removeEventListener('input', 更新);
    } };
}
