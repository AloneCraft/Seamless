import { createHash } from 'node:crypto';
import { writeFile } from 'node:fs/promises';
import path from 'node:path';

// Runs only against the isolated acceptance fixture containing profile-sum.
export async function verifyNodeProfile(page, browser, { artifactDirectory }) {
    const check = (value, message) => { if (!value) throw new Error(message); };
    const tree = page.getByRole('navigation', { name: 'データベース', exact: true });
    const target = tree.locator('[data-対象-id="query:registered:profile-sum"]');
    if (!await target.isVisible()) {
        const database = tree.locator('[data-対象-id^="db:"]').first();
        const toggle = database.locator(':scope > .対象内容 > button.枝の開閉');
        if (await toggle.count() && await toggle.getAttribute('aria-expanded') === 'false') await toggle.click();
        const groups = tree.locator('[data-対象-id^="group:Function:"]');
        for (let index = 0; index < await groups.count(); index++) {
            if (await target.isVisible()) break;
            if (!await groups.nth(index).isVisible()) continue;
            const button = groups.nth(index).locator(':scope > .対象内容 > button.枝の開閉');
            if (await button.count() && await button.getAttribute('aria-expanded') === 'false') await button.click();
        }
    }
    await target.click();
    await tree.locator('[data-対象-id="query:registered:profile-sum"][data-選択中="true"]').waitFor();
    const active = page.locator('.対象文書:visible[data-文書対象="profile-sum"]');
    await active.waitFor();
    await active.getByRole('tab', { name: 'データ', exact: true }).click();
    const result = active.getByRole('region', { name: '問い合わせの実行', exact: true });
    await result.getByLabel('実行時の計測', { exact: true }).selectOption('Profiled');
    await result.getByRole('button', { name: '実行', exact: true }).click();
    const output = result.getByRole('status');
    const returned = result.locator('output[aria-label="戻り値"]');
    await returned.waitFor();
    check((await returned.innerText()).trim() === '5', `Profiled fixture returns 5. Status: ${await output.allTextContents()}`);
    const executionIdFrom = async container => {
        const statistics = container.getByRole('region', { name: '実行の軽量実測', exact: true });
        return (await statistics.locator('.設定稼働情報 > div').filter({ has: page.locator('small').filter({ hasText: /^実行ID$/ }) }).locator('strong').innerText()).trim();
    };
    const executionId = await executionIdFrom(result);
    check(/^[0-9a-f-]{36}$/i.test(executionId), 'Actual Profiled execution ID');
    await active.getByRole('tab', { name: '実行計画', exact: true }).click();
    const plan = active.locator('.実行計画領域');
    await plan.getByLabel('構造の段階', { exact: true }).selectOption('Logical');
    await plan.locator('[data-表示方式="DAG"]').click();
    await plan.getByRole('button', { name: /^PipelineTerminal、ノード/ }).click();
    await plan.getByRole('button', { name: /^PipelineTerminal、ノード/ }).click();
    const quantities = plan.getByRole('region', { name: '物理消費区間の実測', exact: true });
    await quantities.locator('tbody tr').waitFor();
    check(await quantities.locator('tbody tr').count() === 1, 'Exactly one actual fused region');
    const row = quantities.locator('tbody tr').first();
    const cells = await row.locator('td').allTextContents();
    check(cells[3].trim() === '2' && cells[4].trim() === '1', 'Consumer input 2 and scalar output 1');
    check(cells[5].trim() === '正常完了', 'Actual region succeeded');
    check(/^managed-thread:\d+$/.test(cells[0].trim()), 'Observed managed thread is retained');
    check(await row.getAttribute('title') === 'FusedPipelineInclusive', 'Inclusive fused scope remains explicit');
    const nodeId = await row.getAttribute('data-ノード');
    const instanceId = await row.getAttribute('data-区間');
    check(nodeId && /^profile:\d+$/.test(instanceId), 'Actual node and region identities');
    check(await executionIdFrom(plan) === executionId, 'Plan and result share the same execution');
    await page.screenshot({ path: path.join(artifactDirectory, 'node-profile-dag.png'), fullPage: true });

    await plan.getByLabel('構造の段階', { exact: true }).selectOption('Generated');
    const variant = plan.getByLabel('生成コードの経路', { exact: true });
    await variant.waitFor();
    const readBody = async value => {
        await variant.selectOption(value);
        const flow = plan.getByRole('region', { name: '生成C#の制御フロー', exact: true });
        await flow.locator('.小案内').first().filter({ hasText: `__${value}` }).waitFor();
        check((await flow.locator('.小案内').first().innerText()).includes('Available'), `${value} typed CFG available`);
        const text = flow.getByRole('textbox', { name: '最終生成C#本文', exact: true });
        if (!await text.count()) await flow.getByRole('button', { name: '最終生成本文の表示を切り替え', exact: true }).click();
        await text.waitFor();
        const original = await text.evaluate(input => {
            const code = input.closest('.コード編集枠')?.querySelector('pre code');
            if (!code || !code.textContent.endsWith('\n')) throw new Error('Original generated code text unavailable');
            return code.textContent.slice(0, -1);
        });
        check(original.replace(/\r\n?/g, '\n') === await text.inputValue(), `${value} textarea is the normalized original body`);
        const hash = createHash('sha256').update(original, 'utf8').digest('hex').toUpperCase();
        check((await flow.innerText()).includes(hash), `${value} original body matches its displayed hash`);
        if (value === 'Normal') check(!original.includes('QueryProfiler') && !original.includes('QueryProfileRegion'), 'Normal body has no profiling hooks');
        else check(original.includes('.Enter(') && original.includes('.Input(') && original.includes('.Output('), 'Profiled body contains actual region and quantity hooks');
        await page.screenshot({ path: path.join(artifactDirectory, `node-profile-${value.toLowerCase()}.png`), fullPage: true });
        return hash;
    };
    const normalHash = await readBody('Normal');
    const profiledHash = await readBody('Profiled');
    check(normalHash !== profiledHash, 'Normal and Profiled bodies have distinct actual hashes');
    await variant.selectOption('Auto');
    await plan.getByRole('region', { name: '生成C#の制御フロー', exact: true }).locator('.小案内').first().filter({ hasText: '__Profiled' }).waitFor();

    await plan.getByLabel('構造の段階', { exact: true }).selectOption('Logical');
    await plan.locator('[data-表示方式="Gantt"]').click();
    const gantt = plan.getByRole('region', { name: '実行タイムライン', exact: true });
    await gantt.locator(`[data-実行="${executionId}"]`).waitFor();
    const intervalId = instanceId.replace('profile:', 'profile-region:');
    await gantt.locator(`g[data-区間="${intervalId}"]`).waitFor();
    check(await gantt.locator('.ガント時計').count() === 1, 'Whole query and region retain one execution-relative clock');
    check(await executionIdFrom(plan) === executionId, 'Display changes did not replace the execution');
    await page.screenshot({ path: path.join(artifactDirectory, 'node-profile-gantt.png'), fullPage: true });
    await active.getByRole('tab', { name: 'データ', exact: true }).click();
    check(await executionIdFrom(result) === executionId && (await returned.innerText()).trim() === '5', 'View changes retain the original run and return value');
    const report = { nodeProfile: true, objectId: 'profile-sum', executionId, nodeId, instanceId, inputCount: 2, outputCount: 1, normalHash, profiledHash, scope: 'FusedPipelineInclusive', displayChangesReusedExecution: true };
    await writeFile(path.join(artifactDirectory, 'node-profile.json'), JSON.stringify(report, null, 2) + '\n');
    return report;
}
