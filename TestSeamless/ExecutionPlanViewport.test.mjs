import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
const source = await readFile(new URL('../Seamless.Management.Components/wwwroot/execution-plan.js', import.meta.url), 'utf8');
const { 計画グラフを初期化 } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);

function fixture() {
    const frames = new Map(), calls = [], observers = [];
    let frameId = 0;
    globalThis.requestAnimationFrame = callback => { frames.set(++frameId, callback); return frameId; };
    globalThis.cancelAnimationFrame = id => frames.delete(id);
    globalThis.ResizeObserver = globalThis.MutationObserver = class {
        constructor(callback) { this.callback = callback; observers.push(this); }
        observe() {}
        disconnect() { this.disconnected = true; }
    };
    globalThis.DOMPoint = class {
        constructor(x, y) { this.x = x; this.y = y; }
        matrixTransform(matrix) { return { x: this.x * matrix.a + matrix.e, y: this.y * matrix.a + matrix.f }; }
    };
    const viewport = new EventTarget();
    Object.assign(viewport, {
        scrollLeft: 0, scrollTop: 0, clientLeft: 0, clientTop: 0, clientWidth: 800, clientHeight: 600,
        classList: { add() {}, remove() {} }, closest: () => null, matches: () => false,
        hasPointerCapture: () => false, focus() {}, getBoundingClientRect: () => ({ left: 0, top: 0 }),
        querySelectorAll: () => []
    });
    const svg = { getScreenCTM: () => ({ a: 1, e: -viewport.scrollLeft, f: -viewport.scrollTop, inverse: () => ({ a: 1, e: viewport.scrollLeft, f: viewport.scrollTop }) }) };
    viewport.querySelector = () => svg;
    const reference = { invokeMethodAsync(...args) { return new Promise(resolve => calls.push({ args, resolve })); } };
    const api = 計画グラフを初期化(viewport, reference, 1);
    const paint = () => { const callbacks = [...frames.values()]; frames.clear(); callbacks.forEach(callback => callback()); };
    const tick = async () => { await Promise.resolve(); await Promise.resolve(); };
    return { api, viewport, frames, calls, observers, paint, tick };
}

test('camera notifications retain one in-flight request and the latest viewport', async () => {
    const f = fixture();
    try {
        f.paint(); assert.equal(f.calls.length, 1);
        for (let index = 1; index <= 100; index++) {
            f.viewport.scrollTop = index * 256; f.viewport.dispatchEvent(new Event('scroll')); f.paint();
        }
        assert.equal(f.calls.length, 1);
        f.calls[0].resolve(); await f.tick();
        assert.equal(f.calls.length, 2); assert.equal(f.calls[1].args[4], 25600);
        f.calls[1].resolve(); await f.tick();
        f.viewport.dispatchEvent(new Event('scroll')); f.paint(); assert.equal(f.calls.length, 2);
        f.viewport.scrollTop = 30000; f.viewport.dispatchEvent(new Event('scroll')); f.paint();
        assert.equal(f.calls.length, 3);
        f.viewport.scrollTop = 40000; f.viewport.dispatchEvent(new Event('scroll')); f.paint();
        f.api.dispose(); f.calls[2].resolve(); await f.tick();
        assert.equal(f.calls.length, 3); assert.equal(f.frames.size, 0);
        assert.ok(f.observers.every(observer => observer.disconnected));
    } finally { f.api.dispose(); }
});

test('a newer model generation supersedes a pending camera and invalid coordinates are ignored', async () => {
    const f = fixture();
    try {
        f.paint(); f.api.設定を更新(2); f.paint();
        f.calls[0].resolve(); await f.tick();
        assert.equal(f.calls.length, 2); assert.equal(f.calls[1].args[1], 2);
        f.calls[1].resolve(); await f.tick();
        f.viewport.scrollTop = Number.NaN; f.viewport.dispatchEvent(new Event('scroll')); f.paint();
        assert.equal(f.calls.length, 2);
        f.viewport.scrollTop = 0;
        f.api.ノードを表示('outside-dom', 100, 5000, 248, 108);
        assert.equal(f.viewport.scrollTop, 4754);
    } finally { f.api.dispose(); }
});

test('keyboard navigation is bounded and stops after disposal', async () => {
    const f = fixture();
    const key = key => { const event = new Event('keydown', { cancelable: true }); Object.defineProperty(event, 'key', { value: key }); f.viewport.dispatchEvent(event); };
    try {
        key('ArrowDown'); key('ArrowDown'); key('ArrowUp');
        assert.equal(f.calls.length, 1); assert.equal(f.calls[0].args[2], 1);
        f.calls[0].resolve(); await f.tick();
        assert.equal(f.calls.length, 2); assert.equal(f.calls[1].args[2], -1);
        f.api.dispose(); f.calls[1].resolve(); await f.tick(); key('ArrowDown');
        assert.equal(f.calls.length, 2);
    } finally { f.api.dispose(); }
});
