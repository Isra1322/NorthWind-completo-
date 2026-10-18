// Run against an existing headless Chromium debugging endpoint and the local Blazor app.
import assert from 'node:assert/strict';

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
try {
    await send('Page.navigate', { url: app + '/user/register' });
    const deadline = Date.now() + 45000;
    while (!await evaluate("Boolean(document.querySelector('#registrationFirstName'))")) {
        assert.ok(Date.now() < deadline, 'Registration page must load');
        await new Promise(resolve => setTimeout(resolve, 250));
    }
    for (const [field, limit, text] of [
        ['FirstName', 25, 'A'.repeat(25)],
        ['LastName', 25, 'B'.repeat(25)],
        ['Email', 50, 'a'.repeat(38) + '@example.com']
    ]) {
        const selector = '#registration' + field;
        assert.equal(await evaluate(`document.querySelector(${JSON.stringify(selector)}).maxLength`), limit);
        await evaluate(`document.querySelector(${JSON.stringify(selector)}).focus()`);
        for (const char of text) await send('Input.dispatchKeyEvent', { type: 'char', text: char });
        assert.equal(await evaluate(`document.querySelector(${JSON.stringify(selector)}).value`), text);
        await send('Input.dispatchKeyEvent', { type: 'char', text: 'X' });
        assert.equal(await evaluate(`document.querySelector(${JSON.stringify(selector)}).value.length`), limit);
        await evaluate(`document.querySelector(${JSON.stringify(selector)}).select()`);
        await send('Input.insertText', { text: 'Z' + text });
        assert.equal(await evaluate(`document.querySelector(${JSON.stringify(selector)}).value.length`), limit);
        console.log('PASS Browser typing and pasted text enforce ' + field + ' max ' + limit);
    }
    for (const [field, value, message] of [
        ['firstName', 'A'.repeat(26), 'El nombre no puede superar los 25 caracteres.'],
        ['lastName', 'B'.repeat(26), 'Los apellidos no pueden superar los 25 caracteres.'],
        ['email', 'a'.repeat(39) + '@example.com', 'El correo electrónico no puede superar los 50 caracteres.']
    ]) {
        const body = { firstName: 'Valid', lastName: 'Name', email: 'length-test@example.com', password: 'Valid123!', [field]: value };
        const response = await fetch(api + '/user/register', {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
        });
        assert.equal(response.status, 400);
        const problem = await response.json();
        assert.equal(problem.errors.length, 1);
        assert.equal(problem.errors[0].message, message);
        console.log('PASS Direct HTTP API rejects excessive ' + field + ' with one Spanish validation error');
    }
} finally {
    socket.close();
    await fetch(debug + '/json/close/' + target.id);
}
