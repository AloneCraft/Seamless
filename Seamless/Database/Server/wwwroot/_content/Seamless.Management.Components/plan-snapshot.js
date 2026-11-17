export function 計画を保存(名前, 種類, 内容) {
    if (!['application/json', 'image/svg+xml', 'text/html'].includes(種類)) throw new Error('Unsupported plan format.');
    const データ = new Blob([内容], { type: `${種類};charset=utf-8` });
    if (データ.size > 2 * 1024 * 1024) throw new Error('Plan snapshot exceeds the size limit.');
    const URL参照 = URL.createObjectURL(データ);
    const リンク = document.createElement('a');
    リンク.href = URL参照;
    リンク.download = 名前;
    document.body.append(リンク);
    try { リンク.click(); }
    finally {
        リンク.remove();
        setTimeout(() => URL.revokeObjectURL(URL参照), 1000);
    }
}
