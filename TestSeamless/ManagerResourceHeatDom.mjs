import { readFile, rename } from 'node:fs/promises';
import path from 'node:path';

export async function verifyResourceHeat(page, browser, { artifactDirectory, browserDownloadPath = artifactDirectory }) {
    const check = (value, message) => { if (!value) throw new Error(message); };
    const tree = page.getByRole('navigation', { name: 'データベース', exact: true });
    await tree.locator('[data-対象-id="query:ItemList"]').click();
    await tree.locator('[data-対象-id="query:ItemList"][data-選択中="true"]').waitFor();
    const active = page.locator('.対象文書:visible');
    await active.getByRole('tab', { name: 'データ', exact: true }).click();
    const result = active.getByRole('region', { name: '問い合わせの実行', exact: true });
    await result.getByLabel('実行時の計測', { exact: true }).selectOption('Summary');
    await result.getByRole('checkbox', { name: '資源使用を計測', exact: true }).check();
    await result.getByLabel('収集間隔', { exact: true }).selectOption('100');
    await result.getByRole('button', { name: '実行', exact: true }).click();
    await result.getByRole('table', { name: '問い合わせ結果', exact: true }).waitFor();
    const statistics = result.getByRole('region', { name: '実行の軽量実測', exact: true });
    const executionId = (await statistics.locator('.設定稼働情報 > div').filter({ has: page.locator('small').filter({ hasText: /^実行ID$/ }) }).locator('strong').innerText()).trim();
    check(/^[0-9a-f-]{36}$/i.test(executionId), 'Actual measured execution ID');
    await active.getByRole('tab', { name: '実行計画', exact: true }).click();
    await active.getByRole('button', { name: '資源使用ヒートガント', exact: true }).click();
    const heat = active.getByRole('region', { name: '資源使用ヒートガント', exact: true });
    await heat.locator('[data-レーン]').first().waitFor();
    check((await heat.innerText()).includes(executionId), 'Heat and scalar summary identify the same execution');
    check(await heat.locator('[data-状態="Available"][data-出所="Observed"]').count() > 0, 'Actual OS samples appear as observed');
    await heat.getByLabel('資源の帰属範囲', { exact: true }).selectOption('Process');
    await heat.locator('[data-帰属="Process"]').first().waitFor();
    const processSamples = await heat.locator('[data-帰属="Process"]').count();
    check(processSamples > 0, 'Actual process observations');
    await heat.getByLabel('ヒートマップの配色', { exact: true }).selectOption('Accessible');
    await heat.getByRole('checkbox', { name: '数値を重ねる', exact: true }).check();
    await heat.getByRole('checkbox', { name: '観測窓の折線を重ねる', exact: true }).check();
    await page.screenshot({ path: path.join(artifactDirectory, 'resource-heat-process.png'), fullPage: true });
    await heat.getByLabel('資源の帰属範囲', { exact: true }).selectOption('Query');
    await heat.getByRole('status').filter({ hasText: 'この帰属範囲の資源レーンは未収集です。' }).waitFor();
    check(await heat.locator('[data-帰属="Host"], [data-帰属="Process"]').count() === 0, 'Host and process usage are never attributed to the query');
    await heat.getByLabel('資源の帰属範囲', { exact: true }).selectOption('Process');
    await heat.locator('[data-帰属="Process"]').first().waitFor();
    const save = active.getByRole('region', { name: '計画の保存と比較', exact: true });
    const details = save.locator('details').first();
    if (await details.getAttribute('open') === null) await details.locator('summary').first().click();
    const session = await browser.newBrowserCDPSession();
    await session.send('Browser.setDownloadBehavior', { behavior: 'allow', downloadPath: browserDownloadPath, eventsEnabled: true });
    const exports = [];
    try {
        for (const extension of ['json', 'svg', 'html']) {
            let guid, filename;
            const download = new Promise((resolve, reject) => {
                const timeout = setTimeout(() => { cleanup(); reject(new Error(`Heat ${extension} export timed out`)); }, 30000);
                const begin = event => { if (event.suggestedFilename.endsWith(`.${extension}`)) { guid = event.guid; filename = event.suggestedFilename; } };
                const progress = event => { if (event.guid === guid && ['completed', 'canceled'].includes(event.state)) { cleanup(); resolve(event); } };
                function cleanup() { clearTimeout(timeout); session.off('Browser.downloadWillBegin', begin); session.off('Browser.downloadProgress', progress); }
                session.on('Browser.downloadWillBegin', begin); session.on('Browser.downloadProgress', progress);
            });
            await save.getByRole('button', { name: `${extension.toUpperCase()}を保存`, exact: true }).click();
            const completed = await download;
            check(completed.state === 'completed', `Heat ${extension} download completed`);
            const output = path.join(artifactDirectory, `resource-heat.${extension}`);
            await rename(path.join(artifactDirectory, path.basename(filename)), output);
            const bytes = await readFile(output);
            check(bytes.length === completed.totalBytes && bytes.length > 0, `Heat ${extension} download bytes`);
            exports.push({ format: extension, bytes: bytes.length });
        }
    } finally { await session.detach(); }
    const json = await readFile(path.join(artifactDirectory, 'resource-heat.json'));
    const snapshot = JSON.parse(json);
    check(snapshot.format === 2, 'Resource snapshots use format 2');
    check(snapshot.data.extension.resourceHeat.executionId === executionId, 'Saved heat retains the exact execution ID');
    check(snapshot.data.extension.view.mode === 'Heat', 'Saved display mode matches the visible heat chart');
    await save.locator('input[type=file]').setInputFiles({ name: 'resource-heat.json', mimeType: 'application/json', buffer: json });
    const offline = save.getByRole('region', { name: '保存済みの実行計画', exact: true });
    await offline.getByRole('region', { name: '資源使用ヒートガント', exact: true }).waitFor();
    check((await offline.innerText()).includes(executionId), 'Offline replay retains the same execution');
    await page.screenshot({ path: path.join(artifactDirectory, 'resource-heat-offline.png'), fullPage: true });
    return { resourceHeat: true, executionId, processSamples, missingQueryAttributionExplicit: true, format: 2, exports, offlineReplay: true };
}

