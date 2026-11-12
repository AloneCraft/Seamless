const maximumLength = 65536;

function validate(state) {
    if (!state || ![1, 2, 3, 4, 5].includes(state.format) || !Array.isArray(state.openDatabaseIds) || state.openDatabaseIds.length > 256 ||
        typeof state.systemExpanded !== 'boolean' || !Number.isFinite(state.scrollTop) || state.scrollTop < 0 || state.scrollTop > 10000000 ||
        state.openDatabaseIds.some(id => typeof id !== 'string' || !id.startsWith('db:') || id.length < 4 || id.length > 131 || /[\u0000-\u001f\u007f-\u009f]/u.test(id)) ||
        new Set(state.openDatabaseIds).size !== state.openDatabaseIds.length) throw new Error('Invalid sidebar state.');
    const result = { format: state.format, openDatabaseIds: [...state.openDatabaseIds], systemExpanded: state.systemExpanded, scrollTop: state.scrollTop };
    if (state.format === 1) {
        if (state.sidebarWidth !== undefined && state.sidebarWidth !== null || state.sidebarCollapsed !== undefined && state.sidebarCollapsed !== null) throw new Error('Layout fields require sidebar format 2.');
    } else {
        if (!Number.isFinite(state.sidebarWidth) || state.sidebarWidth < 200 || state.sidebarWidth > 600 || typeof state.sidebarCollapsed !== 'boolean') throw new Error('Invalid sidebar layout.');
        result.sidebarWidth = state.sidebarWidth; result.sidebarCollapsed = state.sidebarCollapsed;
    }
    if (state.format < 3) {
        if (state.databaseOrder !== undefined && state.databaseOrder !== null) throw new Error('Display order requires sidebar format 3.');
    } else {
        if (!Array.isArray(state.databaseOrder)) throw new Error('Invalid database display order.');
        validate({ format: 1, openDatabaseIds: state.databaseOrder, systemExpanded: false, scrollTop: 0 });
        result.databaseOrder = [...state.databaseOrder];
    }
    if (state.format < 4) {
        if (state.documents != null || state.selectedDocument != null) throw new Error('Documents require sidebar format 4.');
    } else {
        if (!Array.isArray(state.documents) || state.documents.length > 32 || state.selectedDocument != null && (!Number.isInteger(state.selectedDocument) || state.selectedDocument < 0 || state.selectedDocument >= state.documents.length)) throw new Error('Invalid saved document list.');
        result.documents = state.documents.map(doc => validateDocument(doc, state.format >= 5));
        result.selectedDocument = state.selectedDocument ?? null;
        const identities = result.documents.map(doc => JSON.stringify([doc.databaseId,doc.definitionId,doc.kind,doc.objectId,doc.schema]));
        if (new Set(identities).size !== identities.length) throw new Error('Duplicate saved documents.');
    }
    return result;
}

function validateDocument(doc, allowPublished) {
    if (!doc || typeof doc !== 'object') throw new Error('Invalid saved document.');
    validate({ format: 1, openDatabaseIds: [doc.databaseId], systemExpanded: false, scrollTop: 0 });
    const hash = value => typeof value === 'string' && /^[0-9A-Fa-f]{64}$/u.test(value);
    const text = (value, maximum, empty) => typeof value === 'string' && value.length <= maximum && !/[\u0000-\u001f\u007f-\u009f]/u.test(value) && (empty || value.trim().length > 0);
    if (!Number.isSafeInteger(doc.databaseVersion) || doc.databaseVersion < 1 || !Number.isSafeInteger(doc.version) || doc.version < 1 || !hash(doc.databaseArtifactHash) || !hash(doc.artifactHash) || !text(doc.definitionId,128,false) || !text(doc.objectId,4096,true) || !text(doc.schema,4096,true)) throw new Error('Invalid saved document identity.');
    if (doc.publishedOnly === true) { if (!allowPublished || doc.definitionHash !== '' || !['データ','DB情報'].includes(doc.view)) throw new Error('Invalid published metadata document.'); }
    else if (doc.publishedOnly != null && doc.publishedOnly !== false || !hash(doc.definitionHash)) throw new Error('Invalid saved definition hash.');
    let views = ['データ','構造','C#','実行計画','実行履歴'];
    if (doc.kind === 'Database' || doc.kind === 'Schema') {
        views = ['DB情報'];
        if (doc.objectId !== '' || doc.kind === 'Database' && doc.schema !== '' || doc.kind === 'Schema' && doc.schema === '') throw new Error('Invalid saved membership.');
    } else {
        if (!['Table','View','ScalarFunction','TableFunction','RegisteredFunction'].includes(doc.kind) || doc.objectId === '') throw new Error('Invalid saved object.');
        if (doc.kind === 'Table') views = ['データ','構造','C#','アクセス履歴'];
    }
    if (!views.includes(doc.view)) throw new Error('Invalid saved document view.');
    const result = { databaseId: doc.databaseId, databaseVersion: doc.databaseVersion, databaseArtifactHash: doc.databaseArtifactHash, definitionId: doc.definitionId, version: doc.version, artifactHash: doc.artifactHash, definitionHash: doc.definitionHash, kind: doc.kind, objectId: doc.objectId, schema: doc.schema, view: doc.view };
    if (doc.publishedOnly === true) result.publishedOnly = true;
    return result;
}

