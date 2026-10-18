const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require('../artifacts/verification/node_modules/playwright');
const root = process.cwd();
const children = [];
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const email = `registro-ui-${Date.now()}@northwind.com`;
const password = 'NwRegistro123!';
let browser;
let logs = '';
function start(args, cwd, env) {
    const child = spawn('dotnet', args, { cwd, env: { ...process.env, ...env }, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    for (const pipe of [child.stdout, child.stderr]) pipe.on('data', data => { logs = (logs + data).slice(-10000); });
    children.push(child);
    return child;
}
async function ready(url, child) {
    for (let i = 0; i < 100; i++) {
        if (child.exitCode !== null) throw new Error('Server stopped: ' + logs);
        try { if ((await fetch(url)).ok) return; } catch { }
        await delay(200);
    }
    throw new Error('Server readiness timeout: ' + logs);
}
const sql = (database, statement) => execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', database, '-E', '-b', '-h', '-1', '-Q', 'SET NOCOUNT ON; ' + statement], { encoding: 'utf8' }).trim();
async function check(name, work) { await work(); console.log('PASS ' + name); }
const fields = ['registrationFirstName', 'registrationLastName', 'registrationEmail', 'registrationPassword', 'registrationPasswordConfirmation'];
async function fill(page, values) {
    for (let i = 0; i < fields.length; i++) await page.locator('#' + fields[i]).fill(values[i]);
    await page.locator('#registrationFirstName').focus();
}
async function values(page) { return Promise.all(fields.map(id => page.locator('#' + id).inputValue())); }
async function cleanText(page) {
    const text = await page.locator('body').innerText();
    assert.ok(!/Ã[\u0080-\u00bf]|Â[¿¡]|�/u.test(text), 'Visible text contains mojibake');
}
(async () => {
    const beforeOrders = Number(sql('NorthWindDB', 'SELECT COUNT(*) FROM Orders'));
    const api = start(['bin/Debug/net8.0/NorthWind.Sales.WebApi.dll'], path.join(root, 'NorthWind.Sales.WebApi'), {
        ASPNETCORE_URLS: 'http://localhost:5265', ASPNETCORE_ENVIRONMENT: 'Development',
        JwtOptions__SecurityKey: crypto.randomBytes(48).toString('hex')
    });
    await ready('http://localhost:5265/swagger/v1/swagger.json', api);
    const client = start(['run', '--project', 'NorthWind.Sales.BlazorClient', '--no-build', '--no-launch-profile', '--urls', 'http://localhost:5365'], root, { ASPNETCORE_ENVIRONMENT: 'Development' });
    await ready('http://localhost:5365/', client);
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    const context = await browser.newContext();
    await context.route('**/appsettings.json', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ WebApiAddress: 'http://localhost:5265' }) }));
    const page = await context.newPage();
    const pageErrors = [];
    page.on('pageerror', error => pageErrors.push(error.message));
    let registrations = 0;
    await page.route('**/user/register', async route => {
        if (route.request().method() !== 'POST') return route.continue();
        registrations++;
        await delay(600);
        await route.continue();
    });
    await page.goto('http://localhost:5365/user/register');
    await page.getByRole('heading', { name: 'Registro de usuarios', exact: true }).waitFor();
    await check('Registration labels and HTML encoding are correct in the running application', async () => {
        for (const label of ['Correo electrónico', 'Contraseña', 'Confirmar contraseña'])
            assert.equal(await page.getByLabel(label, { exact: true }).count(), 1);
        assert.equal(await page.evaluate(() => document.characterSet), 'UTF-8');
        await cleanText(page);
        assert.equal(await page.locator('form').evaluate(form => form.noValidate), true);
        assert.equal(await page.locator('.validation-summary-errors').count(), 0);
    });
    async function fieldErrors(id) {
        return page.locator('#' + id).locator('..').locator('.validation-message').allTextContents();
    }
    async function uniqueFieldErrors() {
        for (const id of fields) {
            const errors = await fieldErrors(id);
            assert.equal(new Set(errors).size, errors.length, 'Duplicate error for ' + id);
        }
    }
    await check('Empty form shows one required error per field without a general summary', async () => {
        await page.getByRole('button', { name: 'Registrar', exact: true }).click();
        await page.waitForFunction(() => document.querySelectorAll('.validation-message').length === 5);
        for (const id of fields) assert.equal((await fieldErrors(id)).length, 1);
        assert.deepEqual(await fieldErrors('registrationPasswordConfirmation'), ['Debe confirmar la contraseña.']);
        await uniqueFieldErrors();
        assert.equal(registrations, 0);
    });
    await check('Invalid email shows its format error once', async () => {
        await page.locator('#registrationEmail').fill('d');
        await page.locator('#registrationFirstName').focus();
        await page.waitForFunction(() => document.querySelector('#registrationEmail').parentElement.innerText.includes('formato'));
        const errors = await fieldErrors('registrationEmail');
        assert.equal(errors.length, 1);
        assert.ok(errors[0].includes('formato'));
    });
    await check('Incomplete password reports only unmet requirements, without accumulating errors on repeated submits', async () => {
        await page.locator('#registrationPassword').fill('abc');
        await page.locator('#registrationFirstName').focus();
        await page.waitForFunction(() => document.querySelector('#registrationPassword').parentElement.querySelectorAll('.validation-message').length === 4);
        const original = await fieldErrors('registrationPassword');
        assert.equal(new Set(original).size, 4);
        for (let i = 0; i < 4; i++) {
            await page.getByRole('button', { name: 'Registrar', exact: true }).click();
            await page.waitForFunction(() => document.querySelector('#registrationPassword').parentElement.querySelectorAll('.validation-message').length === 4);
        }
        assert.deepEqual(await fieldErrors('registrationPassword'), original);
        await uniqueFieldErrors();
        assert.equal(registrations, 0);
    });
    await check('Correcting fields clears their errors while name and password confirmation rules remain active', async () => {
        await fill(page, ['123', '456', email, password, password]);
        await page.waitForFunction(() => document.querySelector('#registrationFirstName').parentElement.querySelectorAll('.validation-message').length === 1);
        assert.equal((await fieldErrors('registrationFirstName')).length, 1);
        assert.equal((await fieldErrors('registrationLastName')).length, 1);
        assert.deepEqual(await fieldErrors('registrationEmail'), []);
        assert.deepEqual(await fieldErrors('registrationPassword'), []);
        assert.deepEqual(await fieldErrors('registrationPasswordConfirmation'), []);
        await page.locator('#registrationFirstName').fill('José');
        await page.locator('#registrationLastName').fill('Pérez');
        await page.locator('#registrationEmail').focus();
        await page.waitForFunction(() => document.querySelectorAll('.validation-message').length === 0);
    });
    await check('Invalid registration preserves entered values and shows corrected validation text', async () => {
        const invalid = ['José', 'Pérez', 'invalid-ui@northwind.com', 'abc', 'xyz'];
        await fill(page, invalid);
        await page.getByRole('button', { name: 'Registrar', exact: true }).click();
        await page.getByText('Las contraseñas no coinciden.', { exact: true }).first().waitFor();
        assert.deepEqual(await values(page), invalid);
        assert.equal(await page.getByText('Usuario registrado correctamente.', { exact: true }).count(), 0);
        assert.equal(registrations, 0);
        await cleanText(page);
        await page.screenshot({ path: 'artifacts/membership-fix/invalid-registration.png', fullPage: true });
    });
    await check('Real successful registration clears five fields, validation state, and prevents overlapping submissions', async () => {
        await fill(page, ['José', 'Pérez', email, password, password]);
        await page.getByRole('button', { name: 'Registrar', exact: true }).click();
        await page.waitForFunction(() => document.querySelector('button[type="submit"]').disabled);
        await page.locator('form').evaluate(form => { form.requestSubmit(); form.requestSubmit(); form.requestSubmit(); });
        await page.getByText('Usuario registrado correctamente.', { exact: true }).waitFor();
        assert.deepEqual(await values(page), ['', '', '', '', '']);
        // Blazor labels pristine inputs as valid; the existing stylesheet colors only valid.modified inputs.
        assert.equal(await page.locator('.validation-message, .validation-summary-errors li, input.modified, input.invalid').count(), 0);
        assert.equal(registrations, 1);
        assert.ok(await page.getByRole('button', { name: 'Registrar', exact: true }).isEnabled());
        await page.locator('h1').click();
        assert.deepEqual(await values(page), ['', '', '', '', '']);
        await page.getByText('Usuario registrado correctamente.', { exact: true }).waitFor();
        assert.equal(Number(sql('NorthWindUsersDB', `SELECT COUNT(*) FROM AspNetUsers WHERE Email='${email}'`)), 1);
        await cleanText(page);
        await page.screenshot({ path: 'artifacts/membership-fix/successful-registration.png', fullPage: true });
    });
    await check('Backend rejection preserves values, clears the success message and makes only one request', async () => {
        const duplicate = ['José', 'Pérez', email, password, password];
        await fill(page, duplicate);
        await page.getByRole('button', { name: 'Registrar', exact: true }).click();
        await page.locator('.validation-message, .validation-summary-errors li').first().waitFor();
        assert.deepEqual(await values(page), duplicate);
        assert.equal(await page.getByText('Usuario registrado correctamente.', { exact: true }).count(), 0);
        assert.equal(registrations, 2);
        assert.equal(Number(sql('NorthWindUsersDB', `SELECT COUNT(*) FROM AspNetUsers WHERE Email='${email}'`)), 1);
        await cleanText(page);
    });
    await check('Real login still works with the newly registered user and Spanish session labels', async () => {
        await page.goto('http://localhost:5365/user/login');
        await page.getByRole('button', { name: 'Iniciar sesión', exact: true }).waitFor();
        await page.getByLabel('Correo electrónico', { exact: true }).fill(email);
        await page.getByLabel('Contraseña', { exact: true }).fill(password);
        await page.getByRole('button', { name: 'Iniciar sesión', exact: true }).click();
        await page.waitForFunction(() => localStorage.getItem('tokens') !== null);
        await page.goto('http://localhost:5365/createorder');
        await page.getByRole('heading', { level: 1 }).waitFor();
        assert.equal(await page.getByRole('heading', { level: 1 }).innerText(), 'Crear orden de compra');
        await cleanText(page);
        await page.getByRole('button', { name: 'Agregar producto', exact: true }).click();
        await page.getByRole('dialog').waitFor();
        await page.getByRole('button', { name: 'Seleccionar 1', exact: true }).waitFor();
        await cleanText(page);
        await page.getByRole('button', { name: 'Cerrar', exact: true }).click();
        await page.getByRole('link', { name: 'Consultar facturas', exact: true }).click();
        await page.waitForURL('http://localhost:5365/invoices');
        const row = page.getByRole('button', { name: /^Ver PDF de factura / }).first();
        await row.click();
        await page.getByRole('link', { name: 'Descargar PDF', exact: true }).waitFor();
        await cleanText(page);
    });
    await check('Logout and home still work without corrupt Spanish characters', async () => {
        await page.getByRole('button', { name: 'Cerrar sesión', exact: true }).waitFor();
        await cleanText(page);
        await page.getByRole('button', { name: 'Cerrar sesión', exact: true }).click();
        await page.getByRole('dialog', { name: '¿Cerrar sesión?', exact: true }).getByRole('button', { name: 'Sí, cerrar sesión', exact: true }).click();
        await page.waitForURL('http://localhost:5365/user/login');
        assert.equal(await page.evaluate(() => localStorage.getItem('tokens')), null);
        await cleanText(page);
        assert.deepEqual(pageErrors, []);
    });
    assert.equal(Number(sql('NorthWindDB', 'SELECT COUNT(*) FROM Orders')), beforeOrders);
    console.log('User retained in SQL Server: ' + email);
    console.log('All registration and Spanish UI checks passed; no existing users or orders were modified.');
})().catch(error => { console.error(error); console.error('Registration test email: ' + email); process.exitCode = 1; }).finally(async () => {
    if (browser) await browser.close();
    for (const child of children.reverse()) {
        if (child.exitCode === null) await new Promise(resolve => { child.once('exit', resolve); child.kill(); });
    }
});
