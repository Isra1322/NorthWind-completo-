// Run against an existing headless Chromium debugging endpoint and the local Blazor app.
import assert from 'node:assert/strict';
import { createHmac } from 'node:crypto';

const debug = process.env.BROWSER_DEBUG_URL ?? 'http://127.0.0.1:9228';
const app = process.env.BLAZOR_TEST_URL ?? 'http://127.0.0.1:5189';
const api = process.env.API_TEST_URL ?? 'http://127.0.0.1:5188';
const target = await (await fetch(debug + '/json/new?about:blank', { method: 'PUT' })).json();
const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
let nextId = 0;
const pending = new Map();
socket.onmessage = ({ data }) => {
    const response = JSON.parse(data);
    const callback = pending.get(response.id);
    if (callback) {
        pending.delete(response.id);
        response.error ? callback.reject(new Error(JSON.stringify(response.error))) : callback.resolve(response.result);
    }
};
function send(method, params = {}) {
    const id = ++nextId;
    return new Promise((resolve, reject) => {
        pending.set(id, { resolve, reject });
        socket.send(JSON.stringify({ id, method, params }));
    });
}
async function evaluate(expression) {
    const result = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
    return result.result.value;
}
const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
const unsigned = encode({ alg: 'HS256', typ: 'JWT' }) + '.' + encode({
    iss: 'DeliveryChecks', aud: 'DeliveryChecks', name: 'Delivery checks',
    nbf: Math.floor(Date.now() / 1000) - 30, exp: Math.floor(Date.now() / 1000) + 600
});
const token = unsigned + '.' + createHmac('sha256', 'delivery-check-'.padEnd(96, 'x')).update(unsigned).digest('base64url');
async function focus(id, select = false) {
    await evaluate(`document.getElementById('${id}').focus(); ${select ? "document.getElementById('" + id + "').select();" : ''}`);
}
const value = id => evaluate(`document.getElementById('${id}').value`);
const pause = () => new Promise(resolve => setTimeout(resolve, 80));
try {
    await send('Page.enable');
    await send('Page.addScriptToEvaluateOnNewDocument', { source:
        `localStorage.setItem('tokens', ${JSON.stringify(JSON.stringify({ AccessToken: token }))});` });
    await send('Page.navigate', { url: app + '/createorder' });
    const deadline = Date.now() + 45000;
    while (!await evaluate("Boolean(document.querySelector('#shipAddress'))")) {
        assert.ok(Date.now() < deadline, 'Order page must load: ' + await evaluate('document.body.innerText'));
        await new Promise(resolve => setTimeout(resolve, 250));
    }
    for (const [id, limit, text] of [
        ['shipAddress', 60, 'A'.repeat(60)], ['shipCity', 15, 'B'.repeat(15)],
        ['shipCountry', 15, 'C'.repeat(15)], ['shipPostalCode', 10, '0123456789']
    ]) {
        await focus(id);
        assert.equal(await evaluate(`document.getElementById('${id}').maxLength`), limit);
        for (const char of text) { await send('Input.dispatchKeyEvent', { type: 'char', text: char }); await pause(); }
        assert.equal(await value(id), text);
        await send('Input.dispatchKeyEvent', { type: 'char', text: '9' });
        await pause();
        assert.equal(await value(id), text);
        await focus(id, true);
        await send('Input.insertText', { text: text + '999' });
        await pause();
        assert.equal(await value(id), text);
        console.log('PASS typing and pasted text: ' + id + ' max ' + limit);
    }
    await focus('shipPostalCode', true);
    await send('Input.insertText', { text: '0a1-2 3.e+' });
    await pause();
    assert.equal(await value('shipPostalCode'), '0123');
    for (const text of ['A', ' ', '-', '.', ',', '+', 'e', '٠']) {
        await send('Input.dispatchKeyEvent', { type: 'char', text });
        await pause();
        assert.equal(await value('shipPostalCode'), '0123');
    }
    await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Backspace', windowsVirtualKeyCode: 8 });
    await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Backspace', windowsVirtualKeyCode: 8 });
    await pause();
    assert.equal(await value('shipPostalCode'), '012');
    await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Home', windowsVirtualKeyCode: 36 });
    await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'ArrowRight', windowsVirtualKeyCode: 39 });
    await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Delete', windowsVirtualKeyCode: 46 });
    await pause();
    assert.equal(await value('shipPostalCode'), '02');
    await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Tab', windowsVirtualKeyCode: 9 });
    assert.notEqual(await evaluate('document.activeElement.id'), 'shipPostalCode');
    console.log('PASS postal ASCII filtering, leading zero, mixed paste, Backspace, Delete, arrows and Tab');

    const headers = { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' };
    for (const [field, invalid] of [
        ['shipAddress', 'A'.repeat(61)], ['shipCity', 'B'.repeat(16)],
        ['shipCountry', 'C'.repeat(16)], ['shipPostalCode', '01234567890'],
        ...['A', ' ', '-1', '1.2', '1,2', '+1', '1e2', '٠١٢'].map(v => ['shipPostalCode', v])
    ]) {
        const body = { customerId: 'ALFKI', shipAddress: 'Calle', shipCity: 'Quito', shipCountry: 'Ecuador',
            shipPostalCode: '0123456789', orderDetails: [{ productId: 1, unitPrice: 1, quantity: 1 }], [field]: invalid };
        const response = await fetch(api + '/CreateOrder', { method: 'POST', headers, body: JSON.stringify(body) });
        assert.equal(response.status, 400, field + ' must fail validation before stock/price processing');
        const problem = JSON.stringify(await response.json());
        assert.ok(problem.toLowerCase().includes(field.toLowerCase()), problem);
        assert.ok(!problem.includes('SqlException'), problem);
        console.log('PASS HTTP API rejects ' + field + ': ' + JSON.stringify(invalid));
    }
} finally {
    socket.close();
    await fetch(debug + '/json/close/' + target.id);
}