export function initialize(root, key, bridge) {
    if (typeof key !== 'string' || !/^Seamless\.Manager\.Sidebar\.v1\.[0-9a-f]{32}\.[A-Za-z0-9+/]+=*$/u.test(key)) throw new Error('Authenticated sidebar key is required.');
    const sidebar = root.querySelector('.ナビゲーション');
    if (!sidebar) throw new Error('Sidebar is unavailable.');
    const raw = localStorage.getItem(key);
    if (raw && raw.length > maximumLength) throw new Error('Sidebar state exceeds its limit.');
    let state = null;
    if (raw) state = validate(JSON.parse(raw));
    if (state) {
        let width = 254, collapsed = false;
        if (state.format >= 2) { width = state.sidebarWidth; collapsed = state.sidebarCollapsed; }
        root.dispatchEvent(new CustomEvent('seamless-sidebar-restore', { detail: { width, collapsed } }));
    }
    let disposed = false, restoring = state !== null, failed = false;
    const write = () => {
        const text = JSON.stringify(state);
        if (text.length > maximumLength) throw new Error('Sidebar state exceeds its limit.');
        localStorage.setItem(key, text);
    };
    const scroll = () => {
        if (disposed || restoring || !state || failed || root.dataset.sidebarCollapsed === 'true') return;
        state.scrollTop = sidebar.scrollTop;
        try { write(); }
        catch { failed = true; bridge.invokeMethodAsync('表示状態保存失敗Async').catch(() => {}); }
    };
    const currentLayout = () => {
        let width = Number(root.dataset.sidebarWidth), collapsed = root.dataset.sidebarCollapsed === 'true';
        if (!Number.isFinite(width) || width < 200 || width > 600) { width = 254; if (state?.format >= 2) { width = state.sidebarWidth; collapsed = state.sidebarCollapsed; } }
        return { sidebarWidth: width, sidebarCollapsed: collapsed };
    };
    const restoreScroll = () => {
        if (!restoring || disposed || !state || root.dataset.sidebarCollapsed === 'true') return;
        sidebar.scrollTop = state.scrollTop; restoring = false;
    };
    const layoutChanged = () => {
        if (disposed || failed || !state) return;
        state = validate({ ...state, format: Math.max(2, state.format), ...currentLayout() });
        try { write(); restoreScroll(); }
        catch { failed = true; bridge.invokeMethodAsync('表示状態保存失敗Async').catch(() => {}); }
    };
    sidebar.addEventListener('scroll', scroll, { passive: true });
    root.addEventListener('seamless-sidebar-change', layoutChanged);
    return {
        read() { return state; },
        save(value, databaseOrder = null, documentState = null) {
            if (disposed || failed) return;
            let position = sidebar.scrollTop;
            if ((restoring || root.dataset.sidebarCollapsed === 'true') && state) position = state.scrollTop;
            validate(value);
            let order = databaseOrder;
            if (order === null && state?.format >= 3) order = state.databaseOrder;
            let documents = documentState;
            if (documents === null && state?.format >= 4) documents = state;
            let format = 2;
            if (order !== null) format = 3;
            const next = { ...value, format, scrollTop: position, ...currentLayout() };
            if (order !== null) next.databaseOrder = order;
            if (documents !== null) { next.format = 4; if (documents.documents?.some(doc => doc.publishedOnly === true)) next.format = 5; next.databaseOrder = order ?? []; next.documents = documents.documents; next.selectedDocument = documents.selectedDocument; }
            state = validate(next);
            try { write(); } catch (error) { failed = true; throw error; }
        },
        restoreScroll,
        dispose() { disposed = true; sidebar.removeEventListener('scroll', scroll); root.removeEventListener('seamless-sidebar-change', layoutChanged); }
    };
}