export async function verifyResourceHeatHistory(page, { artifactDirectory, snapshotPath }) {
    const snapshot = JSON.parse(await readFile(snapshotPath, 'utf8'));
    const statistics = snapshot.data.statistics;
    const executionId = statistics.executionId;
    const startedAt = new Date(statistics.startedAtUtc).toISOString().replace('T', ' ').slice(0, 23) + ' UTC';
    const tree = page.getByRole('navigation', { name: 'データベース', exact: true });
    await tree.locator('[data-対象-id="query:ItemList"]').click();
    await tree.locator('[data-対象-id="query:ItemList"][data-選択中="true"]').waitFor();
    const active = page.locator('.対象文書:visible');
    await active.getByRole('tab', { name: '実行履歴', exact: true }).click();
    const history = active.getByRole('region', { name: 'クエリの実績履歴', exact: true });
    await history.getByRole('button', { name: startedAt, exact: true }).click();
    await active.getByRole('tab', { name: '実行計画', exact: true, selected: true }).waitFor();
    await active.getByRole('button', { name: '資源使用ヒートガント', exact: true }).click();
    const heat = active.getByRole('region', { name: '資源使用ヒートガント', exact: true });
    await heat.locator('[data-状態="Available"][data-出所="Observed"]').first().waitFor();
    const text = await heat.innerText();
    if (!text.includes(executionId) || !text.includes('保存済み')) throw new Error('The persisted resource recording does not match the selected historical execution.');
    await page.screenshot({ path: path.join(artifactDirectory, 'resource-heat-history.png'), fullPage: true });
    return { resourceHistory: true, executionId, queryWasExecuted: false, persistedRecordingRestored: true };
}
