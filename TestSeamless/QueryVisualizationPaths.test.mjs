import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { browserArtifactPaths } from './QueryVisualizationPaths.mjs';

test('native defaults preserve the Node artifact directory and its file URL', () => {
    const directory = path.resolve('owned-artifacts');
    assert.deepEqual(browserArtifactPaths(directory), { downloadPath: directory, standaloneHtmlUrl: pathToFileURL(path.join(directory, 'plan.html')).href });
});

test('explicit Linux browser paths never derive or mutate the Node-side directory', () => {
    const directory = 'F:\\Seamless\\owned-artifacts';
    assert.deepEqual(browserArtifactPaths(directory, '/mnt/f/Seamless/owned-artifacts', 'file:///mnt/f/Seamless/owned-artifacts/plan.html'), { downloadPath: '/mnt/f/Seamless/owned-artifacts', standaloneHtmlUrl: 'file:///mnt/f/Seamless/owned-artifacts/plan.html' });
    assert.equal(directory, 'F:\\Seamless\\owned-artifacts');
});

test('explicit Windows file URLs and encoded spaces identify the same local file', () => {
    assert.equal(browserArtifactPaths('unused', 'F:\\Seamless\\owned artifacts', 'file:///F:/Seamless/owned%20artifacts/plan.html').downloadPath, 'F:\\Seamless\\owned artifacts');
});

test('network, relative, mismatched, and executable URLs are rejected before browser access', () => {
    for (const download of ['relative', '\\\\server\\share', '//server/share', '/tmp/../elsewhere', '/tmp/\u0000bad']) assert.throws(() => browserArtifactPaths('unused', download, 'file:///tmp/plan.html'));
    for (const url of ['https://example.invalid/plan.html', 'javascript:alert(1)', 'file://server/tmp/plan.html', 'file:///tmp/other.html', 'file:///tmp/plan.html?x=1', 'file:///tmp/plan.html#x', 'file:///tmp/other/plan.html', 'file:///tmp%2f/plan.html']) assert.throws(() => browserArtifactPaths('unused', '/tmp', url));
});
