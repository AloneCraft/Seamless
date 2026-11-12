import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';

const entry = process.env.SEAMLESS_TEST_PLAYWRIGHT_ENTRY;
const executablePath = process.env.SEAMLESS_TEST_CHROME_EXECUTABLE;
if (!entry || !executablePath) throw new Error('Explicit owned Playwright and Chrome paths are required.');
const { chromium } = createRequire(entry)(entry);
let scriptPath = new URL('../Seamless.Management.Components/wwwroot/interaction.js', import.meta.url);
if (process.env.SEAMLESS_TYPE_GRID_SOURCE) scriptPath = process.env.SEAMLESS_TYPE_GRID_SOURCE;
const script = await readFile(scriptPath, 'utf8');
const moduleUrl = 'data:text/javascript;base64,' + Buffer.from(script).toString('base64');
let browser;
before(async () => { browser = await chromium.launch({ executablePath, headless: true, args: ['--no-first-run', '--no-default-browser-check', '--disable-extensions'] }); });
after(async () => { await browser?.close(); });

async function grid() {
    const page = await browser.newPage();
    const type = (id, value) => `<div class="型入力" data-候補表示="false"><input id="${id}" role="combobox" aria-expanded="false" value="${value}"><button class="型候補ボタン" type="button" tabindex="-1">Types</button></div>`;
    await page.setContent(`<table id="grid"><thead><tr><th>Name</th><th>Type</th></tr></thead><tbody><tr><td><input id="n0" value="Id"></td><td>${type('t0', 'int')}</td></tr><tr><td><input id="n1" value="Name"></td><td>${type('t1', 'string')}</td></tr><tr><td><input id="n2" value="Other"></td><td>${type('t2', '')}</td></tr></tbody></table>${type('standalone', 'int')}`);
    await page.evaluate(async url => {
        const module = await import(url);
        window.calls = []; window.types = [];
        for (const root of document.querySelectorAll('.型入力')) {
            const input = root.querySelector('input');
            const show = value => {
                root.dataset.候補表示 = String(value); input.setAttribute('aria-expanded', String(value));
                root.querySelector('[role="listbox"]')?.remove();
                if (value) root.insertAdjacentHTML('beforeend', '<div role="listbox"><button role="option">int</button><button role="option">string</button></div>');
            };
            // The Razor bridge owns candidate state. The handlers under test are the real production JS initializers.
            const bridge = { async invokeMethodAsync(method, ...args) {
                window.calls.push({ id: input.id, method, args });
                if (method === 'キー入力Async') show(true);
                if (method === '閉じるAsync') show(false);
            } };
            window.types.push(module.型入力を初期化(root, bridge));
            root.querySelector('button').addEventListener('click', () => show(root.dataset.候補表示 !== 'true'));
            input.addEventListener('input', () => show(document.activeElement === input));
        }
        window.engine = module.編集グリッドを初期化(document.querySelector('#grid'));
    }, moduleUrl);
    return page;
}
async function select(page, id, start = 0, end) {
    await page.locator('#' + id).focus();
    await page.locator('#' + id).evaluate((input, range) => input.setSelectionRange(range.start, range.end ?? input.value.length), { start, end });
}
async function active(page) { return page.evaluate(() => ({ id: document.activeElement.id, start: document.activeElement.selectionStart, end: document.activeElement.selectionEnd })); }
async function keys(page) { return page.evaluate(() => window.calls.filter(call => call.method === 'キー入力Async')); }

test('Whole-selected type inputs move between grid cells before the completion key handler', async () => {
    const page = await grid();
    try {
        await select(page, 't1'); await page.keyboard.press('ArrowUp'); assert.equal((await active(page)).id, 't0');
        await page.keyboard.press('ArrowDown'); assert.deepEqual(await active(page), { id: 't1', start: 0, end: 6 });
        await page.keyboard.press('ArrowLeft'); assert.equal((await active(page)).id, 'n1');
        await page.keyboard.press('ArrowRight'); assert.equal((await active(page)).id, 't1');
        assert.equal((await keys(page)).length, 0);
    } finally { await page.close(); }
});

