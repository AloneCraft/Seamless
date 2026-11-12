export function 計画グラフを初期化(画面, 参照, 世代) {
    const 取消 = new AbortController();
    const 監視条件 = { signal: 取消.signal };
    const 図 = 画面.querySelector('svg');
    let 移動 = null, 破棄済みか = false, フレーム = 0, 通知番号 = 0;
    let 送信中か = false, 最新範囲 = null, 送信済み範囲 = '', 移動中か = false, 最新方向 = null, フォーカスID = null;
    const 現在倍率 = () => (図?.getScreenCTM()?.a || 1) * 100;
    const フォーカスを反映 = () => {
        if (!フォーカスID) return;
        const ノード = Array.from(画面.querySelectorAll('button[data-ノード-id]')).find(要素 => 要素.getAttribute('data-ノード-id') === フォーカスID);
        if (ノード) { ノード.focus({ preventScroll: true }); フォーカスID = null; }
    };
    const 範囲を送信 = async () => {
        if (破棄済みか || 送信中か || !最新範囲) return;
        const 範囲 = 最新範囲;
        最新範囲 = null;
        if (範囲.署名 === 送信済み範囲) return;
        送信中か = true;
        try {
            await 参照.invokeMethodAsync('表示範囲変更Async', 範囲.世代, ++通知番号, 範囲.左, 範囲.上, 範囲.幅, 範囲.高さ, 範囲.倍率);
            if (範囲.世代 === 世代) 送信済み範囲 = 範囲.署名;
        } catch (例外) { if (!破棄済みか) console.error('Plan viewport notification failed.', 例外); }
        finally { 送信中か = false; if (!破棄済みか && 最新範囲) void 範囲を送信(); }
    };
    const 範囲を測定 = () => {
        フレーム = 0;
        if (破棄済みか || !図 || !参照) return;
        const 変換 = 図.getScreenCTM();
        if (!変換 || !Number.isFinite(変換.a) || 変換.a <= 0) return;
        const 枠 = 画面.getBoundingClientRect();
        const 逆変換 = 変換.inverse();
        const 始点 = new DOMPoint(枠.left + 画面.clientLeft, 枠.top + 画面.clientTop).matrixTransform(逆変換);
        const 終点 = new DOMPoint(枠.left + 画面.clientLeft + 画面.clientWidth, 枠.top + 画面.clientTop + 画面.clientHeight).matrixTransform(逆変換);
        const 幅 = 終点.x - 始点.x, 高さ = 終点.y - 始点.y, 倍率 = 変換.a * 100;
        if (![始点.x, 始点.y, 幅, 高さ, 倍率].every(Number.isFinite) || 幅 <= 0 || 高さ <= 0 || 倍率 <= 0) return;
        // サーバー側の200単位の余白内では、128単位の区画で微小移動の通知をまとめる。
        const 署名 = [世代, Math.floor(始点.x / 128), Math.floor(始点.y / 128), Math.ceil(幅 / 128), Math.ceil(高さ / 128), 倍率.toFixed(4)].join('|');
        最新範囲 = { 世代, 左: 始点.x, 上: 始点.y, 幅, 高さ, 倍率, 署名 };
        void 範囲を送信();
        フォーカスを反映();
    };
    const 範囲を予約 = () => { if (!破棄済みか && !フレーム) フレーム = requestAnimationFrame(範囲を測定); };
    const 表示操作 = グラフ表示を初期化(画面.closest('.計画グラフ'), 範囲を予約);
    const 移動終了時 = 引数 => {
        if (!移動 || 移動.ポインターID !== 引数.pointerId) return;
        移動 = null;
        画面.classList.remove('計画移動中');
        if (画面.hasPointerCapture(引数.pointerId)) 画面.releasePointerCapture(引数.pointerId);
    };
    画面.addEventListener('pointerdown', 引数 => {
        if (引数.pointerType === 'touch' || 引数.button !== 0 || 引数.target.closest('button,input,select,label,a,[data-ノード-id]')) return;
        移動 = { ポインターID: 引数.pointerId, 開始X: 引数.clientX, 開始Y: 引数.clientY, 左: 画面.scrollLeft, 上: 画面.scrollTop };
        画面.setPointerCapture(引数.pointerId);
        画面.classList.add('計画移動中');
        引数.preventDefault();
    }, 監視条件);
    画面.addEventListener('pointermove', 引数 => {
        if (!移動 || 移動.ポインターID !== 引数.pointerId) return;
        画面.scrollLeft = 移動.左 + 移動.開始X - 引数.clientX;
        画面.scrollTop = 移動.上 + 移動.開始Y - 引数.clientY;
    }, 監視条件);
    画面.addEventListener('pointerup', 移動終了時, 監視条件);
    画面.addEventListener('pointercancel', 移動終了時, 監視条件);
    画面.addEventListener('lostpointercapture', 移動終了時, 監視条件);
    画面.addEventListener('scroll', 範囲を予約, { ...監視条件, passive: true });
    const 方向を送信 = async () => {
        if (破棄済みか || 移動中か || 最新方向 === null) return;
        const 方向 = 最新方向;
        最新方向 = null; 移動中か = true;
        try { await 参照.invokeMethodAsync('キーボードノード移動Async', 世代, 方向); }
        catch (例外) { if (!破棄済みか) console.error('Plan keyboard navigation failed.', 例外); }
        finally { 移動中か = false; if (!破棄済みか && 最新方向 !== null) void 方向を送信(); }
    };
    画面.addEventListener('keydown', 引数 => {
        if (引数.isComposing || 引数.keyCode === 229 || 引数.ctrlKey || 引数.metaKey || 引数.altKey || 引数.target.closest('input,select,textarea')) return;
        if (['ArrowDown', 'ArrowRight', 'ArrowUp', 'ArrowLeft'].includes(引数.key)) {
            引数.preventDefault();
            最新方向 = ['ArrowDown', 'ArrowRight'].includes(引数.key) ? 1 : -1;
            void 方向を送信();
        } else if (['Enter', ' '].includes(引数.key) && 引数.target.matches('.計画簡略ノード')) {
            引数.preventDefault(); 引数.target.dispatchEvent(new MouseEvent('click', { bubbles: true }));
        }
    }, 監視条件);
    const サイズ監視 = new ResizeObserver(範囲を予約);
    サイズ監視.observe(画面);
    if (図) サイズ監視.observe(図);
    const 描画監視 = new MutationObserver(() => { フォーカスを反映(); 範囲を予約(); });
    if (図) 描画監視.observe(図, { attributes: true, attributeFilter: ['style', 'viewBox'], childList: true, subtree: true });
    範囲を予約();
    return {
        表示倍率を取得: 現在倍率,
        設定を更新(新世代) { 世代 = 新世代; 送信済み範囲 = ''; 最新範囲 = null; 最新方向 = null; フォーカスID = null; 範囲を予約(); },
        ノードを表示(ID, X, Y, 幅, 高さ) {
            if (破棄済みか || ![X, Y, 幅, 高さ].every(Number.isFinite)) return;
            const 変換 = 図?.getScreenCTM();
            if (!変換) return;
            const 中心 = new DOMPoint(X + 幅 / 2, Y + 高さ / 2).matrixTransform(変換);
            const 枠 = 画面.getBoundingClientRect();
            画面.scrollLeft += 中心.x - 枠.left - 画面.clientWidth / 2;
            画面.scrollTop += 中心.y - 枠.top - 画面.clientHeight / 2;
            フォーカスID = ID; 画面.focus({ preventScroll: true });
            フォーカスを反映(); 範囲を予約();
        },
        dispose() {
            表示操作.dispose();
            破棄済みか = true; 取消.abort();
            if (フレーム) cancelAnimationFrame(フレーム);
            サイズ監視.disconnect(); 描画監視.disconnect(); 最新範囲 = null; 最新方向 = null;
            if (移動 && 画面.hasPointerCapture(移動.ポインターID)) 画面.releasePointerCapture(移動.ポインターID);
            移動 = null; 画面.classList.remove('計画移動中');
        }
    };
}

