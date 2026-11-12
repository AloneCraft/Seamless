import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';

const entry = process.env.SEAMLESS_TEST_PLAYWRIGHT_ENTRY;
const executablePath = process.env.SEAMLESS_TEST_CHROME_EXECUTABLE;
if (!entry || !executablePath) throw new Error('Explicit owned Playwright and Chrome paths are required.');
const { chromium } = createRequire(entry)(entry);
const script = await readFile(new URL('../Seamless.Management.Components/wwwroot/execution-plan.js', import.meta.url), 'utf8');
const moduleUrl = 'data:text/javascript;base64,' + Buffer.from(script).toString('base64');
let browser;
before(async () => { browser = await chromium.launch({ executablePath, headless: true, args: ['--no-first-run', '--no-default-browser-check', '--disable-extensions'] }); });
after(async () => { await browser?.close(); });

async function select(source, start, length, override) {
    const page = await browser.newPage();
    try {
        await page.setContent('<button id="before">Before</button><div class="コード編集枠"><pre><code></code></pre><textarea style="height:80px;line-height:20px" wrap="off"></textarea></div>');
        return await page.evaluate(async ({ url, source, start, length, override }) => {
            const input = document.querySelector('textarea');
            const code = document.querySelector('code');
            input.value = source;
            code.textContent = source + '\n';
            if (override === 'mismatch') code.textContent = '// stale\n' + code.textContent;
            if (override === 'missing') code.remove();
            if (override === 'no-sentinel') code.textContent = source;
            document.querySelector('#before').focus();
            let scrollEvents = 0;
            input.addEventListener('scroll', () => scrollEvents++);
            const result = (await import(url)).生成範囲を表示(input, start, length);
            return { result, value: input.value, selected: input.value.slice(input.selectionStart, input.selectionEnd), start: input.selectionStart, end: input.selectionEnd, focused: document.activeElement === input, scrollTop: input.scrollTop, scrollEvents };
        }, { url: moduleUrl, source, start, length, override });
    } finally { await page.close(); }
}

test('CRLF original spans select and scroll to the corresponding generated code after textarea normalization', async () => {
    const source = Array.from({ length: 70 }, (_, i) => `int line${i} = ${i};`).join('\r\n') + '\r\nreturn answer;';
    const start = source.indexOf('return answer;');
    const result = await select(source, start, 'return answer;'.length);
    assert.equal(result.result, true); assert.equal(result.selected, 'return answer;');
    assert.equal(result.start, source.slice(0, start).replace(/\r\n?/g, '\n').length);
    assert.equal(result.focused, true); assert.ok(result.scrollTop > 0); assert.ok(result.scrollEvents > 0);
});

test('LF source preserves UTF16 offsets including the end boundary and zero-length caret', async () => {
    const source = 'int value = 1;\nreturn value;\n';
    const result = await select(source, 15, 13);
    assert.equal(result.result, true); assert.equal(result.start, 15); assert.equal(result.end, 28);
    const caret = await select(source, source.length, 0);
    assert.equal(caret.result, true); assert.equal(caret.start, source.length); assert.equal(caret.end, source.length);
});

test('Isolated CR and mixed newline spans normalize independently at both boundaries', async () => {
    const source = 'a\rb\r\nc\nd\r';
    const result = await select(source, source.indexOf('b'), 6);
    assert.equal(result.result, true); assert.equal(result.value, 'a\nb\nc\nd\n');
    assert.equal(result.selected, 'b\nc\nd'); assert.equal(result.start, 2); assert.equal(result.end, 7);
});

test('Surrogate pairs remain UTF16 code units while CRLF offsets are converted', async () => {
    const source = '// 😀\r\nstring text = "𠮷";\r\nreturn text;';
    const start = source.indexOf('"𠮷"');
    const result = await select(source, start, '"𠮷"'.length);
    assert.equal(result.result, true); assert.equal(result.selected, '"𠮷"');
    assert.equal(result.end - result.start, 4);
});

test('Invalid original ranges fail without stealing focus or dispatching selection scroll', async () => {
    for (const [start, length] of [[-1, 1], [0, -1], [1.5, 1], [0, 1.5], [100, 0], [2, 10], [Number.MAX_SAFE_INTEGER, 1]]) {
        const result = await select('a\r\nb', start, length);
        assert.equal(result.result, false); assert.equal(result.focused, false); assert.equal(result.scrollEvents, 0);
    }
});

test('Stale, missing or malformed highlighted source cannot make a false selection', async () => {
    for (const override of ['mismatch', 'missing', 'no-sentinel']) {
        const result = await select('return 7;', 0, 6, override);
        assert.equal(result.result, false); assert.equal(result.focused, false); assert.equal(result.scrollEvents, 0);
    }
});
