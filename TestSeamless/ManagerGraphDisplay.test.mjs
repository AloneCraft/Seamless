import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';

const [artifactDirectory, playwrightEntry] = process.argv.slice(2);
if (!artifactDirectory || !playwrightEntry) throw new Error('Supply an isolated artifact directory and the Playwright entry.');
const directory = path.resolve(artifactDirectory);
if (!directory.toLowerCase().startsWith('f:\\seamless\\')) throw new Error('Use the owned F:\\Seamless artifact root.');
const require = createRequire(playwrightEntry);
const { chromium } = require(playwrightEntry);
const managerCss = await readFile(new URL('../Seamless.Management.Components/wwwroot/manager.css', import.meta.url), 'utf8');
const graphCss = await readFile(new URL('../Seamless.Management.Components/wwwroot/execution-plan.css', import.meta.url), 'utf8');
const graphJs = await readFile(new URL('../Seamless.Management.Components/wwwroot/execution-plan.js', import.meta.url), 'utf8');
const sha = value => createHash('sha256').update(value).digest('hex').toUpperCase();

test('both graph kinds expand to the viewport and restore focus, modal isolation, and scroll', async () => {
    await mkdir(directory, { recursive: true });
    const context = await chromium.launchPersistentContext(path.join(directory, 'owned-edge-profile'), { channel: 'msedge', headless: true, viewport: { width: 2300, height: 1000 }, args: ['--no-first-run', '--disable-background-networking', '--disable-sync'] });
    const page = context.pages()[0] ?? await context.newPage();
    const evidence = { passed: false, physicalInput: false, syntheticDomFixture: true, managerCssSha256: sha(managerCss), graphCssSha256: sha(graphCss), graphJsSha256: sha(graphJs), checks: [] };
    const graph = id => `<section id="${id}" class="計画グラフ" aria-label="${id}"><div class="計画グラフ操作"><div class="グラフ表示操作"><button type="button" class="控えめボタン" data-グラフ表示="maximize" aria-expanded="false">Maximize</button><button type="button" class="控えめボタン" data-グラフ表示="restore" hidden>Restore · Esc</button></div><input aria-label="Search ${id}"></div><div class="計画グラフ配置"><div class="計画グラフ画面" tabindex="0"><svg style="width:2800px;height:2000px"><rect x="20" y="20" width="220" height="100" fill="#edf5e9" stroke="#54836c"/></svg></div><aside class="計画グラフ詳細"><h3>Node details</h3><button class="控えめボタン">Last action</button></aside></div></section>`;
    try {
        await page.setContent(`<main class="管理ワークスペース"><aside id="outside" class="ナビゲーション"><button>Background action</button><div id="original-inert" inert>Already inert</div></aside><section class="作業領域"><article id="graph-document" class="文書領域"><h1>Wide graph workspace</h1>${graph('normal')}${graph('generated')}</article><article id="ordinary-document" class="文書領域"><h2>Ordinary form keeps its width</h2><input></article></section></main>`);
        await page.addStyleTag({ content: managerCss }); await page.addStyleTag({ content: graphCss });
        await page.evaluate(async source => {
            const module = await import(`data:text/javascript;base64,${btoa(unescape(encodeURIComponent(source)))}`);
            window.graphDisplayApis = {};
            for (const id of ['normal', 'generated']) window.graphDisplayApis[id] = module.グラフ表示を初期化(document.getElementById(id));
        }, graphJs);
        const dimensions = await page.evaluate(() => ({ graph: document.getElementById('graph-document').getBoundingClientRect().width, ordinary: document.getElementById('ordinary-document').getBoundingClientRect().width }));
        assert.ok(dimensions.graph > 1800, 'Graph pages use the available wide workspace.'); assert.equal(dimensions.ordinary, 1140);
        evidence.checks.push({ case: 'graph-only-wide-layout', ...dimensions });
        await page.screenshot({ path: path.join(directory, 'wide.png'), fullPage: false });
        for (const id of ['normal', 'generated']) {
            const root = page.locator(`#${id}`), open = root.locator('[data-グラフ表示="maximize"]'), close = root.locator('[data-グラフ表示="restore"]');
            await root.locator('.計画グラフ画面').evaluate(element => { element.scrollLeft = 100; element.scrollTop = 200; });
            await open.click();
            const modal = await root.evaluate(element => ({ role: element.getAttribute('role'), modal: element.getAttribute('aria-modal'), width: element.getBoundingClientRect().width, height: element.getBoundingClientRect().height, outsideInert: document.getElementById('outside').inert, overflow: document.body.style.overflow }));
            assert.equal(modal.role, 'dialog'); assert.equal(modal.modal, 'true'); assert.equal(modal.width, 2300); assert.equal(modal.height, 1000); assert.equal(modal.outsideInert, true); assert.equal(modal.overflow, 'hidden');
            await page.keyboard.press('Shift+Tab');
            assert.equal(await root.locator('button').last().evaluate(element => element === document.activeElement), true);
            await page.keyboard.press('Tab'); assert.equal(await close.evaluate(element => element === document.activeElement), true);
            await root.evaluate(element => {
                const menu = document.createElement('div'); menu.setAttribute('role', 'menu'); menu.innerHTML = '<button>Inner menu</button>';
                menu.addEventListener('keydown', event => { if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); menu.remove(); } });
                element.appendChild(menu); menu.querySelector('button').focus();
            });
            await page.keyboard.press('Escape'); assert.equal(await root.getAttribute('aria-modal'), 'true');
            await close.focus(); await page.keyboard.press('Escape');
            assert.equal(await root.getAttribute('aria-modal'), null); assert.equal(await open.evaluate(element => element === document.activeElement), true);
            const restored = await root.locator('.計画グラフ画面').evaluate(element => ({ left: element.scrollLeft, top: element.scrollTop, outsideInert: document.getElementById('outside').inert, originalInert: document.getElementById('original-inert').inert, overflow: document.body.style.overflow }));
            assert.deepEqual(restored, { left: 100, top: 200, outsideInert: false, originalInert: true, overflow: '' });
            evidence.checks.push({ case: 'maximize-escape-restore', id, modal, restored });
            await open.click(); await page.screenshot({ path: path.join(directory, `maximized-${id}.png`), fullPage: false });
            await close.click(); assert.equal(await root.getAttribute('aria-modal'), null);
        }
        await page.locator('#normal [data-グラフ表示="maximize"]').click();
        await page.evaluate(() => window.graphDisplayApis.normal.dispose());
        assert.equal(await page.locator('#outside').evaluate(element => element.inert), false);
        assert.equal(await page.locator('#original-inert').evaluate(element => element.inert), true);
        assert.equal(await page.locator('#normal').getAttribute('data-グラフ表示準備'), null);
        await page.locator('#generated [data-グラフ表示="maximize"]').click();
        await page.locator('#generated').evaluate(element => element.remove());
        await page.waitForFunction(() => !document.getElementById('outside').inert);
        assert.equal(await page.evaluate(() => document.body.style.overflow), '');
        evidence.checks.push({ case: 'dispose-and-removed-root-restore-background', passed: true });
        evidence.passed = true;
    } catch (error) { evidence.error = { name: error.name, message: error.message }; throw error; }
    finally { await writeFile(path.join(directory, 'graph-display.json'), JSON.stringify(evidence, null, 2)); await context.close(); }
});
