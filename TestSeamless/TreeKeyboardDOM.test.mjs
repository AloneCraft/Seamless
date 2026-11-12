import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';

const entry = process.env.SEAMLESS_TEST_PLAYWRIGHT_ENTRY;
const executablePath = process.env.SEAMLESS_TEST_CHROME_EXECUTABLE;
if (!entry || !executablePath) throw new Error('Explicit owned Playwright and Chrome paths are required.');
const { chromium } = createRequire(entry)(entry);
let scriptPath = new URL('../Seamless.Management.Components/wwwroot/interaction.js', import.meta.url);
if (process.env.SEAMLESS_TREE_SOURCE) scriptPath = process.env.SEAMLESS_TREE_SOURCE;
const source = await readFile(scriptPath, 'utf8');
const moduleUrl = 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
let browser;
before(async () => { browser = await chromium.launch({ executablePath, headless: true, args: ['--no-first-run', '--no-default-browser-check', '--disable-extensions'] }); });
after(async () => { await browser?.close(); });
function row(id, expanded = null) {
    let caret = '';
    if (expanded !== null) caret = `<button class="枝の開閉" aria-expanded="${expanded}">Toggle</button>`;
    let identity = id;
    if (id === 'databases') identity = 'databases';
    return `<div class="対象行" id="${id}" data-対象-id="${identity}" tabindex="0" role="group"><span><button data-メニュー起点="${identity}" id="menu-${id}">Menu</button></span><div class="対象内容">${caret}<span>${id}</span></div></div>`;
}
async function fixture() {
    const page = await browser.newPage();
    await page.setContent(`<div id="root">${row('databases')}<div class="DB一覧">${row('db1', true)}<div data-tree-children>${row('group', true)}<div data-tree-children>${row('table', true)}<div data-tree-children><section><button class="ツリー列行" id="column">Column</button><button class="ツリー列行" id="disabled-column" disabled>Disabled</button></section></div>${row('view')}</div><div>${row('schema', false)}<div data-tree-children hidden>${row('hidden-table')}</div></div></div>${row('db2', false)}<div data-tree-children hidden>${row('hidden-group')}</div></div><input id="outside"><div class="対象行" id="tab" data-対象-id="tab:one" tabindex="0">Tab</div></div>`);
    await page.evaluate(async url => {
        const module = await import(url); window.calls = []; window.selections = []; window.expansions = []; window.bubbled = [];
        window.rootEngine = module.初期化(document.querySelector('#root'), { async invokeMethodAsync(method) { window.calls.push(method); } });
        for (const row of document.querySelectorAll('.対象行')) {
            row.addEventListener('click', () => window.selections.push(row.id));
            row.addEventListener('keydown', event => window.bubbled.push(event.key));
            const caret = row.querySelector('.枝の開閉');
            caret?.addEventListener('click', event => { event.stopPropagation(); const expanded = caret.getAttribute('aria-expanded') !== 'true'; caret.setAttribute('aria-expanded', String(expanded)); const children = row.nextElementSibling; if (children?.hasAttribute('data-tree-children')) children.hidden = !expanded; window.expansions.push(row.id); });
            row.querySelector('[data-メニュー起点]')?.addEventListener('click', event => {
                event.stopPropagation(); window.calls.push('menu:' + row.id);
                const menu = document.createElement('div'); menu.innerHTML = '<input aria-label="Search"><button role="menuitem">Run</button>'; document.querySelector('#root').append(menu); window.menuElement = menu;
                window.menu = module.メニューを開く(menu, event.currentTarget, 0, 0, false, { async invokeMethodAsync(method) { window.calls.push(method); if (method === '閉じるAsync') { window.menu.dispose(); menu.remove(); } } });
                menu.querySelector('button').addEventListener('click', () => { if (window.focusNewInput) document.querySelector('#outside').focus(); menu.remove(); window.menu.dispose(); });
            });
        }
    }, moduleUrl);
    return page;
}
async function focused(page, id) { assert.equal(await page.evaluate(() => document.activeElement.id), id); }
test('up/down follow visible preorder and skip collapsed, disabled and non-tree nodes without selecting', async () => {
    const page = await fixture();
    try {
        await page.locator('#databases').focus();
        for (const id of ['db1', 'group', 'table', 'column', 'view', 'schema', 'db2']) { await page.keyboard.press('ArrowDown'); await focused(page, id); }
        await page.keyboard.press('ArrowDown'); await focused(page, 'db2');
        await page.keyboard.press('ArrowUp'); await focused(page, 'schema');
        assert.deepEqual(await page.evaluate(() => window.selections), []);
        assert.deepEqual(await page.evaluate(() => window.bubbled), []);
    } finally { await page.close(); }
});
test('left/right close, open and move to real parent/child independently from selection', async () => {
    const page = await fixture();
    try {
        await page.locator('#schema').focus(); await page.keyboard.press('ArrowRight'); await focused(page, 'schema');
        assert.equal(await page.locator('#schema button.枝の開閉').getAttribute('aria-expanded'), 'true');
        await page.keyboard.press('ArrowRight'); await focused(page, 'hidden-table');
        await page.keyboard.press('ArrowLeft'); await focused(page, 'schema');
        await page.keyboard.press('ArrowLeft'); assert.equal(await page.locator('#schema button.枝の開閉').getAttribute('aria-expanded'), 'false');
        await page.keyboard.press('ArrowLeft'); await focused(page, 'db1');
        await page.locator('#column').focus(); await page.keyboard.press('ArrowLeft'); await focused(page, 'table');
        assert.deepEqual(await page.evaluate(() => window.selections), []);
    } finally { await page.close(); }
});
test('Enter selects exactly once while native caret buttons keep their own action', async () => {
    const page = await fixture();
    try {
        await page.locator('#db1').focus(); await page.keyboard.press('Enter');
        assert.deepEqual(await page.evaluate(() => window.selections), ['db1']);
        await page.locator('#db1 button.枝の開閉').focus(); await page.keyboard.press('Enter');
        assert.deepEqual(await page.evaluate(() => window.expansions), ['db1']);
        assert.deepEqual(await page.evaluate(() => window.selections), ['db1']);
    } finally { await page.close(); }
});
test('Shift+F10 opens once and Escape restores the original row, not another object', async () => {
    const page = await fixture();
    try {
        await page.locator('#table').focus(); await page.keyboard.press('Shift+F10');
        assert.deepEqual(await page.evaluate(() => window.calls), ['menu:table']);
        await page.keyboard.press('Escape'); await focused(page, 'table');
        assert.deepEqual(await page.evaluate(() => window.selections), []);
        await page.locator('#menu-view').focus(); await page.keyboard.press('ContextMenu');
        assert.equal(await page.getByRole('textbox', { name: 'Search' }).count(), 1);
        await page.keyboard.press('Escape'); await focused(page, 'menu-view');
    } finally { await page.close(); }
});
test('menu command returns focus after DOM removal and preserves focus intentionally moved to an input', async () => {
    const page = await fixture();
    try {
        await page.locator('#table').focus(); await page.keyboard.press('Shift+F10');
        await page.getByRole('menuitem', { name: 'Run' }).click(); await focused(page, 'table');
        await page.keyboard.press('Shift+F10'); await page.evaluate(() => window.focusNewInput = true);
        await page.getByRole('menuitem', { name: 'Run' }).click(); await focused(page, 'outside');
    } finally { await page.close(); }
});
test('inline input, combobox and IME key events do not navigate, select or open tree menus', async () => {
    const page = await fixture();
    try {
        await page.evaluate(() => document.querySelector('#table .対象内容').insertAdjacentHTML('beforeend', '<span><input id="rename" value="Alpha"><input id="type" role="combobox" value="int"></span>'));
        await page.locator('#rename').focus(); await page.locator('#rename').evaluate(input => input.setSelectionRange(2, 2));
        await page.keyboard.press('ArrowLeft'); await focused(page, 'rename');
        assert.equal(await page.locator('#rename').evaluate(input => input.selectionStart), 1);
        await page.keyboard.press('Shift+F10'); assert.deepEqual(await page.evaluate(() => window.calls), []);
        await page.locator('#type').focus(); await page.keyboard.press('ArrowDown'); await focused(page, 'type');
        const prevented = await page.locator('#table').evaluate(row => {
            row.focus(); row.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true }));
            const results = [];
            for (const key of ['ArrowDown', 'ArrowLeft', 'Enter', 'F10']) { const event = new KeyboardEvent('keydown', { key, shiftKey: key === 'F10', bubbles: true, cancelable: true, isComposing: true }); row.dispatchEvent(event); results.push(event.defaultPrevented); }
            row.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true })); return results;
        });
        assert.deepEqual(prevented, [false, false, false, false]); await focused(page, 'table');
        assert.deepEqual(await page.evaluate(() => window.calls), []); assert.deepEqual(await page.evaluate(() => window.selections), []);
    } finally { await page.close(); }
});
