import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';

// 所有する隔離ManagerのPageを利用し、開閉だけを確認する。保存やDB作成は行わない。
export async function verifyManagerTreeExpansion(page, { artifactDirectory, databaseName = 'VisualPlanDemo' } = {}) {
    if (!artifactDirectory || !path.resolve(artifactDirectory).toLowerCase().startsWith('f:\\seamless\\')) throw new Error('Supply an owned artifact directory within F:\\Seamless.');
    await mkdir(artifactDirectory, { recursive: true });
    const sidebar = page.locator('.ナビゲーション');
    const row = sidebar.getByRole('group', { name: databaseName, exact: true });
    const caret = target => target.locator(':scope > .対象内容 > button.枝の開閉');
    const tree = row.locator('xpath=following-sibling::*[1][@data-db-tree]');
    const observations = [];
    const restorations = [];
    const evidence = { passed: false, physicalInput: false, writes: false, observations };
    const snapshot = async () => ({
        title: await page.locator('.作業領域 h1').innerText(),
        tabs: await page.locator('.文書タブ').evaluateAll(items => items.map(item => item.getAttribute('data-文書-id'))),
        display: await page.locator('.表示切替 button[data-選択中=true]').allTextContents()
    });
    async function expanded(target, expected) {
        await page.waitForFunction(({ id, expected }) => {
            const element = document.querySelector(`[data-tree-probe="${id}"]`);
            return element?.getAttribute('aria-expanded') === String(expected);
        }, { id: await target.getAttribute('data-tree-probe'), expected }, { timeout: 10000 });
    }
    let sequence = 0;
    async function register(button) {
        const key = `tree-${process.pid}-${++sequence}`;
        await button.evaluate((element, key) => element.setAttribute('data-tree-probe', key), key);
        return button;
    }
    async function setExpanded(button, value, key) {
        await button.press(key ?? (value ? 'ArrowRight' : 'ArrowLeft'));
        await expanded(button, value);
    }
    try {
        const selectedExpansion = await caret(row).getAttribute('aria-expanded');
        await row.locator(':scope > .対象内容 > .対象名').click();
        await page.getByRole('heading', { name: databaseName, exact: true }).waitFor();
        assert.equal(await caret(row).getAttribute('aria-expanded'), selectedExpansion, 'Selecting a database does not change its expansion');
        const before = await snapshot();
        const dbCaret = await register(caret(row));
        assert.equal(await dbCaret.getAttribute('type'), 'button');
        await setExpanded(dbCaret, false);
        await tree.waitFor({ state: 'hidden' });
        assert.deepEqual(await snapshot(), before);
        observations.push({ case: 'database-collapse', editorPreserved: true });
        await setExpanded(dbCaret, true);
        await tree.waitFor({ state: 'visible' });
        const groups = tree.locator('[data-対象-id^="group:"]:visible');
        const groupIds = await groups.evaluateAll(items => items.map(item => item.getAttribute('data-対象-id')));
        assert.ok(groupIds.some(id => id.startsWith('group:Table:')), 'The fixture has a table category.');
        assert.ok(groupIds.some(id => id.startsWith('group:View:')), 'The fixture has a view category.');
        assert.ok(groupIds.some(id => id.startsWith('group:Function:')), 'The fixture has a function category.');
        for (const id of groupIds) {
            const escaped = await page.evaluate(id => CSS.escape(id), id);
            const actual = tree.locator(`[data-対象-id="${escaped}"]`);
            let button = await register(caret(actual));
            const original = await button.getAttribute('aria-expanded') === 'true';
            restorations.push(async () => { await setExpanded(await register(caret(actual)), original); });
            await setExpanded(button, false);
            await actual.locator('xpath=following-sibling::*[1][contains(@class,"オブジェクト子一覧")]').waitFor({ state: 'hidden' });
            await setExpanded(button, true);
            await actual.locator('xpath=following-sibling::*[1][contains(@class,"オブジェクト子一覧")]').waitFor({ state: 'visible' });
            observations.push({ case: 'category', id, leftCollapses: true, rightExpands: true });
        }
        const schemaIds = await tree.locator('[data-対象-id^="schema:"]').evaluateAll(items => items.map(item => item.getAttribute('data-対象-id')));
        for (const id of schemaIds) {
            const escaped = await page.evaluate(id => CSS.escape(id), id);
            const schema = tree.locator(`[data-対象-id="${escaped}"]`);
            let button = await register(caret(schema));
            const original = await button.getAttribute('aria-expanded') === 'true';
            restorations.push(async () => { await setExpanded(await register(caret(schema)), original); });
            await setExpanded(button, false);
            await schema.locator('xpath=following-sibling::*[contains(@class,"スキーマ子一覧")]').waitFor({ state: 'hidden' });
            await setExpanded(button, true, 'Enter');
            await schema.locator('xpath=following-sibling::*[contains(@class,"スキーマ子一覧")]').waitFor({ state: 'visible' });
            observations.push({ case: 'schema', id, enterExpands: true });
        }
        assert.deepEqual(await snapshot(), before);
        await page.screenshot({ path: path.join(artifactDirectory, 'tree-expanded.png'), fullPage: true });
        await setExpanded(dbCaret, false);
        await tree.waitFor({ state: 'hidden' });
        await page.screenshot({ path: path.join(artifactDirectory, 'tree-collapsed.png'), fullPage: true });
        await setExpanded(dbCaret, true);
        await tree.waitFor({ state: 'visible' });
        observations.push({ case: 'editor-and-tabs-preserved', before, after: await snapshot() });
        evidence.passed = true;
    } catch (error) {
        evidence.error = { name: error.name, message: error.message }; throw error;
    } finally {
        for (const restore of restorations.reverse()) {
            try { await restore(); } catch (error) { (evidence.cleanupErrors ??= []).push(error.message); }
        }
        await writeFile(path.join(artifactDirectory, 'tree.json'), JSON.stringify(evidence, null, 2));
    }
    return evidence;
}
