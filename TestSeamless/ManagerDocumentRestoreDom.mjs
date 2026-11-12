import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';

// 呼出元が所有する隔離Managerと新規ブラウザーContextを使う。DBの保存・適用・クエリ実行は行わない。
export async function verifyManagerDocumentRestoration(page, {
    artifactDirectory, assetHashes, databaseId = 'db:visual-db', databaseName = 'VisualPlanDemo',
    tableId = 'Items', viewId = 'ItemList', functionId = 'Amount'
}) {
    const directory = resolve(artifactDirectory ?? '');
    if (!/^f:[\\/]seamless[\\/]/iu.test(directory) && !directory.startsWith('/mnt/f/Seamless/')) throw new Error('An owned F:/Seamless artifact directory is required.');
    if (!assetHashes?.['browser-state.js']) throw new Error('Supply hashes of the fixed candidate assets.');
    await mkdir(directory, { recursive: true });
    const proof = { passed: false, physicalInput: false, databaseWrites: false, databaseUpgradePerformed: false, steps: [] };
    const digest = value => createHash('sha256').update(JSON.stringify(value)).digest('hex');
    const mark = async (stage, data = {}) => { proof.steps.push({ stage, at: new Date().toISOString(), ...data }); await writeFile(join(directory, 'document-restoration-proof.json'), JSON.stringify(proof, null, 2)); };
    const ready = async () => page.waitForFunction(() => { const root = document.querySelector('.管理ワークスペース'); return root?.dataset.interactiveReady === 'true' && !root.inert; });
    const nav = page.getByRole('navigation', { name: 'データベース', exact: true });
    const tree = nav.locator(`[data-db-tree="${databaseId}"]`);
    const dbRow = nav.locator(`.対象行[data-対象-id="${databaseId}"]`);
    const active = () => page.locator('.対象文書:visible');
    const labels = () => page.locator('.文書タブ > .対象行').evaluateAll(rows => rows.map(row => ({ name: row.getAttribute('aria-label'), selected: row.dataset.選択中 === 'true' })));
    const setView = async name => { await active().getByRole('tab', { name, exact: true }).click(); await active().getByRole('tab', { name, exact: true }).and(page.locator('[aria-selected="true"]')).waitFor(); };
    const open = async (kind, id) => { const row = tree.locator(`[data-対象-id="${kind}:${id}"]`); await row.click(); await row.and(page.locator('[data-選択中="true"]')).waitFor(); await active().waitFor(); };
    let key, original, origin;
    const read = () => page.evaluate(key => JSON.parse(localStorage.getItem(key)), key);
    const waitState = async predicate => { await page.waitForFunction(({ key, source }) => { const raw = localStorage.getItem(key); return raw && (0, eval)(`(${source})`)(JSON.parse(raw)); }, { key, source: predicate.toString() }); return read(); };
    const summary = state => ({ format: state.format, selected: state.selectedDocument, documents: state.documents.map(doc => ({ kind: doc.kind, object: doc.objectId, view: doc.view, version: doc.version })), sha256: digest(state) });
    const reload = async () => { await page.reload({ waitUntil: 'domcontentloaded' }); await ready(); assert.equal(new URL(page.url()).origin, origin, 'Reload must keep the same browser origin.'); };
    const replace = async state => { await page.evaluate(({ key, state }) => localStorage.setItem(key, JSON.stringify(state)), { key, state }); await reload(); };
    try {
        await ready(); origin = new URL(page.url()).origin;
        assert.deepEqual(await labels(), [{ name: databaseName, selected: true }], 'Use a fresh context and the isolated fixture database.');
        for (const [name, expected] of Object.entries(assetHashes)) {
            assert.match(expected, /^[a-f\d]{64}$/iu);
            const url = new URL('_content/Seamless.Management.Components/' + name, await page.evaluate(() => document.baseURI));
            const response = await page.request.get(url.toString(), { headers: { 'Accept-Encoding': 'identity' } }); assert.equal(response.status(), 200);
            assert.equal(createHash('sha256').update(await response.body()).digest('hex').toLowerCase(), expected.toLowerCase(), 'Asset ' + name);
        }
        key = await page.evaluate(() => { const keys = Object.keys(localStorage).filter(key => /^Seamless\.Manager\.Sidebar\.v1\.[0-9a-f]{32}\./u.test(key)); if (keys.length !== 1) throw new Error('Expected exactly one authenticated instance key.'); return keys[0]; });
        const dbCaret = dbRow.locator(':scope > .対象内容 > button.枝の開閉');
        if (await dbCaret.getAttribute('aria-expanded') === 'false') await dbCaret.click();
        await tree.waitFor({ state: 'visible' });
        for (const kind of ['Table', 'View', 'Function']) {
            const caret = tree.locator(`[data-対象-id="group:${kind}:"] > .対象内容 > button.枝の開閉`);
            if (await caret.getAttribute('aria-expanded') === 'false') await caret.click();
            await caret.and(page.locator('[aria-expanded="true"]')).waitFor();
        }
        await open('table', tableId); await setView('C#');
        const source = active().getByRole('textbox', { name: '対象のC#', exact: true }); const originalSource = await source.inputValue();
        assert.ok(originalSource.length > 0);
        await open('query', viewId); await setView('構造');
        await open('query', functionId); await setView('データ');
        const parameter = active().getByRole('textbox', { name: '引数 orderId', exact: true });
        await parameter.fill('987654321'); await setView('実行計画');
        original = await waitState(state => state.format === 4 && state.documents?.length === 4 && state.selectedDocument === 3 && state.documents[3].view === '実行計画');
        assert.deepEqual(original.documents.map(doc => [doc.kind, doc.objectId]), [['Database', ''], ['Table', tableId], ['View', viewId], ['ScalarFunction', functionId]]);
        assert.equal(original.documents[1].view, 'C#'); assert.equal(original.documents[2].view, '構造');
        const before = await labels(); await mark('FourDocumentReferencesStored', { ...summary(original), labels: before, assetsVerified: Object.keys(assetHashes) });
        assert.doesNotMatch(JSON.stringify(original), /987654321|"(?:arguments|source|sourceCode|rows|token)"/iu);
        await reload();
        await active().filter({ has: page.getByRole('heading', { name: functionId, exact: true }) }).waitFor();
        await active().getByRole('tab', { name: '実行計画', exact: true }).and(page.locator('[aria-selected="true"]')).waitFor();
        assert.deepEqual(await labels(), before, 'Document names, order and selection survive reload.');
        await page.locator(`.文書タブ > .対象行[aria-label="${viewId}"]`).click();
        await active().getByRole('tab', { name: '構造', exact: true }).and(page.locator('[aria-selected="true"]')).waitFor();
        await page.locator(`.文書タブ > .対象行[aria-label="${tableId}"]`).click();
        await active().getByRole('tab', { name: 'C#', exact: true }).and(page.locator('[aria-selected="true"]')).waitFor();
        assert.equal(await active().getByRole('textbox', { name: '対象のC#', exact: true }).inputValue(), originalSource);
        await page.locator(`.文書タブ > .対象行[aria-label="${functionId}"]`).click(); await setView('データ');
        assert.notEqual(await active().getByRole('textbox', { name: '引数 orderId', exact: true }).inputValue(), '987654321', 'Argument input is not reconstructed.');
        await setView('実行計画');
        await mark('SameOriginReloadRestoresDocumentsSelectionAndViews', summary(await read()));
        await page.screenshot({ path: join(directory, 'restored-function.png'), fullPage: false });

        const mismatch = structuredClone(original); const mismatched = mismatch.documents[mismatch.selectedDocument]; mismatched.version++;
        await replace(mismatch);
        await page.getByRole('status').filter({ hasText: '定義版・成果物または下書きが変わった文書は復元しませんでした。' }).waitFor();
        const afterMismatch = await labels(); assert.equal(afterMismatch.some(tab => tab.name === functionId), false); assert.equal(afterMismatch.some(tab => tab.selected), false);
        assert.equal(await tree.locator(`[data-対象-id="query:${functionId}"]`).count(), 1, 'The real function still exists.');
        await mark('VersionMismatchIsRejectedWithoutGuessingAnotherDocument', { fixtureInjectedMismatch: true, realDatabaseUpgrade: false, alteredReferenceHash: digest(mismatch), labels: afterMismatch });
        await page.screenshot({ path: join(directory, 'version-mismatch.png'), fullPage: false });
        await replace(original);
        await active().filter({ has: page.getByRole('heading', { name: functionId, exact: true }) }).waitFor();

        // サーバーへ保存しない不正C#を入力して、対象参照の除外と再読込後の元定義を照合する。
        const tableDocument = page.locator(`.文書タブ > .対象行[aria-label="${tableId}"]`);
        await tableDocument.click(); await active().getByRole('tab', { name: 'C#', exact: true }).and(page.locator('[aria-selected="true"]')).waitFor();
        const marker = '__Unsaved_' + randomUUID().replaceAll('-', '');
        await active().getByRole('textbox', { name: '対象のC#', exact: true }).fill(originalSource + '\n' + marker + ' ???');
        const withoutUnsaved = await waitState(state => state.format === 4 && state.documents?.length === 3 && !state.documents.some(doc => doc.kind === 'Table'));
        assert.equal(withoutUnsaved.selectedDocument, null); assert.equal(JSON.stringify(withoutUnsaved).includes(marker), false);
        await page.getByRole('status').filter({ hasText: '未保存の変更がある文書は復元情報に含めません。' }).waitFor();
        await mark('UnsavedSourceAndItsDocumentReferenceAreExcluded', summary(withoutUnsaved));
        let dialogs = 0; const accept = async dialog => { assert.equal(dialog.type(), 'beforeunload'); dialogs++; await dialog.accept(); };
        page.on('dialog', accept); try { await reload(); } finally { page.off('dialog', accept); }
        assert.equal((await labels()).some(tab => tab.name === tableId), false);
        await mark('ReloadDoesNotRecreateAnUnsavedDocument', { dialogs, labels: await labels() });
        // 左の開閉は復元対象外なので、既存タブから元のテーブルを開く前に分類を明示的に展開する。
        const caret = tree.locator('[data-対象-id="group:Table:"] > .対象内容 > button.枝の開閉'); if (await caret.getAttribute('aria-expanded') === 'false') await caret.click();
        await open('table', tableId); await setView('C#');
        assert.equal(await active().getByRole('textbox', { name: '対象のC#', exact: true }).inputValue(), originalSource, 'The server definition was never changed.');
        await mark('ReopenedTableUsesTheUnchangedServerDefinition');
        await replace(original); await active().getByRole('tab', { name: '実行計画', exact: true }).and(page.locator('[aria-selected="true"]')).waitFor();
        proof.passed = true; await mark('Completed'); return proof;
    } catch (error) {
        proof.failure = String(error); await writeFile(join(directory, 'document-restoration-proof.json'), JSON.stringify(proof, null, 2)); throw error;
    }
}

