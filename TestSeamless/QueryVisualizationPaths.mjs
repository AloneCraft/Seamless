import path from 'node:path';
import { pathToFileURL } from 'node:url';

export function browserArtifactPaths(artifactDirectory, downloadOverride, standaloneOverride) {
    const downloadPath = downloadOverride ?? path.resolve(artifactDirectory);
    if (typeof downloadPath !== 'string' || /[\x00-\x1f]/.test(downloadPath) || !downloadPath || downloadPath.startsWith('\\\\') || downloadPath.startsWith('//')) throw new Error('Use a local absolute browser download path.');
    if (!path.posix.isAbsolute(downloadPath) && !path.win32.isAbsolute(downloadPath)) throw new Error('The browser download path must be absolute.');
    const directory = downloadPath.replaceAll('\\', '/').replace(/\/+$/, '');
    if (directory.split('/').some(part => part === '.' || part === '..')) throw new Error('The browser download path must not contain dot segments.');
    const standalone = new URL(standaloneOverride ?? pathToFileURL(path.join(path.resolve(artifactDirectory), 'plan.html')).href);
    if (standalone.protocol !== 'file:' || standalone.hostname || standalone.username || standalone.password || standalone.search || standalone.hash || /%2f|%5c/i.test(standalone.pathname)) throw new Error('Use a local file URL without a host, query, or fragment for the exported plan.');
    let actual = decodeURIComponent(standalone.pathname);
    const expected = directory + '/plan.html';
    if (/^[a-z]:\//i.test(expected)) {
        actual = actual.replace(/^\/(?=[a-z]:\/)/i, '');
        if (actual.toLowerCase() !== expected.toLowerCase()) throw new Error('The standalone HTML URL must identify plan.html in the browser download directory.');
    } else if (actual !== expected) throw new Error('The standalone HTML URL must identify plan.html in the browser download directory.');
    return { downloadPath, standaloneHtmlUrl: standalone.href };
}