let 最大化中 = null;
export function グラフ表示を初期化(根, サイズ変更時 = () => {}) {
    if (!根) return { dispose() {} };
    const 開く = 根.querySelector('[data-グラフ表示="maximize"]');
    const 閉じる = 根.querySelector('[data-グラフ表示="restore"]');
    if (!開く || !閉じる) return { dispose() {} };
    const 取消 = new AbortController();
    const 文書 = 根.ownerDocument;
    const 背景 = [];
    let 表示中か = false, 元フォーカス = null, 元スクロール = null, 元overflow = '', 元role = null, 元modal = null;
    const 画面 = 根.querySelector('.計画グラフ画面');
    const 接続監視 = new MutationObserver(() => { if (表示中か && !根.isConnected) 復帰(false); });
    const フォーカス候補 = () => Array.from(根.querySelectorAll('button,input,select,textarea,a[href],[tabindex]')).filter(要素 => !要素.disabled && !要素.inert && 要素.tabIndex >= 0 && 要素.getClientRects().length > 0);
    function 復帰(フォーカス復帰か = true) {
        if (!表示中か) return;
        表示中か = false; 最大化中 = null; 接続監視.disconnect();
        根.classList.remove('グラフ最大化');
        if (元role === null) 根.removeAttribute('role'); else 根.setAttribute('role', 元role);
        if (元modal === null) 根.removeAttribute('aria-modal'); else 根.setAttribute('aria-modal', 元modal);
        for (const [要素, 値] of 背景) 要素.inert = 値;
        背景.length = 0;
        文書.body.style.overflow = 元overflow;
        開く.hidden = false; 閉じる.hidden = true; 開く.setAttribute('aria-expanded', 'false');
        if (画面 && 元スクロール) { 画面.scrollLeft = 元スクロール.left; 画面.scrollTop = 元スクロール.top; }
        if (フォーカス復帰か && 元フォーカス?.isConnected) 元フォーカス.focus({ preventScroll: true });
        サイズ変更時();
    }
    開く.addEventListener('click', () => {
        if (表示中か) return;
        最大化中?.();
        元フォーカス = 文書.activeElement; 元overflow = 文書.body.style.overflow;
        元role = 根.getAttribute('role'); 元modal = 根.getAttribute('aria-modal');
        if (画面) 元スクロール = { left: 画面.scrollLeft, top: 画面.scrollTop };
        for (let 要素 = 根; 要素.parentElement; 要素 = 要素.parentElement) {
            for (const 兄弟 of 要素.parentElement.children) {
                if (兄弟 !== 要素) { 背景.push([兄弟, 兄弟.inert]); 兄弟.inert = true; }
            }
        }
        表示中か = true; 最大化中 = 復帰;
        根.classList.add('グラフ最大化'); 根.setAttribute('role', 'dialog'); 根.setAttribute('aria-modal', 'true');
        文書.body.style.overflow = 'hidden'; 開く.hidden = true; 閉じる.hidden = false; 開く.setAttribute('aria-expanded', 'true');
        閉じる.focus({ preventScroll: true });
        接続監視.observe(文書.body, { childList: true, subtree: true });
        サイズ変更時();
    }, { signal: 取消.signal });
    閉じる.addEventListener('click', () => 復帰(), { signal: 取消.signal });
    根.addEventListener('keydown', 引数 => {
        if (!表示中か || 引数.defaultPrevented || 引数.isComposing || 引数.keyCode === 229) return;
        // 対象内のポップオーバーが開いている場合、そのEscapeを先に処理する。
        if (引数.key === 'Escape' && !根.querySelector('[role="dialog"]:not([hidden]),[role="menu"]:not([hidden])')) {
            引数.preventDefault(); 引数.stopPropagation(); 復帰();
        } else if (引数.key === 'Tab') {
            const 候補 = フォーカス候補(), 最初 = 候補[0], 最後 = 候補.at(-1);
            if (引数.shiftKey && 文書.activeElement === 最初) { 引数.preventDefault(); 最後?.focus(); }
            else if (!引数.shiftKey && 文書.activeElement === 最後) { 引数.preventDefault(); 最初?.focus(); }
        }
    }, { signal: 取消.signal });
    根.setAttribute('data-グラフ表示準備', 'true');
    return { dispose() { 復帰(); 取消.abort(); 接続監視.disconnect(); 根.removeAttribute('data-グラフ表示準備'); } };
}

