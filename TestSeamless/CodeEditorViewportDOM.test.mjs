import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';

const entry = process.env.SEAMLESS_TEST_PLAYWRIGHT_ENTRY;
const executablePath = process.env.SEAMLESS_TEST_CHROME_EXECUTABLE;
if (!entry || !executablePath) throw new Error('Explicit owned Playwright and Chrome paths are required.');
const { chromium } = createRequire(entry)(entry);
const script = await readFile(new URL('../Seamless.Management.Components/wwwroot/interaction.js', import.meta.url), 'utf8');
const css = await readFile(new URL('../Seamless.Management.Components/wwwroot/manager.css', import.meta.url), 'utf8');
const moduleUrl = 'data:text/javascript;base64,' + Buffer.from(script).toString('base64');
let browser;
before(async () => { browser = await chromium.launch({ executablePath, headless: true, args: ['--no-first-run', '--no-default-browser-check', '--disable-extensions'] }); });
after(async () => { await browser?.close(); });

async function editor({ database = true, lines = 200, rows = 18 } = {}) {
    const page = await browser.newPage({ viewport: { width: 1100, height: 900 } });
    await page.setContent(`<style>${css}</style><div class="管理ワークスペース"><main class="作業領域"><header class="上部バー">Workspace</header><section class="文書領域"><div class="文書見出し"><h1>Database</h1></div><nav class="表示切替"><button>C#</button></nav><div class="コード見出し">Database.cs</div><div id="editor" class="色付きコード" style="--コード行数:${rows}"><div class="コード行番号"><pre id="numbers"></pre></div><div class="コード編集枠"><pre id="color" class="コード色表示"><code></code></pre><textarea id="input" wrap="off" spellcheck="false"></textarea></div></div></section><footer class="作業フッター">Commands</footer></main></div>`);
    await page.evaluate(async ({ url, database, lines }) => {
        const root = document.querySelector('#editor');
        if (database) root.classList.add('DBコード枠');
        const input = document.querySelector('#input');
        input.value = Array.from({ length: lines }, (_, i) => `int value${i} = ${'1234567890'.repeat(35)};`).join('\n');
        const color = document.querySelector('#color');
        color.querySelector('code').textContent = input.value + '\n';
        const numbers = document.querySelector('#numbers');
        numbers.textContent = Array.from({ length: lines }, (_, i) => i + 1).join('\n');
        window.engine = (await import(url)).コード表示を初期化(root, input, color, numbers);
    }, { url: moduleUrl, database, lines });
    await page.waitForFunction(() => document.querySelector('#color').style.height !== '');
    return page;
}

async function dimensions(page) {
    return page.evaluate(() => {
        const input = document.querySelector('#input');
        const root = document.querySelector('#editor').getBoundingClientRect();
        const color = document.querySelector('#color').getBoundingClientRect();
        return { rootHeight: root.height, bottom: root.bottom, viewport: innerHeight, inputHeight: input.offsetHeight, clientHeight: input.clientHeight, colorHeight: color.height, footerTop: document.querySelector('.作業フッター').getBoundingClientRect().top, scrollHeight: document.documentElement.scrollHeight };
    });
}

test('Long source keeps a single bounded viewport and its horizontal scrollbar at the document bottom', async () => {
    const page = await editor();
    try {
        const size = await dimensions(page);
        assert.ok(Math.abs(size.rootHeight - size.inputHeight - 2) <= 1, JSON.stringify(size));
        assert.ok(size.bottom > size.viewport - 70 && size.bottom < size.viewport, JSON.stringify(size));
        assert.ok(size.footerTop >= size.bottom && size.scrollHeight <= size.viewport + 2, JSON.stringify(size));
        assert.equal(size.colorHeight, size.clientHeight);
    } finally { await page.close(); }
});

test('Horizontal and vertical scroll move the colored text and gutter inside unchanged clipped viewports', async () => {
    const page = await editor();
    try {
        await page.locator('#input').evaluate(input => { input.scrollLeft = 240; input.scrollTop = 500; input.dispatchEvent(new Event('scroll')); });
        const state = await page.evaluate(() => {
            const input = document.querySelector('#input');
            return { x: input.scrollLeft, y: input.scrollTop, code: document.querySelector('#color code').style.transform, number: document.querySelector('#numbers').style.transform, colorTop: document.querySelector('#color').getBoundingClientRect().top, inputTop: input.getBoundingClientRect().top, gutterHeight: document.querySelector('.コード行番号').getBoundingClientRect().height, clientHeight: input.clientHeight };
        });
        assert.ok(state.x > 0 && state.y > 0);
        assert.equal(state.code, `translate(${-state.x}px, ${-state.y}px)`);
        assert.equal(state.number, `translateY(${-state.y}px)`);
        assert.equal(state.colorTop, state.inputTop);
        assert.equal(state.gutterHeight, state.clientHeight);
    } finally { await page.close(); }
});

test('Window resizing adjusts only database editors while snippets preserve their requested rows', async () => {
    const page = await editor();
    const snippet = await editor({ database: false, lines: 200, rows: 5 });
    try {
        const initial = await dimensions(page);
        const short = await dimensions(snippet);
        assert.equal(short.inputHeight, 142);
        assert.equal(short.rootHeight, 144);
        await page.setViewportSize({ width: 700, height: 650 });
        await page.waitForFunction(height => document.querySelector('#input').offsetHeight < height - 100, initial.inputHeight);
        const resized = await dimensions(page);
        assert.ok(resized.bottom > 580 && resized.bottom < 650, JSON.stringify(resized));
        assert.equal(resized.colorHeight, resized.clientHeight);
        await snippet.setViewportSize({ width: 700, height: 650 });
        assert.equal((await dimensions(snippet)).inputHeight, 142);
    } finally { await page.close(); await snippet.close(); }
});

test('Highlight refresh preserves scrolling and disposal releases resize and scroll observation', async () => {
    const page = await editor();
    try {
        await page.evaluate(() => {
            const input = document.querySelector('#input');
            input.scrollTop = 200;
            input.dispatchEvent(new Event('scroll'));
            input.value += '\n// appended';
            window.engine.update();
        });
        assert.equal(await page.locator('#editor').getAttribute('data-待機中'), 'true');
        await page.evaluate(() => {
            document.querySelector('#color code').textContent = document.querySelector('#input').value + '\n';
            window.engine.update();
        });
        assert.equal(await page.locator('#editor').getAttribute('data-待機中'), 'false');
        assert.equal(await page.locator('#input').evaluate(input => input.scrollTop), 200);
        const before = await page.locator('#editor').getAttribute('style');
        await page.evaluate(() => window.engine.dispose());
        await page.setViewportSize({ width: 800, height: 700 });
        await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
        assert.equal(await page.locator('#editor').getAttribute('style'), before);
    } finally { await page.close(); }
});
