import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';

const entry = process.env.SEAMLESS_TEST_PLAYWRIGHT_ENTRY;
const executablePath = process.env.SEAMLESS_TEST_CHROME_EXECUTABLE;
if (!entry || !executablePath) throw new Error('Explicit owned Playwright and Chrome paths are required.');
const { chromium } = createRequire(entry)(entry);
const source = await readFile(new URL('../Seamless.Management.Components/wwwroot/sidebar-layout.js', import.meta.url), 'utf8');
const storageSource = await readFile(new URL('../Seamless.Management.Components/wwwroot/browser-state.js', import.meta.url), 'utf8');
const css = await readFile(new URL('../Seamless.Management.Components/wwwroot/sidebar-layout.css', import.meta.url), 'utf8');
const moduleUrl = 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
const storageUrl = 'data:text/javascript;base64,' + Buffer.from(storageSource).toString('base64');
const key = 'Seamless.Manager.Sidebar.v1.' + 'a'.repeat(32) + '.b3duZXI=';
let browser;
before(async () => { browser = await chromium.launch({ executablePath, headless: true, args: ['--no-first-run', '--no-default-browser-check', '--disable-extensions'] }); });
after(async () => { await browser?.close(); });
async function fixture() {
    const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
    const html = `<style>body{margin:0}.管理ワークスペース{display:flex}.管理ワークスペース *{box-sizing:border-box}.ナビゲーション{display:flex;flex-direction:column}.作業領域{flex:1;min-width:0}${css}</style><div id="root" class="管理ワークスペース"><aside id="sidebar" class="ナビゲーション"><button id="close" data-sidebar-toggle>Close sidebar</button><input id="inside"><div style="min-height:1000px">Tree</div></aside><div id="resize" class="サイドバー幅調整" data-sidebar-resize role="separator" tabindex="0" aria-orientation="vertical"></div><main class="作業領域"><button id="toggle" data-sidebar-toggle>Toggle sidebar</button><input id="outside"></main></div>`;
    await page.route('http://sidebar-layout.invalid/**', route => route.fulfill({ contentType: 'text/html; charset=utf-8', body: html }));
    await page.goto('http://sidebar-layout.invalid/manager/one/');
    await initialize(page); return page;
}
async function initialize(page) { await page.evaluate(async url => { window.layoutModule = await import(url); window.layout = window.layoutModule.initialize(document.querySelector('#root')); }, moduleUrl); }
async function storage(page, seed = null) {
    return page.evaluate(async ({ url, key, seed }) => {
        if (seed) localStorage.setItem(key, JSON.stringify(seed));
        const module = await import(url); window.stateEngine = module.initialize(document.querySelector('#root'), key, { async invokeMethodAsync() {} });
        return window.stateEngine.read();
    }, { url: storageUrl, key, seed });
}
async function width(page) { return Number(await page.locator('#resize').getAttribute('aria-valuenow')); }
async function active(page) { return page.evaluate(() => document.activeElement.id); }
test('separator keyboard resizing is bounded and leaves text-input arrows alone', async () => {
    const page = await fixture();
    try {
        await page.locator('#resize').focus(); await page.keyboard.press('ArrowRight'); assert.equal(await width(page), 262);
        await page.keyboard.press('Shift+ArrowLeft'); assert.equal(await width(page), 230);
        await page.keyboard.press('Home'); assert.equal(await width(page), 200);
        await page.keyboard.press('End'); assert.equal(await width(page), 600);
        await page.locator('#outside').fill('abc'); await page.locator('#outside').evaluate(input => input.setSelectionRange(1, 1));
        await page.keyboard.press('ArrowRight'); assert.equal(await width(page), 600); assert.equal(await active(page), 'outside');
        assert.equal(await page.locator('#outside').evaluate(input => input.selectionStart), 2);
    } finally { await page.close(); }
});
test('pointer resizing commits on release and Escape restores the prior width', async () => {
    const page = await fixture();
    try {
        const bounds = await page.locator('#resize').boundingBox(); const x = bounds.x + bounds.width / 2;
        await page.mouse.move(x, 200); await page.mouse.down(); await page.mouse.move(x + 80, 200); assert.equal(await width(page), 334);
        await page.keyboard.press('Escape'); await page.mouse.up(); assert.equal(await width(page), 254);
        await page.mouse.move(x, 200); await page.mouse.down(); await page.mouse.move(x + 64, 200); await page.mouse.up(); assert.equal(await width(page), 318);
        assert.equal(await page.locator('#root').getAttribute('data-sidebar-resizing'), null);
    } finally { await page.close(); }
});
test('closing makes hidden content inert and the outside toggle reopens it by keyboard', async () => {
    const page = await fixture();
    try {
        await page.locator('#close').click(); assert.equal(await active(page), 'toggle');
        assert.equal(await page.locator('#sidebar').isVisible(), false); assert.equal(await page.locator('#resize').isVisible(), false);
        assert.equal(await page.locator('#sidebar').evaluate(sidebar => sidebar.inert), true);
        await page.keyboard.press('Enter'); assert.equal(await page.locator('#sidebar').isVisible(), true);
        assert.equal(await page.locator('#toggle').getAttribute('aria-expanded'), 'true'); assert.equal(await active(page), 'toggle');
    } finally { await page.close(); }
});
test('legacy state loads and new width/collapse settings survive same-origin restart without dropping database IDs', async () => {
    const page = await fixture();
    try {
        const legacy = { format: 1, openDatabaseIds: ['db:one'], systemExpanded: true, scrollTop: 0 };
        assert.deepEqual(await storage(page, legacy), legacy);
        await page.evaluate(value => window.stateEngine.save(value), legacy);
        await page.locator('#resize').focus(); await page.keyboard.press('ArrowRight'); await page.locator('#close').click();
        const saved = await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key);
        assert.equal(saved.format, 2); assert.equal(saved.sidebarWidth, 262); assert.equal(saved.sidebarCollapsed, true); assert.deepEqual(saved.openDatabaseIds, ['db:one']);
        await page.goto('http://sidebar-layout.invalid/manager/new-path/'); await initialize(page); await storage(page);
        assert.equal(await page.locator('#sidebar').isVisible(), false); assert.equal(await width(page), 262);
        await page.locator('#toggle').focus(); await page.keyboard.press('Enter'); assert.equal(await page.locator('#sidebar').isVisible(), true);
    } finally { await page.close(); }
});
test('late authenticated state does not overwrite a layout the user already changed', async () => {
    const page = await fixture();
    try {
        await page.locator('#resize').focus(); await page.keyboard.press('ArrowRight');
        const saved = { format: 2, openDatabaseIds: [], systemExpanded: false, scrollTop: 0, sidebarWidth: 500, sidebarCollapsed: true };
        await storage(page, saved); assert.equal(await width(page), 262); assert.equal(await page.locator('#sidebar').isVisible(), true);
        await page.evaluate(() => window.stateEngine.save({ format: 1, openDatabaseIds: [], systemExpanded: false, scrollTop: 0 }));
        const value = await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key); assert.equal(value.sidebarWidth, 262); assert.equal(value.sidebarCollapsed, false);
    } finally { await page.close(); }
});
test('narrow windows constrain display width without discarding the preferred width and disposal releases controls', async () => {
    const page = await fixture();
    try {
        await page.locator('#resize').focus(); await page.keyboard.press('End');
        await page.setViewportSize({ width: 500, height: 800 }); await page.waitForFunction(() => document.querySelector('#resize').getAttribute('aria-valuenow') === '340');
        assert.equal(Math.round((await page.locator('#sidebar').boundingBox()).width), 340);
        assert.equal(await page.locator('#root').getAttribute('data-sidebar-width'), '600');
        await page.setViewportSize({ width: 1200, height: 800 }); await page.waitForFunction(() => document.querySelector('#resize').getAttribute('aria-valuenow') === '600');
        await page.evaluate(() => window.layout.dispose()); await page.locator('#toggle').click(); assert.equal(await page.locator('#sidebar').isVisible(), true);
    } finally { await page.close(); }
});
