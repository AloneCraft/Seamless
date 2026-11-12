import fs from 'node:fs/promises';
import path from 'node:path';

// 所有する隔離ManagerのPageを渡す。DB/ポリシーの保存は実行しない。
export async function verifyManagerContextualAdditions(page, options = {}) {
    const database = options.databaseName ?? 'VisualPlanDemo';
    const sidebar = page.locator('.ナビゲーション');
    const workspace = page.locator('.管理ワークスペース');
    const results = [];
    const guards = [];
    const ownedForms = [];
    const labels = {
        column: /^(列を追加|Add column)$/,
        cancel: /^(取消|Cancel)$/,
        discard: /^(編集を取り消す|Cancel edits|Discard edits)$/,
        permission: /^(権限を追加|Add permission)$/,
        role: /^(新しいロール|New role)$/,
        rule: /^(対象資源の規則を追加|Add resource rule)$/,
        database: /^(データベース|Databases)$/,
        user: /^(利用者|Users)$/,
        auditDatabase: /^(監査のデータベース|Audit database)$/,
        auditUser: /^(監査の利用者|Audit user)$/
    };
    const check = (condition, message) => { if (!condition) throw new Error(message); };
    const record = (name, details = {}) => results.push({ name, outcome: 'Passed', ...details });
    const skip = (name, reason) => results.push({ name, outcome: 'Unavailable', reason });
    const menu = () => page.locator('.操作ポップ[role="dialog"]:visible').last();
    const row = id => sidebar.locator(`[data-対象-id="${id}"]`).first();
    const dbRow = sidebar.getByRole('group', { name: database, exact: true });
    async function openPlus(target) {
        await target.hover();
        await target.locator(':scope > .操作帯 > button[aria-haspopup="menu"]').first().click();
        await menu().waitFor({ state: 'visible' });
        return menu();
    }
    async function closeMenu() { await menu().getByRole('textbox').press('Escape'); await page.locator('.操作ポップ[role="dialog"]:visible').waitFor({ state: 'hidden' }); }
    async function guardSubmit(form) {
        const handle = await form.elementHandle();
        const key = `seamlessContextualProbe${Date.now()}${guards.length}`;
        await handle.evaluate((element, name) => {
            const state = { count: 0 };
            state.listener = event => { state.count++; event.preventDefault(); event.stopImmediatePropagation(); };
            element[name] = state; element.addEventListener('submit', state.listener, true);
        }, key);
        guards.push({ handle, key });
        return async () => await handle.evaluate((element, name) => element[name].count, key);
    }
    let failed;
    try {
        check(await workspace.getAttribute('data-未保管') === 'false', 'The isolated page must start without unsaved work.');
        check(await page.locator('.操作ポップ[role="dialog"]:visible').count() === 0, 'Close existing menus before this owned probe.');
        await dbRow.click();
        const table = row(`table:${options.tableName ?? 'Items'}`);
        await table.waitFor({ state: 'visible' });
        await (await openPlus(table)).getByRole('menuitem', { name: labels.column }).click();
        const card = page.locator('.表入力カード');
        await card.waitFor({ state: 'visible' }); ownedForms.push(card);
        const names = card.getByRole('textbox', { name: /^(列名|Column name)$/ });
        const count = await names.count();
        await names.last().fill('ContextualPendingColumn');
        await card.locator('input[role="combobox"]').last().fill('decimal');
        await (await openPlus(table)).getByRole('menuitem', { name: labels.column }).click();
        await names.nth(count).waitFor({ state: 'visible' });
        check(await names.count() === count + 1, 'The repeated table addition did not append a column.');
        check(await names.nth(count - 1).inputValue() === 'ContextualPendingColumn', 'The existing column draft was reset.');
        check(await card.locator('input[role="combobox"]').nth(count - 1).inputValue() === 'decimal', 'The existing type draft was reset.');
        await card.getByRole('button', { name: labels.cancel, exact: true }).click();
        await card.waitFor({ state: 'hidden' });
        check(await workspace.getAttribute('data-未保管') === 'false', 'Cancelling the column editor left a changed DB draft.');
        record('table-column-repeat-cancel', { appendedColumns: 2 });
        await openPlus(table);
        await menu().getByRole('textbox').press('ArrowDown');
        await menu().locator('[role="menuitem"]:focus').waitFor({ state: 'visible' });
        check(await menu().getByRole('menuitem').first().evaluate(element => element === document.activeElement), 'ArrowDown did not focus the first command.');
        await menu().getByRole('menuitem').first().press('Escape');
        await page.locator('.操作ポップ[role="dialog"]:visible').waitFor({ state: 'hidden' });
        record('table-keyboard-escape');

        const system = row('system');
        const systemMenu = await openPlus(system);
        record('system-add-menu', { commands: await systemMenu.getByRole('menuitem').allTextContents() });
        await closeMenu();
        const expansion = system.locator('button.枝の開閉');
        if (await expansion.getAttribute('aria-expanded') !== 'true') await expansion.click();
        await system.locator('button.枝の開閉[aria-expanded="true"]').waitFor({ state: 'visible' });
        for (const id of ['roles', 'settings', 'users', 'audit']) {
            const entry = row(id);
            await entry.waitFor({ state: 'visible' });
            const popup = await openPlus(entry);
            record(`${id}-entry-add-menu`, { commands: await popup.getByRole('menuitem').allTextContents() });
            await closeMenu();
        }

        if (await row('users').count()) {
            await row('users').click();
            const view = page.locator('.利用者管理画面'); await view.waitFor({ state: 'visible' });
            const profiles = view.locator('.利用者一覧 [data-対象-id^="user:"]');
            try { await profiles.first().waitFor({ state: 'visible', timeout: 10000 }); }
            catch { skip('user-form-plus-no-submit', 'No editable user profile in this fixture.'); }
            if (await profiles.count()) {
                await profiles.first().click();
                const form = view.locator('form.利用者編集'); await form.waitFor({ state: 'visible' }); ownedForms.push(form);
                const submissions = await guardSubmit(form);
                const previous = await form.locator('.利用者権限').count();
                await (await openPlus(form.locator('[data-対象-id="editing"]'))).getByRole('menuitem', { name: labels.permission }).click();
                await form.locator('.利用者権限').nth(previous).waitFor({ state: 'visible' });
                check(await form.locator('.利用者権限').count() === previous + 1, 'The user permission row was not added.');
                check(await submissions() === 0, 'The user permission plus submitted the form.');
                await form.getByRole('button', { name: labels.discard }).click();
                await form.locator('.利用者権限').nth(previous).waitFor({ state: 'hidden' });
                check(await form.locator('.利用者権限').count() === previous, 'Discard did not restore user grants.');
                record('user-form-plus-no-submit');
            }
        }

        if (await row('roles').count()) {
            await row('roles').click();
            const view = page.locator('.ロール管理画面'); await view.waitFor({ state: 'visible' });
            const list = view.locator('.ロール一覧');
            try { await list.waitFor({ state: 'visible', timeout: 10000 }); }
            catch { skip('role-rule-plus-no-submit', 'Common authorization is not initialized in this fixture.'); }
            if (await list.isVisible()) {
                const create = await openPlus(list.locator('[data-対象-id="roles"]'));
                const command = create.getByRole('menuitem', { name: labels.role });
                if (await command.isEnabled()) {
                    await command.click();
                    const form = view.locator('.ロール編集 form'); await form.waitFor({ state: 'visible' }); ownedForms.push(form);
                    const submissions = await guardSubmit(form);
                    const previous = await form.locator('.ロール規則').count();
                    await (await openPlus(form.locator('[data-対象-id]'))).getByRole('menuitem', { name: labels.rule }).click();
                    await form.locator('.ロール規則').nth(previous).waitFor({ state: 'visible' });
                    check(await form.locator('.ロール規則').count() === previous + 1, 'The role rule was not added.');
                    check(await submissions() === 0, 'The role rule plus submitted the form.');
                    await form.getByRole('button', { name: labels.discard }).click();
                    record('role-rule-plus-no-submit');
                } else { await closeMenu(); skip('role-rule-plus-no-submit', 'New role cannot be edited in the current fixture state.'); }
            }
        }

        if (await row('audit').count()) {
            await row('audit').click();
            const view = page.locator('.監査画面'); await view.waitFor({ state: 'visible' });
            const header = view.locator('[data-対象-id="audit"]');
            if (await view.getByRole('textbox', { name: labels.auditDatabase }).count() === 0) {
                await (await openPlus(header)).getByRole('menuitem', { name: labels.database }).click();
            }
            const databaseInput = view.getByRole('textbox', { name: labels.auditDatabase });
            const previous = await databaseInput.inputValue(); await databaseInput.fill(database);
            if (await view.getByRole('textbox', { name: labels.auditUser }).count() === 0) {
                await (await openPlus(header)).getByRole('menuitem', { name: labels.user }).click();
            }
            await view.getByRole('textbox', { name: labels.auditUser }).waitFor({ state: 'visible' });
            check(await databaseInput.inputValue() === database, 'Adding another audit condition reset the existing DB condition.');
            await databaseInput.fill(previous);
            record('audit-condition-preserves-existing-input');
        }
    } catch (error) { failed = error.message; results.push({ name: 'probe', outcome: 'Failed', message: error.message }); }
    finally {
        for (const form of ownedForms.reverse()) {
            try { if (await form.isVisible()) { const cancel = form.getByRole('button', { name: labels.discard }).or(form.getByRole('button', { name: labels.cancel, exact: true })); if (await cancel.count() === 1) await cancel.click(); } } catch {}
        }
        for (const { handle, key } of guards) {
            try { await handle.evaluate((element, name) => { element.removeEventListener('submit', element[name].listener, true); delete element[name]; }, key); } finally { await handle.dispose(); }
        }
    }
    const report = { passed: !failed, complete: !results.some(item => item.outcome === 'Unavailable'), physicalInputVerified: false, results };
    if (options.artifactDirectory) {
        const directory = path.resolve(options.artifactDirectory);
        const artifactRoots = ['G:/GitHub/Seamless/Work', 'F:/Seamless/visualization-20261109-0e63c1c7'];
        check(artifactRoots.some(root => directory.toLowerCase().startsWith((path.resolve(root) + path.sep).toLowerCase())), 'Use an owned Manager verification artifact directory.');
        await fs.mkdir(directory, { recursive: true });
        await fs.writeFile(path.join(directory, 'contextual-additions.json'), JSON.stringify(report, null, 2));
        await page.screenshot({ path: path.join(directory, 'contextual-additions.png') });
    }
    return report;
}