export function 生成範囲を表示(入力, 開始, 長さ) {
    if (!(入力 instanceof HTMLTextAreaElement) || !Number.isSafeInteger(開始) || !Number.isSafeInteger(長さ) || 開始 < 0 || 長さ < 0) return false;
    const 色本文 = 入力.closest('.コード編集枠')?.querySelector('pre code');
    if (!色本文 || !色本文.textContent.endsWith('\n')) return false;
    // 色表示の末尾にはCodeEditorがLFを一つ追加する。textareaの位置は改行正規化後の本文に従う。
    const 原文 = 色本文.textContent.slice(0, -1);
    if (開始 > 原文.length - 長さ || 原文.replace(/\r\n?/g, '\n') !== 入力.value) return false;
    const 表示開始 = 原文.slice(0, 開始).replace(/\r\n?/g, '\n').length;
    const 表示終了 = 原文.slice(0, 開始 + 長さ).replace(/\r\n?/g, '\n').length;
    入力.focus({ preventScroll: true });
    入力.setSelectionRange(表示開始, 表示終了);
    const 行 = 入力.value.slice(0, 表示開始).split('\n').length - 1;
    const 高さ = parseFloat(getComputedStyle(入力).lineHeight) || 20;
    入力.scrollTop = Math.max(0, 行 * 高さ - 入力.clientHeight / 3);
    入力.dispatchEvent(new Event('scroll'));
    return true;
}
