import { createRequire } from 'node:module';
import { readFile, mkdir, writeFile, rename, stat } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { performance } from 'node:perf_hooks';
import { createHash } from 'node:crypto';
import { browserArtifactPaths } from './QueryVisualizationPaths.mjs';

const [proofDirectory, artifactDirectory, playwrightEntry, phase = 'all'] = process.argv.slice(2);
const harnessSha256 = createHash('sha256').update(await readFile(new URL(import.meta.url))).digest('hex').toUpperCase();
const pathPolicySha256 = createHash('sha256').update(await readFile(new URL('./QueryVisualizationPaths.mjs', import.meta.url))).digest('hex').toUpperCase();
const browserPaths = browserArtifactPaths(artifactDirectory, process.env.SEAMLESS_VISUAL_BROWSER_DOWNLOAD_PATH, process.env.SEAMLESS_VISUAL_STANDALONE_HTML_URL);
const require = createRequire(playwrightEntry);
const { chromium } = require(playwrightEntry);
await mkdir(artifactDirectory, { recursive: true });
const browser = await chromium.connectOverCDP(process.env.SEAMLESS_VISUAL_CDP_URL ?? 'http://127.0.0.1:9231');
const context = browser.contexts()[0];
const page = context.pages()[0] ?? await context.newPage();
await page.setViewportSize({ width: 1500, height: 1080 });
const observations = [];
let languageSession;
const expect = (condition, name) => { if (!condition) throw new Error(name); };
async function openQuery(id = 'ItemList', kind = 'ビュー') {
    const tree = page.getByRole('navigation', { name: 'データベース', exact: true });
    const database = tree.locator('[data-対象-id^="db:"]').first();
    const databaseCaret = database.locator(':scope > .対象内容 > button.枝の開閉');
    if (await databaseCaret.getAttribute('aria-expanded') !== 'true') await databaseCaret.click();
    const contents = database.locator('xpath=following-sibling::*[1][@data-db-tree]');
    for (const schema of await contents.locator('[data-対象-id^="schema:"]').all()) {
        const caret = schema.locator(':scope > .対象内容 > button.枝の開閉');
        if (await caret.getAttribute('aria-expanded') !== 'true') await caret.click();
    }
    for (const group of await contents.locator('[data-対象-id^="group:"]:visible').all()) {
        const caret = group.locator(':scope > .対象内容 > button.枝の開閉');
        if (await caret.getAttribute('aria-expanded') !== 'true') await caret.click();
    }
    const target = tree.locator(`[data-対象-id="query:${id}"]`);
    await target.waitFor();
    await target.click();
    const active = page.locator('.対象文書:visible');
    await active.getByRole('tab', { name: 'データ', exact: true }).click();
    const result = active.getByRole('region', { name: '問い合わせの実行', exact: true });
    await result.waitFor();
    return result;
}
async function openPlan() {
    const active = page.locator('.対象文書:visible');
    await active.getByRole('tab', { name: '実行計画', exact: true }).click();
    const live = active.getByRole('region', { name: '実行計画グラフ', exact: true }).first();
    await live.waitFor({ timeout: 30000 }); return live;
}
async function waitForEventHandlers(selector, required, name) {
    const session = await context.newCDPSession(page);
    const started = performance.now();
    try {
        while (performance.now() - started < 30000) {
            const { result } = await session.send('Runtime.evaluate', { expression: `document.querySelector(${JSON.stringify(selector)})`, objectGroup: 'seamless-owned-hydration' });
            try {
                if (result.objectId) {
                    const { listeners } = await session.send('DOMDebugger.getEventListeners', { objectId: result.objectId });
                    const ready = required.every(handler => listeners.some(value => value.type === handler.type && (handler.useCapture === undefined || value.useCapture === handler.useCapture)));
                    if (ready) return { initialized: name, waitedMilliseconds: performance.now() - started, observedHandlers: required };
                }
            } finally { await session.send('Runtime.releaseObjectGroup', { objectGroup: 'seamless-owned-hydration' }); }
            // OnAfterRenderからの登録という完了条件を照会する間隔。操作の再送は行わない。
            await new Promise(resolve => setTimeout(resolve, 100));
        }
        throw new Error(`${name} handlers were not registered within 30 seconds.`);
    } finally { await session.detach(); }
}
async function waitForInteractiveWorkspace() {
    return waitForEventHandlers('.管理ワークスペース', [{ type: 'pointerdown' }, { type: 'click', useCapture: true }, { type: 'keydown' }], 'interactive-workspace');
}
async function twoPaints() { await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))); }
async function statistic(view, label) {
    return (await view.locator('.設定稼働情報 > div').filter({ has: page.locator('small').filter({ hasText: new RegExp(`^${label}$`) }) }).locator('strong').innerText()).trim();
}
async function metricSnapshot(session) {
    const [{ metrics }, dom] = await Promise.all([session.send('Performance.getMetrics'), session.send('Memory.getDOMCounters')]);
    const names = ['JSHeapUsedSize', 'JSHeapTotalSize', 'LayoutDuration', 'RecalcStyleDuration', 'TaskDuration'];
    return { ...Object.fromEntries(metrics.filter(value => names.includes(value.name)).map(value => [value.name, value.value])), dom };
}
async function selectHistoryRecord(startedAt) {
    await page.getByRole('tab', { name: '実行履歴', exact: true }).click();
    const history = page.getByRole('region', { name: 'クエリの実績履歴', exact: true });
    await history.waitFor();
    // 履歴writerの非同期確定を通常の更新操作で照会する。完了時刻を固定しない。
    await page.waitForFunction(time => {
        const region = document.querySelector('.実績履歴');
        if ([...region?.querySelectorAll('tbody button') ?? []].some(button => button.textContent.trim() === time)) return true;
        const refresh = [...region?.querySelectorAll('button') ?? []].find(button => button.textContent.includes('履歴を更新'));
        if (refresh && !refresh.disabled) refresh.click();
        return false;
    }, startedAt, { timeout: 30000, polling: 500 });
    const button = history.getByRole('button', { name: startedAt, exact: true });
    expect(await button.count() === 1, 'Unique execution start in persisted history');
    const row = button.locator('..').locator('..');
    const cells = await row.locator('td').allTextContents();
    await button.click();
    await page.getByRole('region', { name: '実行計画グラフ', exact: true }).first().waitFor();
    return cells.map(value => value.trim());
}
try {
    if (process.env.SEAMLESS_VISUAL_LANGUAGE) {
        const language = process.env.SEAMLESS_VISUAL_LANGUAGE;
        expect(['ja-JP', 'en-US'].includes(language), 'Explicit supported test language');
        languageSession = await context.newCDPSession(page);
        const userAgent = await page.evaluate(() => navigator.userAgent);
        await languageSession.send('Emulation.setUserAgentOverride', { userAgent, acceptLanguage: language });
        observations.push({ explicitBrowserLanguage: language });
    }
    if (process.env.SEAMLESS_VISUAL_RESET_PAGE === '1') {
        await page.reload({ waitUntil: 'domcontentloaded' });
        observations.push({ reloadedOwnedFixture: true });
    }
    if (phase === 'connect' || phase === 'all') {
        const deadline = Date.now() + 150000;
        let launch, ready;
        while (Date.now() < deadline) {
            try {
                if (process.env.SEAMLESS_VISUAL_LAUNCH_FILE) {
                    const file = process.env.SEAMLESS_VISUAL_LAUNCH_FILE;
                    const modified = (await stat(file)).mtimeMs;
                    expect(modified <= Date.now() && Date.now() - modified < 60000, 'Fresh CLI owner launch file');
                    launch = { url: await readFile(file, 'utf8') };
                } else {
                    launch = JSON.parse(await readFile(path.join(proofDirectory, 'private-launch.json'), 'utf8'));
                }
                ready = JSON.parse(await readFile(path.join(proofDirectory, 'ready.json'), 'utf8'));
                break;
            } catch { await new Promise(resolve => setTimeout(resolve, 250)); }
        }
        expect(launch && ready, 'Host readiness');
        if (!process.env.SEAMLESS_VISUAL_LAUNCH_FILE) expect(Date.parse(launch.expiresAt) > Date.now(), 'Launch capability lifetime');
        expect(new URL(launch.url, ready.origin).origin === new URL(ready.origin).origin, 'Owner launch targets the owned host');
        await page.goto(new URL(launch.url, ready.origin).href, { waitUntil: 'domcontentloaded' });
        await page.locator('aside').getByText(process.env.SEAMLESS_VISUAL_DATABASE ?? 'VisualPlanDemo', { exact: true }).waitFor({ timeout: 30000 });
        observations.push({ connected: true, serverPid: ready.pid, browserVersion: browser.version(), browserUserAgent: await page.evaluate(() => navigator.userAgent) });
    }
    observations.push(await waitForInteractiveWorkspace());
    if (phase === 'language') {
        const english = process.env.SEAMLESS_VISUAL_LANGUAGE === 'en-US';
        const language = await page.evaluate(() => ({ preferred: navigator.languages[0], document: document.documentElement.lang }));
        expect(language.preferred === process.env.SEAMLESS_VISUAL_LANGUAGE && language.document === (english ? 'en' : 'ja'), 'The Manager follows the browser language');
        await page.getByRole('navigation', { name: english ? 'Databases' : 'データベース', exact: true }).locator('[data-対象-id^="db:"]').first().click();
        const information = page.getByRole('region', { name: english ? 'Database information' : 'DB情報', exact: true });
        await information.waitFor();
        const version = await information.locator('dt').filter({ hasText: english ? /^Published version$/ : /^公開版$/ }).evaluate(element => element.nextElementSibling.textContent.trim());
        expect(version === 'v1', 'Published version is rendered as a value');
        expect(await information.locator('dt').filter({ hasText: english ? /^Draft$/ : /^下書き$/ }).count() === 1, 'Draft label is localized');
        expect(!(await information.innerText()).includes('v@'), 'No raw Razor expression is displayed');
        await page.screenshot({ path: path.join(artifactDirectory, 'manager-language.png'), fullPage: true });
        observations.push({ browserLanguage: language, publishedVersion: version, localizedDraft: true });
    }
    if (phase === 'profile') {
        const module = await import(pathToFileURL(path.resolve('TestSeamless/ManagerProfileDom.mjs')).href);
        observations.push({ profileHarnessSha256: createHash('sha256').update(await readFile('TestSeamless/ManagerProfileDom.mjs')).digest('hex').toUpperCase() });
        observations.push(await module.verifyNodeProfile(page, browser, { artifactDirectory }));
    }
    if (phase === 'heat') {
        const module = await import(pathToFileURL(path.resolve('TestSeamless/ManagerResourceHeatDom.mjs')).href);
        observations.push({ heatHarnessSha256: createHash('sha256').update(await readFile('TestSeamless/ManagerResourceHeatDom.mjs')).digest('hex').toUpperCase() });
        observations.push(await module.verifyResourceHeat(page, browser, { artifactDirectory, browserDownloadPath: browserPaths.downloadPath }));
    }
    if (phase === 'heat-history') {
        const module = await import(pathToFileURL(path.resolve('TestSeamless/ManagerResourceHeatDom.mjs')).href);
        observations.push(await module.verifyResourceHeatHistory(page, { artifactDirectory, snapshotPath: process.env.SEAMLESS_VISUAL_HEAT_SOURCE }));
    }
    if (phase === 'objects') {
        const module = await import(pathToFileURL(path.resolve('TestSeamless/ManagerObjectDocumentsDom.mjs')).href);
        observations.push({ objectHarnessSha256: createHash('sha256').update(await readFile('TestSeamless/ManagerObjectDocumentsDom.mjs')).digest('hex').toUpperCase() });
        observations.push(await module.verifyObjectDocuments(page, { artifactDirectory }));
    }
    if (phase === 'additions') {
        const module = await import(pathToFileURL(path.resolve('TestSeamless/ManagerContextualAddDom.mjs')).href);
        observations.push(await module.verifyManagerContextualAdditions(page, { databaseName: 'VisualPlanDemo', artifactDirectory }));
        const ime = await import(pathToFileURL(path.resolve('TestSeamless/ManagerImeDom.mjs')).href);
        const base = await page.locator('base').getAttribute('href');
        observations.push(await ime.verifyManagerIme(page, new URL('_content/Seamless.Management.Components/interaction.js', new URL(base, page.url())).href));
    }
    if (phase === 'plans' || phase === 'all') {
        const results = await openQuery('ItemList');
        await results.waitFor();
        await results.getByLabel('実行時の計測', { exact: true }).selectOption('Summary');
        await results.getByRole('button', { name: '実行', exact: true }).click();
        await results.getByRole('table', { name: '問い合わせ結果', exact: true }).waitFor({ timeout: 30000 });
        const rows = await results.locator('tbody tr').evaluateAll(elements => elements.map(row => [...row.cells].map(cell => cell.textContent.trim())));
        expect(rows.length === 2 && rows.every(row => ['1', '2'].includes(row[0])), 'Actual view cells and row count');
        observations.push({ view: 'ItemList', rows });
        await page.getByRole('tab', { name: '実行計画', exact: true }).click();
        await page.getByRole('button', { name: '実行DAG', exact: true }).click();
        const graph = page.getByRole('region', { name: '実行計画グラフ', exact: true });
        await graph.waitFor({ timeout: 30000 });
        const graphStyles = await page.evaluate(async () => {
            const link = document.querySelector('link[href*="execution-plan.css"]');
            if (!link) return { status: 0 };
            const response = await fetch(link.href);
            return { status: response.status, loaded: !!link.sheet };
        });
        expect(graphStyles.status === 200 && graphStyles.loaded, 'Graph stylesheet is served under the Manager base path');
        const nodeStyle = await graph.locator('[data-ノード-id]').first().evaluate(element => {
            const style = getComputedStyle(element);
            return { background: style.backgroundColor, border: parseFloat(style.borderTopWidth) };
        });
        expect(nodeStyle.background !== 'rgba(0, 0, 0, 0)' && nodeStyle.border > 0, 'Node boxes survive the shared button reset');
        expect(await page.locator('.対象文書:visible .計画セレクター帯 select option').count() === 1, 'One actual call-site plan');
        expect(await graph.getByRole('button', { name: '表示用Result', exact: true }).count() === 1, 'One display Result');
        await graph.getByLabel('計画の表示層', { exact: true }).selectOption('数式');
        const node = graph.locator('[data-ノード-id]').first();
        await node.click();
        await page.getByRole('region', { name: '対応コード', exact: true }).waitFor();
        expect((await page.getByLabel('計画ノードの元ソース', { exact: true }).inputValue()).length > 0, 'Exact source span');
        const zoom = graph.getByRole('button', { name: '拡大', exact: true });
        const beforeScale = await graph.locator('.計画グラフ拡大 span').innerText();
        await zoom.click();
        await page.waitForFunction(previous => document.querySelector('.計画グラフ拡大 span')?.textContent !== previous, beforeScale);
        const scale = await graph.locator('.計画グラフ拡大 span').innerText();
        await page.getByRole('button', { name: 'ノードツリー', exact: true }).click();
        await graph.waitFor({ state: 'hidden' });
        await page.getByRole('button', { name: 'DOT グラフ', exact: true }).click();
        await page.locator('.DOTコード').waitFor({ state: 'visible' });
        const modeButtons = page.locator('.計画表示切替 button');
        expect(await modeButtons.filter({ hasText: 'ノードツリー' }).getAttribute('data-選択中') === 'false', 'DOT clears the preceding node-tree selection');
        expect(await modeButtons.count() === 6, 'DAG, Sankey, Gantt, Heat, tree and DOT remain available');
        expect(await page.locator('.計画表示切替 button[data-選択中="true"]').count() === 1, 'Only the DOT display mode is selected');
        await page.getByRole('button', { name: '実行DAG', exact: true }).click();
        await graph.waitFor({ state: 'visible' });
        expect(await page.locator('.計画表示切替 button[data-選択中="true"]').count() === 1, 'Returning to the graph keeps display modes exclusive');
        expect(await graph.locator('.計画グラフ拡大 span').innerText() === scale, 'Graph view state survives card toggle');
        const downloadSession = await browser.newBrowserCDPSession();
        await downloadSession.send('Browser.setDownloadBehavior', { behavior: 'allow', downloadPath: browserPaths.downloadPath, eventsEnabled: true });
        const save = page.getByRole('region', { name: '計画の保存と比較', exact: true });
        if (await save.locator('details').first().getAttribute('open') === null) await save.locator('summary').first().click();
        const exportProofs = [];
        for (const [format, extension] of [['JSON', 'json'], ['SVG', 'svg'], ['HTML', 'html']]) {
            let guid, filename, finish;
            const completed = new Promise(resolve => { finish = resolve; });
            const onBegin = event => { if (event.suggestedFilename.endsWith(`.${extension}`)) { guid = event.guid; filename = event.suggestedFilename; } };
            const onProgress = event => { if (event.guid === guid && ['completed', 'canceled'].includes(event.state)) finish(event); };
            const timer = setTimeout(() => finish({ state: 'timeout' }), 30000);
            downloadSession.on('Browser.downloadWillBegin', onBegin);
            downloadSession.on('Browser.downloadProgress', onProgress);
            try {
                await save.getByRole('button', { name: `${format}を保存`, exact: true }).click();
                const progress = await completed;
                expect(progress.state === 'completed', `${format} download ${progress.state}`);
                const downloaded = path.join(artifactDirectory, path.basename(filename));
                const bytes = await readFile(downloaded);
                expect(bytes.length > 0 && bytes.length === progress.totalBytes, `${format} download matches its completed byte count`);
                await rename(downloaded, path.join(artifactDirectory, `plan.${extension}`));
                exportProofs.push({ format, guid, bytes: bytes.length, sha256: createHash('sha256').update(bytes).digest('hex').toUpperCase(), file: `plan.${extension}` });
            } finally {
                clearTimeout(timer); downloadSession.off('Browser.downloadWillBegin', onBegin); downloadSession.off('Browser.downloadProgress', onProgress);
            }
        }
        await downloadSession.detach();
        await save.locator('input[type=file]').setInputFiles({ name: 'plan.json', mimeType: 'application/json', buffer: await readFile(path.join(artifactDirectory, 'plan.json')) });
        await page.getByRole('table', { name: '計画の構造件数比較', exact: true }).waitFor();
        observations.push({ boundPlans: 1, displayResult: 1, sourceMapped: true, retainedScale: scale, exports: ['json', 'svg', 'html'], exportProofs, browserPaths, imported: true });
        await page.screenshot({ path: path.join(artifactDirectory, 'query-plan.png'), fullPage: true });
        const standalone = await context.newPage();
        try {
            await standalone.goto(browserPaths.standaloneHtmlUrl);
            expect(await standalone.locator('svg').count() === 1, 'Standalone HTML graph');
            await standalone.screenshot({ path: path.join(artifactDirectory, 'standalone-plan.png'), fullPage: true });
        } finally { await standalone.close(); }
    }
    if (phase === 'generated') {
        for (const [id, kind] of [['ItemList', 'ビュー'], ['dbo0.Amount', '関数'], ['dbo1.OrderTotals', 'ビュー']]) {
            const result = await openQuery(id, kind);
            const resultRowsBefore = await result.locator('tbody tr').count();
            await page.getByRole('tab', { name: '実行計画', exact: true }).click();
            const levels = page.getByLabel('構造の段階', { exact: true });
            await levels.waitFor();
            await levels.selectOption('Relational');
            const graph = page.getByRole('region', { name: '実行計画グラフ', exact: true }).first();
            await graph.waitFor();
            await levels.selectOption('Logical');
            await graph.waitFor();
            await levels.selectOption('Generated');
            const flow = page.getByRole('region', { name: '生成C#の制御フロー', exact: true }).first();
            await flow.waitFor({ timeout: 30000 });
            expect((await flow.innerText()).includes('Available'), `${id} has a bound generated CFG`);
            const blocks = Number(await flow.getAttribute('data-モデルノード数'));
            const edges = Number(await flow.getAttribute('data-モデル接続数'));
            expect(blocks > 1 && edges > 0, `${id} has actual basic blocks and successors`);
            await flow.getByRole('button', { name: '最終生成本文の表示を切り替え', exact: true }).click();
            const bodyInput = flow.getByRole('textbox', { name: '最終生成C#本文', exact: true });
            await bodyInput.waitFor();
            const body = await bodyInput.inputValue();
            const highlighted = await bodyInput.locator('..').locator('pre code').textContent();
            expect(highlighted.endsWith('\n'), 'The code editor retains its explicit display newline');
            const originalBody = highlighted.slice(0, -1);
            expect(originalBody.replace(/\r\n?/g, '\n') === body, `${id} textarea only normalizes source line endings`);
            const bodyHash = createHash('sha256').update(originalBody, 'utf8').digest('hex').toUpperCase();
            const textareaHash = createHash('sha256').update(body, 'utf8').digest('hex').toUpperCase();
            expect(originalBody.length > 0 && (await flow.innerText()).includes(bodyHash), `${id} original final source matches the displayed hash`);
            const operations = flow.locator('.生成操作');
            expect(await operations.count() > 0, `${id} exposes bound operations`);
            const mapped = flow.getByRole('region', { name: '生成操作に対応するコード', exact: true });
            await mapped.waitFor();
            const snippet = await mapped.getByRole('textbox', { name: '生成操作の対応コード', exact: true }).inputValue();
            expect(snippet.length > 0 && body.includes(snippet), `${id} operation source is part of the verified final body`);
            expect(await result.locator('tbody tr').count() === resultRowsBefore, `${id} display switches do not execute the query`);
            const save = page.getByRole('region', { name: '計画の保存と比較', exact: true });
            if (await save.locator('details').first().getAttribute('open') === null) await save.locator('summary').first().click();
            expect(await save.getByRole('button', { name: 'SVGを保存', exact: true }).isDisabled(), 'Generated SVG is explicitly unavailable');
            expect(await save.getByRole('button', { name: 'HTMLを保存', exact: true }).isDisabled(), 'Generated HTML is explicitly unavailable');
            await page.screenshot({ path: path.join(artifactDirectory, `generated-${observations.length}.png`), fullPage: true });
            observations.push({ objectId: id, blocks, edges, bodyCharacters: originalBody.length, textareaCharacters: body.length, bodyHash, textareaHash, sourceSnippetCharacters: snippet.length, resultRowsBefore, noImplicitExecution: true });
        }
    }
    if (phase === 'all') {
        const module = await import(pathToFileURL(path.resolve('TestSeamless/ManagerContextualAddDom.mjs')).href);
        observations.push(await module.verifyManagerContextualAdditions(page, { databaseName: 'VisualPlanDemo', artifactDirectory }));
        const ime = await import(pathToFileURL(path.resolve('TestSeamless/ManagerImeDom.mjs')).href);
        const base = await page.locator('base').getAttribute('href');
        observations.push(await ime.verifyManagerIme(page, new URL('_content/Seamless.Management.Components/interaction.js', new URL(base, page.url())).href));
    }
    if (phase === 'queries') {
        await page.locator('aside').getByText('VisualPlanDemo', { exact: true }).click();
        await page.getByRole('heading', { name: 'VisualPlanDemo', exact: true }).waitFor();
        for (const query of [
            { id: 'OrderTotals', kind: 'ビュー', rows: [['1', '8'], ['2', '6']] },
            { id: 'dbo0.OrderTotals', kind: 'ビュー', rows: [['1', '80'], ['2', '60']] },
            { id: 'dbo1.Find', kind: '関数', argument: ['minPrice', '250'], rows: [['2', '300']] },
            { id: 'dbo1.Amount', kind: '関数', argument: ['orderId', '10'], scalar: '600' }
        ]) {
            const results = await openQuery(query.id, query.kind);
            await results.waitFor();
            if (query.argument) await results.getByLabel(`引数 ${query.argument[0]}`, { exact: true }).fill(query.argument[1]);
            await results.getByLabel('実行時の計測', { exact: true }).selectOption('Summary');
            await results.getByRole('button', { name: '実行', exact: true }).click();
            if (query.rows) {
                const table = results.getByRole('table', { name: '問い合わせ結果', exact: true });
                await table.waitFor();
                const rows = await table.locator('tbody tr').evaluateAll(elements => elements.map(row => [...row.cells].map(cell => cell.textContent.trim())));
                rows.sort((left, right) => left[0].localeCompare(right[0]));
                expect(JSON.stringify(rows) === JSON.stringify(query.rows), `Actual values for ${query.id}`);
            } else {
                const output = results.getByLabel('戻り値', { exact: true });
                await output.waitFor();
                expect((await output.innerText()).trim() === query.scalar, `Actual scalar for ${query.id}`);
            }
            await page.getByRole('tab', { name: '実行計画', exact: true }).click();
            await page.getByRole('region', { name: '実行計画グラフ', exact: true }).waitFor();
            const methods = page.locator('.対象文書:visible .計画セレクター帯 select option');
            expect(await methods.count() === 1 && (await methods.innerText()).includes(`.${query.id.split('.').at(-1)}(`), `Bound method for ${query.id}`);
            expect(!(await page.locator('main').innerText()).includes('v@'), 'Rendered version numbers');
            await page.screenshot({ path: path.join(artifactDirectory, `${query.id}.png`), fullPage: true });
            observations.push({ id: query.id, correctResult: true, boundPlans: 1 });
        }
    }
    if (phase === 'history') {
        await page.getByRole('tab', { name: '実行履歴', exact: true }).click();
        const history = page.getByRole('region', { name: 'クエリの実績履歴', exact: true });
        const table = history.getByRole('table', { name: 'クエリの実績履歴', exact: true });
        await table.locator('tbody button').first().waitFor();
        const cells = await table.locator('tbody tr').evaluateAll(elements => elements.map(row => [...row.cells].map(cell => cell.textContent.trim())));
        expect(cells.length > 0 && cells.every(row => row[1] === '正常完了' && row[2] === 'v1'), 'Native history outcomes and versions');
        const status = await history.innerText();
        expect(!status.includes('SaveFailed') && !status.includes('ReadMemoryBudget'), 'Readable native history');
        await table.locator('tbody button').first().click();
        await page.getByRole('region', { name: '実行計画グラフ', exact: true }).waitFor();
        const identity = await page.locator('.計画メタデータ').innerText();
        expect(identity.includes('.Amount(') && identity.includes('公開版 v1'), 'Historical query identity');
        observations.push({ cells, status, historicalPlanBound: true });
        await page.screenshot({ path: path.join(artifactDirectory, 'historical-plan.png'), fullPage: true });
    }
    if (phase === 'management-read') {
        const sidebar = page.locator('aside').first();
        const system = sidebar.locator('[data-対象-id="system"] button.枝の開閉[aria-expanded]');
        if (await system.getAttribute('aria-expanded') !== 'true') await system.click();
        const usersEntry = sidebar.locator('[data-対象-id="users"]'); await usersEntry.waitFor(); await usersEntry.click();
        const users = page.getByRole('region', { name: '利用者管理', exact: true });
        await users.getByRole('heading', { name: '利用者', exact: true }).waitFor();
        const userRows = users.locator('.利用者一覧 [data-対象-id^="user:"]'); await userRows.first().waitFor();
        const userNames = await userRows.locator('strong').allTextContents();
        expect(userNames.length > 0 && await users.locator('[data-エラー="true"]').count() === 0, 'Typed user rows are readable through the normal UI');
        expect(await users.locator('form').count() === 0, 'User inspection does not start an edit');
        await users.screenshot({ path: path.join(artifactDirectory, 'typed-users.png') });
        await sidebar.locator('[data-対象-id="settings"]').click();
        const settings = page.getByRole('region', { name: 'サーバー設定', exact: true });
        await settings.getByRole('textbox', { name: '管理TCPのIPアドレス', exact: true }).waitFor();
        const values = await settings.locator('.設定稼働情報 > div').evaluateAll(elements => elements.filter(element => ['実行形態', 'プロセス', '保存版'].includes(element.querySelector('small')?.textContent.trim())).map(element => ({ label: element.querySelector('small').textContent.trim(), value: element.querySelector('strong').textContent.trim() })));
        const save = settings.getByRole('button', { name: '保存（再起動時に反映）', exact: true });
        expect(await save.isDisabled() && await settings.locator('[data-エラー="true"]').count() === 0, 'Settings are readable without dirtying or saving the form');
        const fields = { address: await settings.getByLabel('管理TCPのIPアドレス', { exact: true }).inputValue(), managerPort: await settings.getByLabel('ローカルManagerのポート', { exact: true }).inputValue() };
        await settings.screenshot({ path: path.join(artifactDirectory, 'typed-settings.png') });
        observations.push({ userNames, values, fields, settingsSaved: false, userEdited: false });
    }
    if (phase === 'settings-write' || phase === 'settings-read') {
        async function settingsView() {
            const sidebar = page.locator('aside').first();
            const system = sidebar.locator('[data-対象-id="system"] button.枝の開閉[aria-expanded]');
            if (await system.getAttribute('aria-expanded') !== 'true') await system.click();
            const entry = sidebar.locator('[data-対象-id="settings"]'); await entry.waitFor(); await entry.click();
            const view = page.getByRole('region', { name: 'サーバー設定', exact: true });
            await view.getByLabel('参照監査の保存間隔', { exact: true }).waitFor(); return view;
        }
        async function settingsSnapshot(view) {
            const values = await view.locator('form input').evaluateAll(inputs => inputs.map(input => ({ label: input.getAttribute('aria-label'), type: input.type, value: input.value, checked: input.checked, readOnly: input.readOnly, disabled: input.disabled })));
            const version = await view.locator('.設定稼働情報 > div').filter({ has: page.locator('small').filter({ hasText: /^保存版$/ }) }).locator('strong').innerText();
            return { values, version: version.trim() };
        }
        let view = await settingsView();
        const before = await settingsSnapshot(view);
        const interval = view.getByLabel('参照監査の保存間隔', { exact: true });
        if (phase === 'settings-write') {
            expect(await interval.inputValue() === '5' && before.version.startsWith('v0'), 'Only the intended initial v0 / 5-second setting may be changed');
            await writeFile(path.join(artifactDirectory, 'settings-before.json'), JSON.stringify({ operation: 'Save only ReadAuditSaveSeconds', oldValue: 5, newValue: 6, before }, null, 2));
            await interval.fill('6');
            await page.waitForFunction(() => [...document.querySelectorAll('.サーバー設定画面 button[type="submit"]')].some(button => !button.disabled));
            await view.getByRole('button', { name: '保存（再起動時に反映）', exact: true }).click();
            await page.waitForFunction(() => [...document.querySelectorAll('.サーバー設定画面 .設定稼働情報 > div')].some(element => element.querySelector('small')?.textContent.trim() === '保存版' && element.querySelector('strong')?.textContent.trim().startsWith('v1')));
            const after = await settingsSnapshot(view);
            expect(after.values.every((value, index) => value.label === '参照監査の保存間隔' || JSON.stringify(value) === JSON.stringify(before.values[index])), 'All other settings and enablement flags are retained');
            await page.reload({ waitUntil: 'domcontentloaded' }); await waitForInteractiveWorkspace(); view = await settingsView();
            const reloaded = await settingsSnapshot(view);
            expect(reloaded.version.startsWith('v1') && await view.getByLabel('参照監査の保存間隔', { exact: true }).inputValue() === '6', 'Saved v1 / 6-second value survives a real reload');
            observations.push({ before, after, reloaded, onlyReadAuditSaveSecondsChanged: true });
        } else {
            expect(before.version.startsWith('v1') && await interval.inputValue() === '6', 'Saved v1 / 6-second value survives host restart');
            expect(await view.getByRole('button', { name: '保存（再起動時に反映）', exact: true }).isDisabled(), 'Read-back keeps the settings form clean');
            observations.push({ readAfterRestart: before, settingsSaved: false });
        }
        await view.screenshot({ path: path.join(artifactDirectory, `${phase}.png`) });
    }
    if (phase === 'scale' || phase === 'viewport') {
        await openQuery();
        await openPlan();
        const save = page.getByRole('region', { name: '計画の保存と比較', exact: true });
        if (await save.locator('details').first().getAttribute('open') === null) await save.locator('summary').first().click();
        const scaleRoot = process.env.SEAMLESS_PLAN_SCALE_ROOT ?? (process.platform === 'win32' ? 'F:/Seamless/visualization-20261109-0e63c1c7/scale-r1' : '/mnt/f/Seamless/visualization-20261109-0e63c1c7/scale-r1');
        const session = await context.newCDPSession(page);
        await session.send('Performance.enable');
        try {
            for (const shape of ['SharedDag', 'Chain']) {
                const previous = save.getByRole('button', { name: '保存済み資料を閉じる', exact: true });
                if (await previous.count()) await previous.click();
                const input = path.join(scaleRoot, `synthetic-${shape}.json`);
                const bytes = await readFile(input);
                expect(bytes.length <= 2 * 1024 * 1024, 'Scale input respects the snapshot limit');
                const snapshot = JSON.parse(bytes.toString('utf8'));
                expect(snapshot.data.plan.nodes.length === 2048, 'Fixed scale input node count');
                const before = await metricSnapshot(session);
                const started = performance.now();
                await save.getByLabel('保存済み計画を開く', { exact: true }).setInputFiles({ name: path.basename(input), mimeType: 'application/json', buffer: bytes });
                const imported = save.getByRole('region', { name: '保存済みの実行計画', exact: true });
                await imported.waitFor();
                const graph = imported.getByRole('region', { name: '実行計画グラフ', exact: true });
                await graph.locator('[data-ノード-id]').nth(2047).waitFor({ state: 'attached', timeout: 60000 });
                const graphReadiness = await waitForEventHandlers('.計画保存 [aria-label="保存済みの実行計画"] .計画グラフ画面', [{ type: 'pointerdown' }, { type: 'pointermove' }, { type: 'pointerup' }, { type: 'lostpointercapture' }], 'imported-plan-graph');
                await twoPaints();
                const importToPaintMilliseconds = performance.now() - started;
                expect(await graph.getAttribute('data-モデルノード数') === '2048', 'All scale nodes remain in the logical model');
                expect(await graph.getAttribute('data-モデル接続数') === String(snapshot.data.plan.edges.length), 'All scale edges remain in the logical model');
                expect(await graph.getAttribute('data-詳細ノード数') === '0', 'Whole-graph large plans use lightweight SVG nodes');
                expect(await graph.locator('.計画簡略ノード').count() === 2048, 'Whole-graph projection shows every node once');
                const combinedEdges = await graph.locator('[data-合成接続数]').evaluateAll(paths => paths.reduce((count, element) => count + Number(element.getAttribute('data-合成接続数')), 0));
                expect(combinedEdges === snapshot.data.plan.edges.length, 'Whole-graph combined paths retain all recorded edges');
                const projectedNode = await graph.locator('[data-ノード-id]').last().evaluate(node => ({ id: node.getAttribute('data-ノード-id'), label: node.querySelector('title')?.textContent }));
                const precedingNodeId = await graph.locator('[data-ノード-id]').nth(2046).getAttribute('data-ノード-id');
                const fit = await graph.locator('svg.計画グラフ図').evaluate(svg => {
                    const box = svg.getBoundingClientRect(), view = svg.viewBox.baseVal;
                    return { percent: Math.min(box.width / view.width, box.height / view.height) * 100, viewWidth: view.width, viewHeight: view.height };
                });
                const zoomStarted = performance.now();
                await graph.getByRole('button', { name: '標準倍率100%に戻す', exact: true }).click();
                await graph.locator('.計画グラフ拡大 span').filter({ hasText: /^100%$/ }).waitFor();
                await page.waitForFunction(() => document.querySelector('.計画保存 [aria-label="保存済みの実行計画"] .計画グラフ')?.getAttribute('data-簡略表示') === 'false', null, { timeout: 30000 });
                const standard = await graph.locator('svg.計画グラフ図').evaluate(svg => ({ width: parseFloat(svg.style.width), height: parseFloat(svg.style.height) }));
                expect(standard.width === fit.viewWidth && standard.height === fit.viewHeight, '100% restores the actual SVG layout dimensions');
                const zoomMilliseconds = performance.now() - zoomStarted;
                const searchStarted = performance.now();
                const detailedAtOrigin = Number(await graph.getAttribute('data-詳細ノード数'));
                expect(detailedAtOrigin > 0 && detailedAtOrigin <= 256, 'Readable zoom renders a bounded viewport of detailed cards');
                // Search the last logical node even after the readable projection removes it from the DOM.
                await graph.getByLabel('ノードを検索', { exact: true }).fill(projectedNode.id);
                await graph.getByRole('button', { name: /^次の一致/ }).click();
                const selected = graph.locator(`button[data-ノード-id="${projectedNode.id}"][data-選択中="true"]`);
                await selected.waitFor();
                await page.waitForFunction(id => {
                    const graph = document.querySelector('.計画保存 [aria-label="保存済みの実行計画"] .計画グラフ');
                    const node = [...(graph?.querySelectorAll('button[data-ノード-id]') ?? [])].find(node => node.getAttribute('data-ノード-id') === id);
                    if (!node) return false;
                    const bounds = node.getBoundingClientRect(), viewport = node.closest('.計画グラフ画面').getBoundingClientRect();
                    return bounds.bottom > viewport.top && bounds.top < viewport.bottom && bounds.right > viewport.left && bounds.left < viewport.right;
                }, projectedNode.id, { timeout: 30000 });
                await twoPaints();
                const selectedDom = await selected.evaluate(element => {
                    const node = element.getBoundingClientRect(), viewport = element.closest('.計画グラフ画面'), bounds = viewport.getBoundingClientRect();
                    return { title: element.title, label: element.getAttribute('aria-label'), scrollTop: viewport.scrollTop, scrollLeft: viewport.scrollLeft, intersectsViewport: node.bottom > bounds.top && node.top < bounds.bottom && node.right > bounds.left && node.left < bounds.right };
                });
                expect(selectedDom.intersectsViewport, 'Search scrolls the selected scale node into view');
                expect(selectedDom.title === projectedNode.label, 'Synthetic source-free node has no source tooltip');
                const projection = await graph.evaluate(element => ({ modelNodes: Number(element.dataset.モデルノード数), modelEdges: Number(element.dataset.モデル接続数), detailedNodes: Number(element.dataset.詳細ノード数), simpleNodes: Number(element.dataset.簡略ノード数), representedEdges: Number(element.dataset.描画接続数), actualPaths: element.querySelectorAll('g.計画入力参照 > path').length }));
                expect(projection.modelNodes === 2048 && projection.detailedNodes <= 256, 'Search retains the full model and the detailed rendering bound');
                expect(await imported.locator('.軽量実測表示').count() === 0, 'Synthetic scale fixture has no executed-query statistics');
                const after = await metricSnapshot(session);
                const searchToPaintMilliseconds = performance.now() - searchStarted;
                let narrow;
                if (phase === 'viewport') {
                    await page.setViewportSize({ width: 900, height: 1000 });
                    await page.waitForFunction(() => {
                        const graph = document.querySelector('.計画保存 [aria-label="保存済みの実行計画"] .計画グラフ');
                        return graph?.getAttribute('data-簡略表示') === 'false' && Math.abs(graph.querySelector('svg.計画グラフ図').getScreenCTM().a * 100 - 100) < 0.01;
                    });
                    await selected.focus(); await selected.press('ArrowUp');
                    const preceding = graph.locator(`button[data-ノード-id="${precedingNodeId}"][data-選択中="true"]`);
                    await preceding.waitFor();
                    narrow = await graph.locator('svg.計画グラフ図').evaluate(svg => ({ percentage: svg.getScreenCTM().a * 100, innerWidth, detailedCards: svg.querySelectorAll('foreignObject [data-ノード-id]').length }));
                    expect(narrow.detailedCards > 0 && narrow.detailedCards <= 256, 'Narrow viewport retains 100% detailed rendering');
                    narrow.keyboardSelected = precedingNodeId;
                    await graph.screenshot({ path: path.join(artifactDirectory, `viewport-${shape}-narrow.png`) });
                    await page.setViewportSize({ width: 1500, height: 1080 });
                }
                observations.push({ shape, synthetic: true, nodes: 2048, edges: snapshot.data.plan.edges.length, snapshotBytes: bytes.length, inputSha256: createHash('sha256').update(bytes).digest('hex').toUpperCase(), graphReadiness, importToPaintMilliseconds, zoomMilliseconds, searchToPaintMilliseconds, fit, standard, projectedNode, selectedDom, projection, detailedAtOrigin, combinedEdges, narrow, before, after, memoryScope: 'Chromium renderer metrics without forced GC', performanceThresholdApplied: false, functionalOnlyUnderConcurrentBuild: phase === 'viewport' });
                await graph.screenshot({ path: path.join(artifactDirectory, `scale-${shape}.png`) });
                await save.getByRole('button', { name: '保存済み資料を閉じる', exact: true }).click();
                await imported.waitFor({ state: 'hidden' });
            }
        } finally { await session.send('Performance.disable'); await session.detach(); }
    }
    if (phase === 'trace-overview') {
        await page.reload({ waitUntil: 'domcontentloaded' });
        await waitForInteractiveWorkspace();
        await openQuery(); await openPlan();
        const trace = page.getByRole('region', { name: 'クエリ境界のトレース', exact: true });
        await trace.locator('[data-トレース順]').nth(19).waitFor();
        const points = await trace.locator('[data-トレース順]').evaluateAll(elements => elements.map(node => ({ id: node.getAttribute('data-トレース実行'), sequence: node.getAttribute('data-トレース順'), y: node.querySelector('circle').getAttribute('cy') })));
        expect(new Set(points.map(point => point.id)).size >= 4, 'Overview includes the four actual executions');
        const grouped = trace.locator('button[data-説明観測数]').filter({ hasText: /ほか/ }).first();
        await grouped.waitFor(); const count = Number(await grouped.getAttribute('data-説明観測数'));
        expect(count > 1, 'Dense explanations are grouped without dropping recorded points');
        await grouped.click();
        await page.waitForFunction(count => document.querySelectorAll('.クエリトレース tr[data-対応観測="true"]').length === count, count);
        await page.waitForFunction(() => document.activeElement?.matches('.クエリトレース tr[data-対応観測="true"]'));
        const first = await page.evaluate(() => document.activeElement.cells[1].textContent);
        await grouped.click();
        await page.waitForFunction(first => document.activeElement?.matches('.クエリトレース tr[data-対応観測="true"]') && document.activeElement.cells[1].textContent !== first, first);
        const after = await trace.locator('[data-トレース順]').evaluateAll(elements => elements.map(node => ({ id: node.getAttribute('data-トレース実行'), sequence: node.getAttribute('data-トレース順'), y: node.querySelector('circle').getAttribute('cy') })));
        expect(JSON.stringify(points) === JSON.stringify(after), 'Explanation selection keeps every actual timestamp coordinate unchanged');
        const status = await trace.innerText();
        expect(status.includes('今回起動の収集状況') && status.includes(`表示中の記録${points.length}件`), 'Session counters and displayed records are separately labelled');
        const sessionId = await trace.getByText('今回起動の収集状況', { exact: true }).getAttribute('title');
        expect(/^[\da-f-]{36}$/i.test(sessionId), 'Collection session identity is available as a tooltip');
        observations.push({ points, groupedCount: count, highlightedRows: count, keyboardCycle: true, coordinatesUnchanged: true, sessionId, displayRecords: points.length, noQueryExecuted: true });
        await trace.screenshot({ path: path.join(artifactDirectory, 'trace-overview.png') });
    }
    if (phase === 'trace') {
        const executions = new Set();
        for (const recordMode of ['TimeSampled', 'BoundaryEvents']) {
            for (const eventRefresh of [false, true]) {
                const results = await openQuery();
                const record = results.getByLabel('トレースの記録方式', { exact: true });
                expect(await record.inputValue() === 'None', 'A newly selected query starts without additional recording');
                await record.selectOption(recordMode);
                const reliability = results.getByLabel('トレースの完全性', { exact: true });
                await reliability.waitFor(); // サーバーの条件付き描画が返るまで待つ。
                await page.waitForFunction(mode => {
                    const root = document.querySelector('.問い合わせ結果 .トレース設定');
                    const option = root?.querySelector('select[aria-label="トレースの完全性"] option[value="Complete"]');
                    return root?.querySelector('select[aria-label="トレースの記録方式"]')?.value === mode && option && option.disabled === (mode === 'TimeSampled');
                }, recordMode, { timeout: 30000 });
                const configuredReliability = recordMode === 'BoundaryEvents' && eventRefresh ? 'Complete' : 'BestEffort';
                await reliability.selectOption(configuredReliability);
                if (configuredReliability === 'Complete') await results.getByRole('status').filter({ hasText: '記録容量が空くまでクエリを待たせます。' }).waitFor();
                if (recordMode === 'TimeSampled') expect(await reliability.locator('option[value="Complete"]').getAttribute('disabled') !== null, 'Unsupported sampled-complete combination is disabled');
                expect(await results.getByRole('table', { name: '問い合わせ結果', exact: true }).count() === 0, 'Changing recording options did not produce a result');
                expect(await results.getByRole('region', { name: '実行の軽量実測', exact: true }).count() === 0, 'Changing recording options did not execute a measured query');
                expect(await results.getByRole('button', { name: '実行を取り消す', exact: true }).count() === 0, 'Changing recording options did not start a pending query');
                await results.getByLabel('実行時の計測', { exact: true }).selectOption('Summary');
                const started = performance.now();
                await results.getByRole('button', { name: '実行', exact: true }).click();
                const table = results.getByRole('table', { name: '問い合わせ結果', exact: true });
                await table.waitFor({ timeout: 30000 });
                const rows = await table.locator('tbody tr').evaluateAll(elements => elements.map(row => [...row.cells].map(cell => cell.textContent.trim())));
                expect(rows.length === 2 && rows.map(row => row[0]).sort().join(',') === '1,2', 'Traced ItemList preserves its actual result');
                const statistics = results.getByRole('region', { name: '実行の軽量実測', exact: true });
                await statistics.waitFor();
                const executionId = await statistic(statistics, '実行ID');
                const startedAt = await statistic(statistics, '開始UTC');
                expect(/^[\da-f-]{36}$/i.test(executionId) && !executions.has(executionId), 'Distinct actual trace execution identity');
                executions.add(executionId);
                const resultMilliseconds = performance.now() - started;
                const graph = await openPlan();
                const trace = page.getByRole('region', { name: 'クエリ境界のトレース', exact: true });
                await trace.waitFor();
                const handle = await trace.elementHandle();
                await handle.evaluate(element => {
                    const state = { observations: [] };
                    state.capture = () => {
                        if (state.observations.length >= 64) return;
                        state.observations.push({ at: performance.now(), status: [...element.querySelectorAll('p.小案内')].map(node => node.textContent.trim()).join('\n'), sequences: [...element.querySelectorAll('[data-トレース順]')].map(node => node.getAttribute('data-トレース順')), terminals: element.querySelectorAll('.トレース完了').length });
                    };
                    state.observer = new MutationObserver(state.capture);
                    state.observer.observe(element, { subtree: true, childList: true, characterData: true });
                    element.__seamlessTraceProbe = state; state.capture();
                });
                let traceRows, traceStatus, updates, tooltip, fullSourceLength, timeCoordinates, coordinatesUnchanged, summarySnapshot;
                try {
                    await trace.getByLabel('トレース表示の更新方式', { exact: true }).selectOption(eventRefresh ? 'True' : 'False');
                    await trace.locator(`[data-トレース実行="${executionId}"] circle.トレース完了`).waitFor({ timeout: 30000 });
                    expect(await trace.locator('[data-トレース範囲="QueryBoundariesAndSamples"]').count() === 1, 'Trace explicitly identifies whole-query observation scope');
                    const identifiers = await trace.locator('[data-トレース実行]').evaluateAll(elements => [...new Set(elements.map(element => element.getAttribute('data-トレース実行')))]);
                    expect(identifiers.length === 1 && identifiers[0] === executionId, 'Trace is bound to the executed query identity');
                    timeCoordinates = await trace.locator('[data-トレース順]').evaluateAll(nodes => nodes.map(node => [node.getAttribute('data-トレース順'), node.querySelector('circle').getAttribute('cy')]));
                    await trace.getByLabel('トレースの座標方式', { exact: true }).selectOption('True');
                    await trace.locator('text.トレース時刻').first().filter({ hasText: /^#/ }).waitFor();
                    await trace.getByLabel('トレースの座標方式', { exact: true }).selectOption('False');
                    await trace.locator('text.トレース時刻').first().filter({ hasText: /UTC$/ }).waitFor();
                    const restoredCoordinates = await trace.locator('[data-トレース順]').evaluateAll(nodes => nodes.map(node => [node.getAttribute('data-トレース順'), node.querySelector('circle').getAttribute('cy')]));
                    coordinatesUnchanged = JSON.stringify(timeCoordinates) === JSON.stringify(restoredCoordinates);
                    expect(coordinatesUnchanged, 'Coordinate mode round-trip retains the actual timestamp positions');
                    const detail = trace.locator('details');
                    if (await detail.getAttribute('open') === null) await detail.locator('summary').click();
                    const traceTableRows = trace.getByRole('table', { name: 'トレースの記録一覧', exact: true }).locator('tbody tr');
                    await traceTableRows.first().waitFor();
                    const expectedMode = recordMode === 'TimeSampled' ? '時間サンプリング' : '境界イベント';
                    const consistent = await page.waitForFunction(({ id, mode }) => {
                        const trace = document.querySelector('.クエリトレース');
                        const rows = [...(trace?.querySelectorAll('tbody tr') ?? [])].map(row => [...row.cells].map(cell => cell.textContent.trim()));
                        const completed = [...(trace?.querySelectorAll('[data-トレース実行]') ?? [])].some(node => node.getAttribute('data-トレース実行') === id && node.querySelector('circle.トレース完了'));
                        const summary = document.querySelector('[aria-label="実行の軽量実測"]');
                        const value = label => [...(summary?.querySelectorAll('.設定稼働情報 small') ?? [])].find(node => node.textContent.trim() === label)?.parentElement.querySelector('strong')?.textContent.trim();
                        const values = { executionId: value('実行ID'), observed: Number(value('観測出力数')), returned: Number(value('返却出力数')), elapsed: value('経過時間') };
                        if (!completed || values.executionId !== id || values.observed !== 2 || values.returned !== 2 || rows.length === 0 || !rows.every(row => row[0] === id && row[4] === mode)) return false;
                        return { rows, summary: values };
                    }, { id: executionId, mode: expectedMode }, { timeout: 30000 });
                    const snapshot = await consistent.jsonValue(); await consistent.dispose();
                    traceRows = snapshot.rows; summarySnapshot = snapshot.summary;
                    await writeFile(path.join(artifactDirectory, `trace-table-${recordMode}-${eventRefresh ? 'event' : 'time'}.json`), JSON.stringify({ executionId, expectedMode, traceRows, summarySnapshot }, null, 2));
                    expect(traceRows.length >= 2 && traceRows.every(row => row[0] === executionId && row[4] === expectedMode), 'Saved trace frames retain the selected recording mode and execution ID');
                    expect(traceRows.some(row => row[5] === '観測開始') && traceRows.some(row => row[5] === '観測終了'), 'Actual start and completion boundaries are recorded');
                    expect(traceRows.every((row, index) => index === 0 || Number(row[1]) > Number(traceRows[index - 1][1])), 'Trace sequence increases within the execution');
                    expect(traceRows.at(-1)[7] === 'v1' && !traceRows.some(row => row[7].includes('v@')), 'Trace version is rendered from actual identity');
                    traceStatus = await trace.innerText();
                    expect(!traceStatus.includes('SaveFailed') && !traceStatus.includes('FrameHashMismatch'), 'Trace storage has no failure diagnostic');
                    await graph.getByLabel('計画の表示層', { exact: true }).selectOption('元のクエリ');
                    await page.waitForFunction(() => [...document.querySelectorAll('.実行計画領域 > div > .計画グラフ [data-ノード-id]')].some(node => node.title.includes('\n')), null, { timeout: 30000 });
                    const mapped = await graph.locator('[data-ノード-id]').evaluateAll(elements => elements.map(node => ({ id: node.getAttribute('data-ノード-id'), title: node.title, label: node.querySelector('strong')?.textContent ?? '' })).find(node => node.title.includes('\n')));
                    // IDで対象を確定し、hover自体では実行や取得を開始しない。
                    const sourceNode = graph.locator(`[data-ノード-id="${mapped.id}"]`);
                    await sourceNode.hover(); tooltip = await sourceNode.getAttribute('title');
                    expect(tooltip === mapped.title, 'The source tooltip is already available before hover');
                    await sourceNode.click();
                    const code = page.getByLabel('計画ノードの元ソース', { exact: true });
                    await code.waitFor({ state: 'attached' });
                    const full = (await code.inputValue()).replace(/\r\n/g, '\n'); fullSourceLength = full.length;
                    const preview = tooltip.replace(/\r\n/g, '\n').slice(mapped.label.replace(/\r\n/g, '\n').length + 1).replace(/\n全文はノードを選択して確認できます。$/, '');
                    expect(preview.length > 0 && preview.length <= 512 && full.startsWith(preview), 'Tooltip matches the authorized full source span');
                    const finalIdentifiers = await trace.locator('[data-トレース実行]').evaluateAll(elements => [...new Set(elements.map(element => element.getAttribute('data-トレース実行')))]);
                    expect(finalIdentifiers.length === 1 && finalIdentifiers[0] === executionId, 'Display and source controls retain the current execution identity');
                    updates = await handle.evaluate(element => element.__seamlessTraceProbe.observations);
                    await trace.screenshot({ path: path.join(artifactDirectory, `trace-${recordMode}-${eventRefresh ? 'event' : 'time'}.png`) });
                    await trace.getByLabel('トレースの座標方式', { exact: true }).selectOption('True');
                    await trace.locator('text.トレース時刻').first().filter({ hasText: /^#/ }).waitFor();
                    await trace.screenshot({ path: path.join(artifactDirectory, `trace-${recordMode}-${eventRefresh ? 'event' : 'time'}-sequence.png`) });
                } finally {
                    await handle.evaluate(element => { element.__seamlessTraceProbe?.observer.disconnect(); delete element.__seamlessTraceProbe; });
                    await handle.dispose();
                }
                const historyCells = await selectHistoryRecord(startedAt);
                expect(historyCells[1] === '正常完了' && historyCells[2] === 'v1', 'Trace execution has a successful persisted terminal history entry');
                const selectedTrace = page.getByRole('region', { name: 'クエリ境界のトレース', exact: true });
                await selectedTrace.locator(`[data-トレース実行="${executionId}"] circle.トレース完了`).waitFor({ timeout: 30000 });
                const pendingObserved = updates.some(item => /保存待ち [1-9]\d* バイト/.test(item.status));
                const arrivalsObserved = new Set(updates.map(item => item.sequences.join(','))).size > 1;
                observations.push({ recordMode, displayRefresh: eventRefresh ? 'event' : 'time', configuredReliability, executionId, startedAt, resultMilliseconds, resultRows: rows, traceRows, traceStatus, summarySnapshot, updates, pendingObserved, arrivalsObserved, sampledFrames: traceRows.filter(row => row[5] === 'サンプル').length, sourceTooltip: tooltip, fullSourceLength, historyCells, timeCoordinates, coordinatesUnchanged, historyTraceIdentityMatched: true, noResultBeforeExplicitExecution: true, sameExecutionAfterDisplayChanges: true });
                await writeFile(path.join(artifactDirectory, 'trace-progress.json'), JSON.stringify({ completedCases: observations.filter(item => item.recordMode).length, observations }, null, 2));
            }
        }
    }
    expect(observations.every(observation => observation.passed !== false), 'A child DOM verification failed. See contextual-additions.json.');
    await writeFile(path.join(artifactDirectory, `${phase}.json`), JSON.stringify({ outcome: 'Passed', harnessSha256, pathPolicySha256, observations }, null, 2));
    console.log(JSON.stringify({ outcome: 'Passed', phase }));
} catch (error) {
    await writeFile(path.join(artifactDirectory, `${phase}.json`), JSON.stringify({ outcome: 'Failed', harnessSha256, pathPolicySha256, error: error.name, message: error.message.replace(/https?:\/\/\S+/g, '[url]') }, null, 2));
    await page.screenshot({ path: path.join(artifactDirectory, `${phase}-failure.png`), fullPage: true }).catch(() => {});
    console.log(JSON.stringify({ outcome: 'Failed', phase, error: error.name }));
    process.exitCode = 1;
} finally {
    await languageSession?.detach().catch(() => {});
    await browser.close();
}
