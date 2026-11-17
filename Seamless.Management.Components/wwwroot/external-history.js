// 外部DB の接続文字列の履歴。画面の利用者ごとの鍵で、このブラウザーに残す(外部DBの取り込みの決定2)。
// パスワードは残さない(パスワードを含む接続文字列は保存しない)。
const maximumCount = 20;
const keyPattern = /^Seamless\.Manager\.ExternalConnections\.v1\.[0-9a-f]{32}\.[A-Za-z0-9+/]+=*$/u;

function validate(list) {
    if (!Array.isArray(list) || list.length > maximumCount) throw new Error('Invalid connection history.');
    for (const item of list) {
        if (typeof item !== 'string' || item.length === 0 || item.length > 4096 || /[\u0000-\u001f\u007f-\u009f]/u.test(item)) throw new Error('Invalid connection history entry.');
        if (/(^|;)\s*(password|pwd)\s*=/iu.test(item)) throw new Error('Connection history must not contain a password.');
    }
    return [...list];
}

export function read(key) {
    if (typeof key !== 'string' || !keyPattern.test(key)) throw new Error('Authenticated history key is required.');
    const raw = localStorage.getItem(key);
    if (!raw) return [];
    return validate(JSON.parse(raw));
}

export function write(key, list) {
    if (typeof key !== 'string' || !keyPattern.test(key)) throw new Error('Authenticated history key is required.');
    localStorage.setItem(key, JSON.stringify(validate(list)));
}
