import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';

// 独立したCSS fixtureだけを開く。実行中のManagerや既存ブラウザーへは接続しない。
const [artifactDirectory, playwrightEntry] = process.argv.slice(2);
if (!artifactDirectory || !playwrightEntry) throw new Error('Supply an isolated artifact directory and the Playwright entry.');
const resolved = path.resolve(artifactDirectory);
if (!resolved.toLowerCase().startsWith('f:\\seamless\\')) throw new Error('Artifacts and the owned browser profile must stay within F:\\Seamless.');
const require = createRequire(playwrightEntry);
const { chromium } = require(playwrightEntry);
const managerCss = await readFile(new URL('../Seamless.Management.Components/wwwroot/manager.css', import.meta.url), 'utf8');
const graphCss = await readFile(new URL('../Seamless.Management.Components/wwwroot/execution-plan.css', import.meta.url), 'utf8');
const sha = content => createHash('sha256').update(content).digest('hex').toUpperCase();

test('native controls share the Manager styling while contextual handles and graph cards keep their own density', async () => {
    await mkdir(resolved, { recursive: true });
    const profile = path.join(resolved, 'owned-edge-profile');
    const browser = await chromium.launchPersistentContext(profile, {
        channel: 'msedge', headless: true, viewport: { width: 1000, height: 760 },
        args: ['--no-first-run', '--disable-background-networking', '--disable-sync']
    });
    const page = browser.pages()[0] ?? await browser.newPage();
    const evidence = { passed: false, processId: process.pid, profile, managerCssSha256: sha(managerCss), graphCssSha256: sha(graphCss), physicalInput: false, measurements: {} };
    try {
        await page.setContent(`<main class="管理ワークスペース"><section style="padding:30px;max-width:900px">
            <h1>Manager controls · isolated CSS fixture</h1>
            <p><label>Structure level <select id="type"><option>関係代数</option><option>論理処理</option></select></label> <button id="plain" class="控えめボタン">変更を確認</button> <button id="empty" class="控えめボタン">再読み込み</button></p>
            <nav class="表示切替"><button id="tab">C#</button><button class="" id="empty-tab">Data</button></nav>
            <p><label>Artifact <input id="file" type="file"></label></p>
            <p><select id="disabled-select" disabled><option>Disabled</option></select> <button id="disabled-button" disabled>Disabled</button> <input id="disabled-file" type="file" disabled></p>
            <div class="対象行" style="width:300px"><span class="操作帯"><button id="handle" type="button" class="操作ハンドル" aria-label="Add">+</button></span><div class="対象内容">Contextual handle · 22 × 25</div></div>
            <section class="計画グラフ"><div style="width:236px;height:112px"><button id="card" class="計画グラフノード"><small>Original source operator</small><strong>Where</strong><span>System.Int32</span></button></div></section>
        </section></main>`);
        await page.addStyleTag({ content: managerCss });
        await page.addStyleTag({ content: graphCss });
        const style = async (id, pseudo = null) => page.locator(`#${id}`).evaluate((element, pseudo) => {
            const value = getComputedStyle(element, pseudo);
            return { height: value.height, minHeight: value.minHeight, width: value.width, padding: value.padding, borderWidth: value.borderTopWidth, borderStyle: value.borderTopStyle, radius: value.borderTopLeftRadius, background: value.backgroundColor, color: value.color, cursor: value.cursor, opacity: value.opacity, outlineWidth: value.outlineWidth, outlineStyle: value.outlineStyle, focusVisible: element.matches(':focus-visible') };
        }, pseudo);
        for (const id of ['type', 'plain', 'empty']) {
            const value = await style(id); evidence.measurements[id] = value;
            assert.equal(value.height, '32px', `${id} has a 32px control height`);
            assert.equal(value.borderWidth, '1px'); assert.equal(value.radius, '5px');
        }
        const file = await style('file', '::file-selector-button'); evidence.measurements.file = file;
        assert.equal(file.minHeight, '32px'); assert.equal(file.borderWidth, '1px'); assert.equal(file.radius, '5px');
        for (const id of ['tab', 'empty-tab']) {
            const value = await style(id); evidence.measurements[id] = value;
            assert.equal(value.borderWidth, '0px', 'Document tabs keep their unboxed appearance');
            assert.equal(value.height, '39px');
        }
        await page.locator('#type').focus();
        evidence.measurements.focus = await style('type');
        assert.equal(evidence.measurements.focus.outlineWidth, '2px'); assert.equal(evidence.measurements.focus.outlineStyle, 'solid');
        assert.equal(evidence.measurements.focus.focusVisible, true);
        const disabled = await style('disabled-select'); evidence.measurements.disabledSelect = disabled;
        assert.equal(disabled.cursor, 'default'); assert.equal(disabled.background, 'rgb(243, 245, 240)');
        evidence.measurements.disabledButton = await style('disabled-button');
        assert.equal(evidence.measurements.disabledButton.opacity, '0.4');
        evidence.measurements.disabledFile = await style('disabled-file', '::file-selector-button');
        assert.equal(evidence.measurements.disabledFile.opacity, '0.4');
        const handle = await style('handle'); evidence.measurements.handle = handle;
        assert.equal(handle.width, '22px'); assert.equal(handle.height, '25px'); assert.equal(handle.borderWidth, '0px'); assert.equal(handle.radius, '4px');
        const card = await style('card'); evidence.measurements.card = card;
        assert.equal(card.width, '236px'); assert.equal(card.height, '112px'); assert.equal(card.borderWidth, '1px'); assert.equal(card.radius, '8px'); assert.equal(card.padding, '13px 15px');
        evidence.passed = true;
    } catch (error) {
        evidence.error = { name: error.name, message: error.message }; throw error;
    } finally {
        await page.screenshot({ path: path.join(resolved, 'controls.png'), fullPage: true });
        await writeFile(path.join(resolved, 'controls.json'), JSON.stringify(evidence, null, 2));
        await browser.close();
    }
});
