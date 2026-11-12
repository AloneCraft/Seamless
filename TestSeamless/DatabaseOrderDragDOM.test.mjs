import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';

const entry = process.env.SEAMLESS_TEST_PLAYWRIGHT_ENTRY;
const executablePath = process.env.SEAMLESS_TEST_CHROME_EXECUTABLE;
if (!entry || !executablePath) throw new Error('Explicit owned Playwright and Chrome paths are required.');
const { chromium } = createRequire(entry)(entry);
const source = await readFile(new URL('../Seamless.Management.Components/wwwroot/interaction.js', import.meta.url), 'utf8');
const css = await readFile(new URL('../Seamless.Management.Components/wwwroot/db-order.css', import.meta.url), 'utf8');
const moduleUrl = 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
let browser;
before(async () => { browser = await chromium.launch({ executablePath, headless: true, args: ['--no-first-run', '--no-default-browser-check', '--disable-extensions'] }); });
after(async () => { await browser?.close(); });
async function fixture() {
    const page = await browser.newPage();
    await page.setContent(`<style>${css}.対象行{height:40px;width:300px;display:flex;align-items:center}button{width:32px;height:32px}.移動プレビュー,.挿入位置{position:fixed;pointer-events:none;background:green;z-index:9999}</style><div id="root"><div class="DB一覧">${['a','b','c'].map(id => `<div class="対象行" id="${id}" data-db-order-row="db:${id}" data-db-order-enabled="true" aria-label="DB ${id}" tabindex="0"><button id="handle-${id}" data-db-order-handle="db:${id}">…</button>${id}</div>`).join('')}<div id="schema" class="対象行" tabindex="0">Schema</div></div><div data-文書-id="tab:first" id="tab-first" class="対象行"><button data-ドラッグ-id="tab:first" id="tab-handle">Tab</button></div><div data-文書-id="tab:second" id="tab-second" class="対象行">Second</div><input id="input"></div>`);
    await page.evaluate(async url => {
        const module = await import(url); window.calls = []; window.clicks = [];
        window.engine = module.初期化(document.querySelector('#root'), { async invokeMethodAsync(...args) { window.calls.push(args); } });
        document.querySelector('#root').addEventListener('click', event => window.clicks.push(event.target.id));
    }, moduleUrl);
    return page;
}
async function point(page, selector, fraction = .5) { const rect = await page.locator(selector).boundingBox(); return { x: rect.x + rect.width / 2, y: rect.y + rect.height * fraction }; }
async function start(page, id = 'a') { const p = await point(page, '#handle-' + id); await page.mouse.move(p.x, p.y); await page.mouse.down(); }
async function move(page, selector, fraction) { const p = await point(page, selector, fraction); await page.mouse.move(p.x, p.y, { steps: 3 }); }
test('DB handle drag shows a horizontal insertion line and drops before or after without selecting', async () => {
    const page = await fixture(); try {
        await start(page); await move(page, '#c', .2);
        assert.equal(await page.locator('#root').evaluate(root => root.classList.contains('DB表示移動中')), true);
        const line = await page.locator('.挿入位置').boundingBox(); assert.equal(line.height, 3); assert.equal(line.width, 300);
        assert.equal(await page.locator('#c').evaluate(row => getComputedStyle(row).cursor), 'grabbing');
        await page.mouse.up(); await page.waitForFunction(() => window.calls.length === 1);
        assert.deepEqual(await page.evaluate(() => window.calls), [['DB表示順ドロップAsync','db:a','db:c',true]]);
        assert.deepEqual(await page.evaluate(() => window.clicks), []);
        await start(page); await move(page, '#b', .8); await page.mouse.up();
        await page.waitForFunction(() => window.calls.length === 2); assert.equal(await page.evaluate(() => window.calls[1][3]), false);
    } finally { await page.close(); }
});
test('Escape and pointer cancellation discard the move and its decorations', async () => {
    const page = await fixture(); try {
        await start(page); await move(page, '#c', .3); await page.keyboard.press('Escape'); await page.mouse.up();
        assert.deepEqual(await page.evaluate(() => window.calls), []); assert.equal(await page.locator('.挿入位置').count(), 0);
        await start(page); await move(page, '#b', .3); await page.evaluate(() => document.dispatchEvent(new PointerEvent('pointercancel', { bubbles: true }))); await page.mouse.up();
        assert.equal(await page.locator('.移動プレビュー').count(), 0); assert.deepEqual(await page.evaluate(() => window.calls), []);
    } finally { await page.close(); }
});
test('unavailable DBs and schema or tab targets are forbidden and never receive a DB drop', async () => {
    const page = await fixture(); try {
        await page.locator('#b').evaluate(row => row.dataset.dbOrderEnabled = 'false');
        for (const target of ['#b','#schema','#tab-second']) {
            await start(page); await move(page, target, .5); assert.equal(await page.locator('#root').evaluate(root => root.classList.contains('DB表示移動禁止')), true);
            assert.equal(await page.locator(target).evaluate(row => getComputedStyle(row).cursor), 'not-allowed'); await page.mouse.up();
        }
        await start(page, 'b'); await move(page, '#c', .3); await page.mouse.up();
        assert.deepEqual(await page.evaluate(() => window.calls), []);
    } finally { await page.close(); }
});
test('ordinary click and keyboard menu keep working while tab drag uses only its existing callbacks', async () => {
    const page = await fixture(); try {
        await page.locator('#handle-a').click(); assert.deepEqual(await page.evaluate(() => window.clicks), ['handle-a']);
        await page.locator('#handle-a').evaluate(button => button.setAttribute('data-メニュー起点', 'db:a'));
        await page.locator('#a').evaluate(row => row.setAttribute('data-対象-id', 'db:a'));
        await page.locator('#a').focus(); await page.keyboard.press('Shift+F10');
        assert.deepEqual(await page.evaluate(() => window.clicks), ['handle-a','handle-a']);
        assert.deepEqual(await page.evaluate(() => window.calls), []);
        const p = await point(page, '#tab-handle'); await page.mouse.move(p.x,p.y); await page.mouse.down(); await move(page, '#tab-second', .5); await page.mouse.up();
        await page.waitForFunction(() => window.calls.some(call => call[0] === 'ドロップAsync'));
        assert.deepEqual(await page.evaluate(() => window.calls.map(call => call[0])), ['ドラッグ開始Async','ドロップAsync']);
    } finally { await page.close(); }
});
test('startup inert, source removal, permission changes and disposal cannot commit a drag', async () => {
    const page = await fixture(); try {
        await page.locator('#root').evaluate(root => root.inert = true); await start(page); await move(page, '#c', .3); await page.mouse.up();
        await page.locator('#root').evaluate(root => root.inert = false); await start(page); await move(page, '#c', .3);
        await page.locator('#a').evaluate(row => row.dataset.dbOrderEnabled = 'false'); await page.mouse.up();
        await page.locator('#a').evaluate(row => row.dataset.dbOrderEnabled = 'true'); await start(page); await move(page, '#c', .3); await page.locator('#a').evaluate(row => row.remove()); await page.mouse.up();
        await start(page, 'b'); await move(page, '#c', .3); await page.evaluate(() => window.engine.dispose()); await page.mouse.up();
        assert.deepEqual(await page.evaluate(() => window.calls), []); assert.equal(await page.locator('.移動プレビュー').count(), 0);
    } finally { await page.close(); }
});
test('dragging the row text or composing in another input does not start a DB drag', async () => {
    const page = await fixture(); try {
        const p = await point(page, '#a'); await page.mouse.move(p.x,p.y); await page.mouse.down(); await move(page, '#c', .3); await page.mouse.up();
        await page.locator('#input').focus(); await page.locator('#input').evaluate(input => input.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true })));
        await page.locator('#input').press('Escape'); assert.deepEqual(await page.evaluate(() => window.calls), []);
    } finally { await page.close(); }
});
