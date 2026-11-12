import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';

const entry = process.env.SEAMLESS_TEST_PLAYWRIGHT_ENTRY;
const executablePath = process.env.SEAMLESS_TEST_CHROME_EXECUTABLE;
if (!entry || !executablePath) throw new Error('Explicit owned Playwright and Chrome paths are required.');
const { chromium } = createRequire(entry)(entry);
const source = await readFile(new URL('../Seamless.Management.Components/wwwroot/browser-state.js', import.meta.url), 'utf8');
const moduleUrl = 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
const key = 'Seamless.Manager.Sidebar.v1.' + 'a'.repeat(32) + '.b3duZXI=';
let browser;
before(async () => { browser = await chromium.launch({ executablePath, headless: true, args: ['--no-first-run', '--no-default-browser-check', '--disable-extensions'] }); });
after(async () => { await browser?.close(); });
async function fixture() {
    const page = await browser.newPage();
    await page.route('http://sidebar.invalid/**', route => route.fulfill({ contentType: 'text/html; charset=utf-8', body: '<div id="root"><aside class="ナビゲーション" style="height:100px;overflow:auto"><div style="height:1000px">Sidebar</div></aside></div>' }));
    await page.goto('http://sidebar.invalid/manager/first/');
    return page;
}
async function initialize(page, storageKey = key) {
    return page.evaluate(async ({ moduleUrl, storageKey }) => {
        const module = await import(moduleUrl);
        window.failures = [];
        window.engine = module.initialize(document.querySelector('#root'), storageKey, { async invokeMethodAsync(method) { window.failures.push(method); } });
        return window.engine.read();
    }, { moduleUrl, storageKey });
}
test('same browser origin restores DB and sidebar state after reload and changed launch path', async () => {
    const page = await fixture();
    try {
        assert.equal(await initialize(page), null);
        await page.evaluate(() => {
            document.querySelector('aside').scrollTop = 160;
            window.engine.save({ format: 1, openDatabaseIds: ['db:one'], systemExpanded: true, scrollTop: 0, sourceCode: 'must not persist' });
        });
        await page.goto('http://sidebar.invalid/manager/new-random-path/');
        const state = await initialize(page);
        assert.deepEqual(state, { format: 2, openDatabaseIds: ['db:one'], systemExpanded: true, scrollTop: 160, sidebarWidth: 254, sidebarCollapsed: false });
        await page.evaluate(() => window.engine.restoreScroll());
        assert.equal(await page.locator('aside').evaluate(element => element.scrollTop), 160);
        assert.equal(await page.evaluate(key => localStorage.getItem(key).includes('sourceCode'), key), false);
    } finally { await page.close(); }
});
test('instances and authenticated users have independent browser state', async () => {
    const page = await fixture();
    try {
        await initialize(page);
        await page.evaluate(() => window.engine.save({ format: 1, openDatabaseIds: ['db:one'], systemExpanded: false, scrollTop: 0 }));
        for (const other of [key.replace('a'.repeat(32), 'b'.repeat(32)), key.replace('b3duZXI=', 'b3RoZXI=')]) {
            await page.evaluate(() => window.engine.dispose());
            assert.equal(await initialize(page, other), null);
        }
    } finally { await page.close(); }
});
test('unknown format, oversized content and name-derived object IDs are rejected without overwriting storage', async () => {
    const page = await fixture();
    try {
        for (const raw of [JSON.stringify({ format: 6 }), 'x'.repeat(65537), JSON.stringify({ format: 1, openDatabaseIds: ['dbo0.Items'], systemExpanded: false, scrollTop: 0 })]) {
            await page.evaluate(({ key, raw }) => localStorage.setItem(key, raw), { key, raw });
            await assert.rejects(initialize(page), /Invalid sidebar state|exceeds its limit/u);
            assert.equal(await page.evaluate(key => localStorage.getItem(key), key), raw);
        }
    } finally { await page.close(); }
});
function documentReference(overrides = {}) {
    return { databaseId:'db:a', databaseVersion:1, databaseArtifactHash:'A'.repeat(64), definitionId:'a', version:1, artifactHash:'A'.repeat(64), definitionHash:'B'.repeat(64), kind:'Table', objectId:'Items', schema:'', view:'構造', ...overrides };
}
test('published metadata uses format five without a source hash and preserves old source-document format four', async () => {
    const page = await fixture(); try {
        await initialize(page);
        const published = documentReference({publishedOnly:true,definitionHash:'',view:'データ'});
        await page.evaluate(doc => window.engine.save({format:1,openDatabaseIds:[],systemExpanded:false,scrollTop:0},[],{documents:[doc],selectedDocument:0}),published);
        const state = await page.evaluate(key => JSON.parse(localStorage.getItem(key)),key); assert.equal(state.format,5); assert.deepEqual(state.documents,[published]);
        await page.goto('http://sidebar.invalid/manager/published/'); assert.deepEqual((await initialize(page)).documents,[published]);
        await page.evaluate(doc => window.engine.save({format:1,openDatabaseIds:[],systemExpanded:false,scrollTop:0},[],{documents:[doc],selectedDocument:0}),documentReference());
        assert.equal((await page.evaluate(key => JSON.parse(localStorage.getItem(key)),key)).format,4);
        for (const doc of [documentReference({publishedOnly:true,definitionHash:'',view:'C#'}),documentReference({publishedOnly:true,view:'データ'})]) {
            await assert.rejects(page.evaluate(doc => window.engine.save({format:1,openDatabaseIds:[],systemExpanded:false,scrollTop:0},[],{documents:[doc],selectedDocument:0}),doc));
        }
    } finally { await page.close(); }
});
test('format four restores bounded document references and selection while stripping code, arguments and row values', async () => {
    const page = await fixture(); try {
        await initialize(page);
        await page.evaluate(doc => window.engine.save({ format:1, openDatabaseIds:['db:a'], systemExpanded:false, scrollTop:0 }, ['db:a'], { documents:[{ ...doc, sourceCode:'secret-code', arguments:['secret-argument'], rows:['secret-row'] }], selectedDocument:0, token:'secret-token' }), documentReference());
        const raw = await page.evaluate(key => localStorage.getItem(key), key); assert.doesNotMatch(raw, /secret|sourceCode|arguments|rows|token/u);
        await page.goto('http://sidebar.invalid/manager/restarted/'); const restored = await initialize(page);
        assert.equal(restored.format, 4); assert.deepEqual(restored.documents, [documentReference()]); assert.equal(restored.selectedDocument, 0);
        await page.evaluate(() => { document.querySelector('#root').dataset.sidebarWidth = '350'; document.querySelector('#root').dispatchEvent(new CustomEvent('seamless-sidebar-change')); window.engine.save({ format:1, openDatabaseIds:['db:a'], systemExpanded:true, scrollTop:0 }, ['db:a']); });
        const saved = await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key); assert.equal(saved.format, 4); assert.equal(saved.sidebarWidth,350); assert.deepEqual(saved.documents, [documentReference()]); assert.equal(saved.selectedDocument, 0);
    } finally { await page.close(); }
});
test('invalid document identity, view, hash, duplicate and selection metadata are rejected before overwriting', async () => {
    const page = await fixture(); try {
        await initialize(page);
        await page.evaluate(() => window.engine.save({ format:1, openDatabaseIds:[], systemExpanded:false, scrollTop:0 }, []));
        const before = await page.evaluate(key => localStorage.getItem(key), key);
        const cases = [ {documents:[documentReference({version:0})]}, {documents:[documentReference({artifactHash:'invalid'})]}, {documents:[documentReference({view:'実行計画'})]}, {documents:[documentReference(),documentReference()]}, {documents:[documentReference()],selectedDocument:1}, {documents:Array.from({length:33},(_,index)=>documentReference({objectId:'Item'+index}))} ];
        for (const value of cases) {
            const rejected = await page.evaluate(value => { try { window.engine.save({ format:1, openDatabaseIds:[], systemExpanded:false, scrollTop:0 }, [], value); return false; } catch { return true; } }, value);
            assert.equal(rejected,true); assert.equal(await page.evaluate(key => localStorage.getItem(key), key), before);
        }
        await page.evaluate(() => window.engine.save({ format:1, openDatabaseIds:[], systemExpanded:false, scrollTop:0 }, [], {documents:[],selectedDocument:null}));
        const saved = await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key); assert.equal(saved.format,4); assert.deepEqual(saved.documents,[]); assert.equal(saved.selectedDocument,null);
    } finally { await page.close(); }
});
test('oversized document metadata fails explicitly and retains the last stored state without dropping documents', async () => {
    const page = await fixture(); try {
        await initialize(page); await page.evaluate(() => window.engine.save({ format:1, openDatabaseIds:[], systemExpanded:false, scrollTop:0 }, []));
        const before = await page.evaluate(key => localStorage.getItem(key), key);
        await assert.rejects(page.evaluate(doc => window.engine.save({ format:1, openDatabaseIds:[], systemExpanded:false, scrollTop:0 }, [], {documents:Array.from({length:32},(_,index)=>({...doc,objectId:String(index)+'x'.repeat(4000)})),selectedDocument:0}),documentReference()),/exceeds its limit/u);
        assert.equal(await page.evaluate(key => localStorage.getItem(key), key),before);
    } finally { await page.close(); }
});
test('unavailable browser storage is reported and never replaced with a shared key', async () => {
    const page = await fixture();
    try {
        await page.evaluate(() => { Storage.prototype.getItem = () => { throw new DOMException('Blocked', 'SecurityError'); }; });
        await assert.rejects(initialize(page));
    } finally { await page.close(); }
});
test('scroll persistence reports quota failures once and disposal removes the listener', async () => {
    const page = await fixture();
    try {
        await initialize(page);
        await page.evaluate(() => {
            window.engine.save({ format: 1, openDatabaseIds: [], systemExpanded: false, scrollTop: 0 });
            Storage.prototype.setItem = () => { throw new DOMException('Full', 'QuotaExceededError'); };
            const sidebar = document.querySelector('aside');
            sidebar.dispatchEvent(new Event('scroll')); sidebar.dispatchEvent(new Event('scroll'));
        });
        assert.deepEqual(await page.evaluate(() => window.failures), ['表示状態保存失敗Async']);
        await page.evaluate(() => { window.engine.dispose(); document.querySelector('aside').dispatchEvent(new Event('scroll')); });
        assert.equal(await page.evaluate(() => window.failures.length), 1);
    } finally { await page.close(); }
});
test('format three persists display IDs including unread pages and layout changes keep that order', async () => {
    const page = await fixture();
    try {
        await initialize(page);
        await page.evaluate(() => window.engine.save({ format: 1, openDatabaseIds: ['db:a'], systemExpanded: true, scrollTop: 0 }, ['db:b', 'db:later', 'db:a']));
        await page.evaluate(() => { const root = document.querySelector('#root'); root.dataset.sidebarWidth = '320'; root.dataset.sidebarCollapsed = 'false'; root.dispatchEvent(new CustomEvent('seamless-sidebar-change')); });
        const saved = await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key);
        assert.equal(saved.format, 3); assert.equal(saved.sidebarWidth, 320); assert.deepEqual(saved.databaseOrder, ['db:b', 'db:later', 'db:a']);
        await page.goto('http://sidebar.invalid/manager/next/');
        assert.deepEqual((await initialize(page)).databaseOrder, ['db:b', 'db:later', 'db:a']);
    } finally { await page.close(); }
});
test('failed order persistence keeps the previous saved data and does not silently retry from layout or scroll', async () => {
    const page = await fixture();
    try {
        await initialize(page);
        const outcome = await page.evaluate(key => {
            const base = { format: 1, openDatabaseIds: [], systemExpanded: false, scrollTop: 0 };
            window.engine.save(base, ['db:a', 'db:b']); const before = localStorage.getItem(key); let attempts = 0, rejected = false;
            Storage.prototype.setItem = () => { attempts++; throw new DOMException('Full', 'QuotaExceededError'); };
            try { window.engine.save(base, ['db:b', 'db:a']); } catch { rejected = true; }
            document.querySelector('#root').dispatchEvent(new CustomEvent('seamless-sidebar-change')); document.querySelector('aside').dispatchEvent(new Event('scroll'));
            return { rejected, attempts, savedUnchanged: localStorage.getItem(key) === before, pendingOrder: window.engine.read().databaseOrder };
        }, key);
        assert.deepEqual(outcome, { rejected: true, attempts: 1, savedUnchanged: true, pendingOrder: ['db:b', 'db:a'] });
    } finally { await page.close(); }
});
test('display order rejects duplicate, unbounded and name-derived identifiers', async () => {
    const page = await fixture();
    try {
        await initialize(page);
        for (const order of [['table:Items'], ['db:a', 'db:a'], Array.from({ length: 257 }, (_, index) => 'db:' + index)]) {
            const invalid = await page.evaluate(order => { try { window.engine.save({ format: 1, openDatabaseIds: [], systemExpanded: false, scrollTop: 0 }, order); return false; } catch { return true; } }, order);
            assert.equal(invalid, true);
        }
    } finally { await page.close(); }
});