// 実サーバーが返したView専用利用者のUserInfoと、その利用者で認証した新規Contextを呼出元が用意する。
// 所有者セッションのDOMやlocalStorageだけを書き換えて権限不足を模擬しない。
export async function verifyViewOnlyDocumentRestoration(page, { artifactDirectory, seed, userInfo, databaseName = 'VisualPlanDemo' }) {
    const directory = resolve(artifactDirectory ?? '');
    if (!/^f:[\\/]seamless[\\/]/iu.test(directory) && !directory.startsWith('/mnt/f/Seamless/')) throw new Error('An owned F:/Seamless artifact directory is required.');
    const grants = userInfo?.grants?.filter(grant => grant.database === '*' || grant.database === databaseName);
    if (!userInfo?.name || userInfo.disabled || !grants?.length || grants.some(grant => grant.permissions !== 1)) throw new Error('Supply actual server UserInfo showing View-only grants for the fixture database.');
    if (seed?.format !== 4 || !seed.documents?.length) throw new Error('Supply the previously observed exact document-reference state.');
    await mkdir(directory, { recursive: true });
    const proof = { passed: false, physicalInput: false, databaseWrites: false, currentContract: 'GetDefinition requires View and Edit', source: 'Authenticated View-only user; saved reference metadata fixture' };
    try {
        const ready = () => page.waitForFunction(() => { const root = document.querySelector('.管理ワークスペース'); return root?.dataset.interactiveReady === 'true' && !root.inert; });
        await ready(); assert.equal((await page.locator('.利用者名').innerText()).trim(), userInfo.name);
        assert.equal(await page.locator('.文書タブ').count(), 0, 'Use a fresh View-only session.');
        const suffix = '.' + Buffer.from(userInfo.name).toString('base64');
        const key = await page.evaluate(suffix => { const keys = Object.keys(localStorage).filter(key => /^Seamless\.Manager\.Sidebar\.v1\.[0-9a-f]{32}\./u.test(key) && key.endsWith(suffix)); if (keys.length !== 1) throw new Error('Missing authenticated View-only browser key.'); return keys[0]; }, suffix);
        const state = structuredClone(seed); state.openDatabaseIds = [];
        await page.evaluate(({ key, state }) => localStorage.setItem(key, JSON.stringify(state)), { key, state });
        const origin = new URL(page.url()).origin; await page.reload({ waitUntil: 'domcontentloaded' }); await ready(); assert.equal(new URL(page.url()).origin, origin);
        await page.getByRole('status').filter({ hasText: '文書の参照権限が不足しているため、復元しませんでした。' }).waitFor();
        assert.equal(await page.locator('.文書タブ').count(), 0); assert.equal(await page.locator('.対象文書').count(), 0); assert.equal(await page.getByRole('textbox', { name: '対象のC#', exact: true }).count(), 0);
        proof.passed = true; proof.documentCount = state.documents.length; proof.referenceHash = createHash('sha256').update(JSON.stringify(state)).digest('hex');
        await page.screenshot({ path: join(directory, 'view-only-restoration-denied.png'), fullPage: false }); return proof;
    } catch (error) { proof.failure = String(error); throw error; }
    finally { await writeFile(join(directory, 'view-only-restoration-proof.json'), JSON.stringify(proof, null, 2)); }
}
