import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';

const entry = process.env.SEAMLESS_TEST_PLAYWRIGHT_ENTRY;
const executablePath = process.env.SEAMLESS_TEST_CHROME_EXECUTABLE;
if (!entry || !executablePath) throw new Error('Explicit owned Playwright and Chrome paths are required.');
const { chromium } = createRequire(entry)(entry);
const source = await readFile(new URL('../Seamless.Management.Components/wwwroot/interaction.js', import.meta.url), 'utf8');
const moduleUrl = 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
let browser;
before(async () => { browser = await chromium.launch({ executablePath, headless: true, args: ['--no-first-run', '--no-default-browser-check', '--disable-extensions'] }); });
after(async () => { await browser?.close(); });
async function fixture() {
    const page = await browser.newPage();
    await page.setContent('<div id="root"><div class="DB一覧"><div id="table" class="対象行" data-対象-id="table:Items" tabindex="0"><button id="menu" data-メニュー起点="table:Items">Menu</button></div><div data-tree-children><section class="ツリー列一覧"><button class="ツリー列行" id="column" data-列名="Title">Title</button></section></div></div><input id="outside"></div>');
    await page.evaluate(async url => {
        window.module = await import(url);
        window.engine = window.module.初期化(document.querySelector('#root'), { async invokeMethodAsync() {} });
        window.insert = () => {
            const form = document.createElement('div'); form.setAttribute('data-column-editor', ''); form.className = 'ツリー列入力';
            form.innerHTML = '<input id="name" data-column-name-input value="Column"><input id="type" value="int"><button id="cancel">Cancel</button><button id="commit">Commit</button>';
            form.querySelector('#cancel').onclick = () => form.remove(); form.querySelector('#commit').onclick = () => form.remove();
            form.querySelector('#name').onkeydown = event => { if (event.key === 'Escape' || event.key === 'Enter') form.remove(); };
            document.querySelector('.ツリー列一覧').append(form);
        };
    }, moduleUrl);
    return page;
}
async function focusIs(page, id) { await page.waitForFunction(id => document.activeElement?.id === id, id); }
async function settle(page) { await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))); }
test('editing a column focuses its name once and Escape returns to that column while IME confirmation is preserved', async () => {
    const page = await fixture();
    try {
        await page.locator('#column').focus(); await page.evaluate(() => window.insert()); await focusIs(page, 'name');
        assert.deepEqual(await page.locator('#name').evaluate(input => [input.selectionStart, input.selectionEnd]), [0, 6]);
        await page.locator('#name').evaluate(input => { input.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true })); input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, isComposing: true })); input.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true })); });
        assert.equal(await page.locator('[data-column-editor]').count(), 1);
        await page.keyboard.press('Escape'); await focusIs(page, 'column');
    } finally { await page.close(); }
});
test('adding from a table returns to that table after commit', async () => {
    const page = await fixture();
    try {
        await page.locator('#table').focus(); await page.evaluate(() => window.insert()); await focusIs(page, 'name');
        await page.getByRole('button', { name: 'Commit', exact: true }).click(); await focusIs(page, 'table');
    } finally { await page.close(); }
});
test('rerenders and validation messages preserve the active caret and another input', async () => {
    const page = await fixture();
    try {
        await page.locator('#column').focus(); await page.evaluate(() => window.insert()); await focusIs(page, 'name');
        await page.locator('#name').evaluate(input => input.setSelectionRange(2, 2));
        await page.evaluate(() => document.querySelector('[data-column-editor]').append(document.createElement('p'))); await settle(page);
        assert.deepEqual(await page.locator('#name').evaluate(input => [document.activeElement.id, input.selectionStart, input.selectionEnd]), ['name', 2, 2]);
        await page.locator('#type').focus(); await page.evaluate(() => document.querySelector('[data-column-editor]').append(document.createElement('p'))); await settle(page);
        await focusIs(page, 'type');
    } finally { await page.close(); }
});
test('a different control focused before the editor arrives keeps focus', async () => {
    const page = await fixture();
    try {
        await page.locator('#column').focus(); await page.locator('#outside').focus(); await page.evaluate(() => window.insert()); await settle(page);
        await focusIs(page, 'outside');
        await page.evaluate(() => document.querySelector('[data-column-editor]').remove()); await settle(page); await focusIs(page, 'outside');
    } finally { await page.close(); }
});
test('a different control focused during an asynchronous save keeps focus when the editor closes', async () => {
    const page = await fixture();
    try {
        await page.locator('#column').focus(); await page.evaluate(() => window.insert()); await focusIs(page, 'name');
        await page.locator('#outside').focus(); await page.evaluate(() => document.querySelector('[data-column-editor]').remove()); await settle(page);
        await focusIs(page, 'outside');
    } finally { await page.close(); }
});
test('the real context menu can open a column editor and disposal removes focus observers', async () => {
    const page = await fixture();
    try {
        await page.locator('#table').focus();
        await page.evaluate(() => {
            const menu = document.createElement('div'); menu.className = '操作ポップ'; menu.innerHTML = '<input><button role="menuitem">Add column</button>'; document.querySelector('#root').append(menu);
            window.menu = window.module.メニューを開く(menu, document.querySelector('#menu'), 0, 0, false, { async invokeMethodAsync() {} });
            menu.querySelector('button').onclick = () => { menu.remove(); window.insert(); window.menu.dispose(); };
        });
        await page.getByRole('menuitem', { name: 'Add column' }).click(); await focusIs(page, 'name');
        await page.evaluate(() => window.engine.dispose());
        await page.locator('#cancel').click(); await settle(page); assert.notEqual(await page.evaluate(() => document.activeElement.id), 'table');
    } finally { await page.close(); }
});