test('Automatic suggestions yield to whole-selection navigation but retain caret editing', async () => {
    const page = await grid();
    try {
        await page.locator('#t0').fill('long'); await select(page, 't0');
        assert.equal(await page.locator('#t0').getAttribute('aria-expanded'), 'true');
        await page.keyboard.press('ArrowDown'); assert.equal((await active(page)).id, 't1'); assert.equal((await keys(page)).length, 0);
        await select(page, 't1', 2, 2); await page.keyboard.press('ArrowDown');
        assert.equal((await active(page)).id, 't1'); assert.equal((await keys(page)).at(-1).args[0], 'ArrowDown');
        await select(page, 't1', 1, 3); await page.keyboard.press('ArrowUp'); assert.equal((await active(page)).id, 't1');
    } finally { await page.close(); }
});

test('Explicit AltDown, CtrlSpace and candidate-button opening keep candidate arrow navigation', async () => {
    for (const command of ['Alt+ArrowDown', 'Control+Space', 'button']) {
        const page = await grid();
        try {
            await select(page, 't0');
            if (command === 'button') await page.locator('#t0').locator('..').locator('button').click();
            else await page.keyboard.press(command);
            await select(page, 't0'); await page.keyboard.press('ArrowDown'); await page.keyboard.press('ArrowUp');
            assert.equal((await active(page)).id, 't0'); assert.equal((await keys(page)).at(-1).args[0], 'ArrowUp');
            await page.keyboard.press('Enter'); assert.equal((await keys(page)).at(-1).args[0], 'Enter');
            await page.locator('#t0').evaluate(input => {
                const root = input.closest('.型入力'); root.dataset.候補表示 = 'false'; input.setAttribute('aria-expanded', 'false'); root.querySelector('[role="listbox"]')?.remove();
            });
            await select(page, 't0'); await page.keyboard.press('ArrowDown'); assert.equal((await active(page)).id, 't1');
        } finally { await page.close(); }
    }
});

test('Empty type cells use the grid navigation marker until F2 explicitly starts editing', async () => {
    const page = await grid();
    try {
        await select(page, 't1'); await page.keyboard.press('ArrowDown'); assert.equal((await active(page)).id, 't2');
        await page.keyboard.press('ArrowUp'); assert.equal((await active(page)).id, 't1');
        await page.keyboard.press('ArrowDown'); await page.keyboard.press('F2'); await page.keyboard.press('ArrowUp');
        assert.equal((await active(page)).id, 't2'); assert.equal((await keys(page)).at(-1).args[0], 'ArrowUp');
    } finally { await page.close(); }
});

test('Composition lifetime and keyCode229 protect both completion and grid navigation', async () => {
    const page = await grid();
    try {
        await select(page, 't0'); await page.locator('#t0').dispatchEvent('compositionstart'); await page.keyboard.press('ArrowDown');
        assert.equal((await active(page)).id, 't0'); assert.equal((await keys(page)).length, 0);
        await page.locator('#t0').dispatchEvent('compositionend');
        await page.locator('#t0').evaluate(input => input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', keyCode: 229, bubbles: true, cancelable: true })));
        assert.equal((await active(page)).id, 't0'); assert.equal((await keys(page)).length, 0);
        await select(page, 't0'); await page.keyboard.press('ArrowDown'); assert.equal((await active(page)).id, 't1');
    } finally { await page.close(); }
});

test('Standalone completion and disposal retain their own keyboard ownership', async () => {
    const page = await grid();
    try {
        await select(page, 'standalone'); await page.keyboard.press('ArrowDown');
        assert.equal((await active(page)).id, 'standalone'); assert.equal((await keys(page)).length, 1);
        await page.evaluate(() => { window.engine.dispose(); for (const type of window.types) type.dispose(); window.calls = []; });
        await select(page, 't0'); await page.keyboard.press('ArrowDown'); assert.equal((await active(page)).id, 't0'); assert.equal((await keys(page)).length, 0);
    } finally { await page.close(); }
});
