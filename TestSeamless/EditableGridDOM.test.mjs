import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';

const entry = process.env.SEAMLESS_TEST_PLAYWRIGHT_ENTRY;
const executablePath = process.env.SEAMLESS_TEST_CHROME_EXECUTABLE;
if (!entry || !executablePath) throw new Error('Explicit owned Playwright and Chrome paths are required.');
const require = createRequire(entry);
const { chromium } = require(entry);
const script = await readFile(new URL('../Seamless.Management.Components/wwwroot/interaction.js', import.meta.url), 'utf8');
const moduleUrl = 'data:text/javascript;base64,' + Buffer.from(script).toString('base64');
let browser;
before(async () => { browser = await chromium.launch({ executablePath, headless: true, args: ['--no-first-run', '--no-default-browser-check', '--disable-extensions'] }); });
after(async () => { await browser?.close(); });

async function grid() {
    const page = await browser.newPage();
    await page.setContent(`<button id="before">Before</button><table id="grid"><thead><tr><th></th><th>A</th><th>B</th><th>C</th></tr></thead><tbody>
      <tr><td class="行操作列"><button>Delete</button></td><td><input id="a" value="Alpha"></td><td><input id="b" value="Beta"></td><td><input id="c" value=""></td></tr>
      <tr><td class="行操作列"><button>Delete</button></td><td><input id="d" value="Delta"></td><td><input id="e" value="Echo"></td><td><input id="f" value="Foxtrot"></td></tr>
      </tbody></table><button id="after">After</button>`);
    await page.evaluate(async url => {
        window.engine = (await import(url)).編集グリッドを初期化(document.querySelector('#grid'));
        window.inputEvents = 0;
        document.querySelector('#grid').addEventListener('input', () => window.inputEvents++);
    }, moduleUrl);
    return page;
}
async function select(page, id, start = 0, end) {
    await page.locator('#' + id).focus();
    await page.locator('#' + id).evaluate((input, range) => input.setSelectionRange(range.start, range.end ?? input.value.length), { start, end });
}
async function state(page) {
    return page.evaluate(() => ({ id: document.activeElement.id, start: document.activeElement.selectionStart, end: document.activeElement.selectionEnd, value: document.activeElement.value }));
}
async function selected(page, id) {
    const current = await state(page);
    assert.equal(current.id, id); assert.equal(current.start, 0); assert.equal(current.end, current.value.length);
}

test('full selection moves across rows and columns while caret and partial selections stay native', async () => {
    const page = await grid();
    try {
        await select(page, 'a'); await page.keyboard.press('ArrowRight'); await selected(page, 'b');
        await page.keyboard.press('ArrowDown'); await selected(page, 'e');
        await page.keyboard.press('ArrowLeft'); await selected(page, 'd');
        await page.keyboard.press('ArrowUp'); await selected(page, 'a');
        await select(page, 'a', 2, 2); await page.keyboard.press('ArrowRight'); assert.deepEqual(await state(page), { id: 'a', start: 3, end: 3, value: 'Alpha' });
        await select(page, 'a', 1, 3); await page.keyboard.press('ArrowLeft'); assert.equal((await state(page)).id, 'a');
        await select(page, 'a'); await page.keyboard.press('Shift+ArrowRight'); assert.equal((await state(page)).id, 'a');
    } finally { await page.close(); }
});
test('F2 edits at the caret and Tab wraps with full selection without trapping boundary focus', async () => {
    const page = await grid();
    try {
        await select(page, 'a'); await page.keyboard.press('F2'); assert.deepEqual(await state(page), { id: 'a', start: 5, end: 5, value: 'Alpha' });
        await page.keyboard.press('ArrowLeft'); assert.equal((await state(page)).start, 4);
        await page.keyboard.press('Tab'); await selected(page, 'b');
        await page.keyboard.press('Tab'); await selected(page, 'c');
        await page.keyboard.press('Tab'); await selected(page, 'd');
        await page.keyboard.press('Shift+Tab'); await selected(page, 'c');
        await select(page, 'f'); await page.keyboard.press('Tab'); assert.equal((await state(page)).id, 'after');
        await select(page, 'a'); await page.keyboard.press('Shift+Tab'); assert.equal((await state(page)).id, 'before');
    } finally { await page.close(); }
});
test('empty cells navigate only after movement selection and pointer or F2 restores native editing', async () => {
    const page = await grid();
    try {
        await select(page, 'b'); await page.keyboard.press('ArrowRight'); await selected(page, 'c');
        await page.keyboard.press('ArrowDown'); await selected(page, 'f');
        await page.keyboard.press('ArrowUp'); await selected(page, 'c');
        await page.keyboard.press('F2'); await page.keyboard.press('ArrowDown'); assert.equal((await state(page)).id, 'c');
        await select(page, 'b'); await page.keyboard.press('Tab'); await page.locator('#c').click(); await page.keyboard.press('ArrowLeft'); assert.equal((await state(page)).id, 'c');
    } finally { await page.close(); }
});
test('Escape restores the edit and does not emit duplicate changes for an unchanged cell', async () => {
    const page = await grid();
    try {
        await page.locator('#a').fill('Changed'); await page.keyboard.press('Escape'); await selected(page, 'a'); assert.equal((await state(page)).value, 'Alpha');
        const count = await page.evaluate(() => window.inputEvents); await page.keyboard.press('Escape'); assert.equal(await page.evaluate(() => window.inputEvents), count);
    } finally { await page.close(); }
});
test('IME and expanded comboboxes keep their arrow keys and native selects remain editable', async () => {
    const page = await grid();
    try {
        await select(page, 'a'); await page.locator('#a').dispatchEvent('compositionstart'); await page.keyboard.press('ArrowRight'); assert.equal((await state(page)).id, 'a');
        await page.locator('#a').dispatchEvent('compositionend'); await select(page, 'a'); await page.keyboard.press('ArrowRight'); await selected(page, 'b');
        await page.locator('#b').evaluate(input => { input.setAttribute('role', 'combobox'); input.setAttribute('aria-expanded', 'true'); });
        await page.keyboard.press('ArrowRight'); assert.equal((await state(page)).id, 'b');
        await page.locator('#c').evaluate(input => { input.parentElement.innerHTML = '<select id="native"><option value="1">One</option><option value="2">Two</option></select>'; });
        await page.locator('#native').focus(); await page.keyboard.press('ArrowDown'); assert.equal((await state(page)).id, 'native'); assert.equal(await page.locator('#native').inputValue(), '2');
    } finally { await page.close(); }
});
test('new rows acquire the same navigation and disposal removes the key handler', async () => {
    const page = await grid();
    try {
        await page.locator('tbody').evaluate(body => body.insertAdjacentHTML('beforeend', '<tr><td class="行操作列"></td><td><input id="g" value="Golf"></td><td><input id="h" value="Hotel"></td><td><input id="i" value="India"></td></tr>'));
        await page.waitForFunction(() => document.querySelector('#g').closest('td').getAttribute('role') === 'gridcell');
        await select(page, 'd'); await page.keyboard.press('ArrowDown'); await selected(page, 'g');
        await page.evaluate(() => window.engine.dispose()); await select(page, 'a'); await page.keyboard.press('ArrowRight'); assert.equal((await state(page)).id, 'a');
    } finally { await page.close(); }
});
