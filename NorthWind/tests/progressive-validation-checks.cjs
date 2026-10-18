const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require('../artifacts/verification/node_modules/playwright');
const root = process.cwd(), children = [];
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
let browser, page, logs = '';
function start(args, cwd, env) {
    const child = spawn('dotnet', args, { cwd, env: { ...process.env, ...env }, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    for (const pipe of [child.stdout, child.stderr]) pipe.on('data', data => { logs = (logs + data).slice(-10000); });
    children.push(child); return child;
}
async function ready(url, child) {
    for (let i = 0; i < 100; i++) {
        if (child.exitCode !== null) throw new Error('Server stopped: ' + logs);
        try { if ((await fetch(url)).ok) return; } catch { }
        await delay(200);
    }
    throw new Error('Server readiness timeout');
}
const sql = (db, statement) => execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', db, '-E', '-b', '-h', '-1', '-Q', 'SET NOCOUNT ON; ' + statement], { encoding: 'utf8' }).trim();
async function check(name, work) { await work(); console.log('PASS ' + name); }
const errorsFor = id => page.locator('#' + id).locator('..').locator('.validation-message').allTextContents();
async function count(id, expected) {
    await page.waitForFunction(({ id, expected }) => document.querySelector('#' + id).parentElement.querySelectorAll('.validation-message').length === expected, { id, expected });
}
async function pristine() {
    assert.equal(await page.locator('.validation-message, .validation-summary-errors, input.invalid').count(), 0);
}
async function blur() { await page.getByRole('heading', { level: 1 }).click(); }
(async () => {
    const beforeOrders = Number(sql('NorthWindDB', 'SELECT COUNT(*) FROM Orders'));
    const email = sql('NorthWindUsersDB', "SELECT TOP(1) Email FROM AspNetUsers WHERE Email LIKE 'registro-ui-%@northwind.com' ORDER BY Email DESC");
    const api = start(['bin/Debug/net8.0/NorthWind.Sales.WebApi.dll'], path.join(root, 'NorthWind.Sales.WebApi'), {
        ASPNETCORE_URLS: 'http://localhost:5285', ASPNETCORE_ENVIRONMENT: 'Development', JwtOptions__SecurityKey: crypto.randomBytes(48).toString('hex')
    });
    await ready('http://localhost:5285/swagger/v1/swagger.json', api);
    const client = start(['run', '--project', 'NorthWind.Sales.BlazorClient', '--no-build', '--no-launch-profile', '--urls', 'http://localhost:5385'], root, { ASPNETCORE_ENVIRONMENT: 'Development' });
    await ready('http://localhost:5385/', client);
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    const context = await browser.newContext();
    await context.route('**/appsettings.json', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ WebApiAddress: 'http://localhost:5285' }) }));
    page = await context.newPage();
    const runtimeErrors = [];
    page.on('pageerror', error => runtimeErrors.push(error.message));
    await check('Registration starts pristine and focus does not validate later fields', async () => {
        await page.goto('http://localhost:5385/user/register');
        await page.getByRole('heading', { name: 'Registro de usuarios', exact: true }).waitFor();
        await pristine();
        await page.locator('#registrationFirstName').focus();
        await pristine();
        await blur();
        await count('registrationFirstName', 1);
        for (const id of ['registrationLastName', 'registrationEmail', 'registrationPassword', 'registrationPasswordConfirmation']) assert.deepEqual(await errorsFor(id), []);
        await page.locator('#registrationFirstName').fill('José');
        await blur();
        await count('registrationFirstName', 0);
        await pristine();
    });
    await check('Michael followed by untouched surnames does not expose later errors', async () => {
        await page.locator('#registrationFirstName').fill('Michael');
        await page.locator('#registrationLastName').focus();
        await pristine();
        await page.locator('#registrationEmail').focus();
        await count('registrationLastName', 1);
        for (const id of ['registrationEmail', 'registrationPassword', 'registrationPasswordConfirmation'])
            assert.deepEqual(await errorsFor(id), []);
        await page.locator('#registrationLastName').fill('Perez');
        await blur();
        await count('registrationLastName', 0);
        await page.goto('http://localhost:5385/user/register');
        await page.locator('#registrationFirstName').fill('Michael');
        await blur();
        await pristine();
    });
    await check('Registration password errors and confirmation dependency are progressive', async () => {
        await page.locator('#registrationPassword').focus();
        await pristine();
        await blur();
        await count('registrationPassword', 1);
        assert.deepEqual(await errorsFor('registrationPasswordConfirmation'), []);
        await page.locator('#registrationPassword').fill('abc');
        await blur();
        await count('registrationPassword', 4);
        assert.equal(new Set(await errorsFor('registrationPassword')).size, 4);
        await page.locator('#registrationPassword').fill('Valid123!');
        await blur();
        await count('registrationPassword', 0);
        await page.locator('#registrationPasswordConfirmation').fill('Valid123!');
        await blur();
        await count('registrationPasswordConfirmation', 0);
        await page.locator('#registrationPassword').fill('Other123!');
        await blur();
        await count('registrationPasswordConfirmation', 1);
        await page.locator('#registrationPasswordConfirmation').fill('Other123!');
        await blur();
        await count('registrationPasswordConfirmation', 0);
        assert.deepEqual(await errorsFor('registrationEmail'), []);
    });
    await check('Registration submit exposes all remaining errors without a summary or duplicates', async () => {
        await page.getByRole('button', { name: 'Registrar', exact: true }).click();
        await count('registrationLastName', 1);
        await count('registrationEmail', 1);
        assert.equal(await page.locator('.validation-summary-errors').count(), 0);
        for (let i = 0; i < 3; i++) await page.getByRole('button', { name: 'Registrar', exact: true }).click();
        await count('registrationLastName', 1);
        await count('registrationEmail', 1);
    });
    await check('Login validates only the blurred field and still authenticates', async () => {
        await page.goto('http://localhost:5385/user/login');
        await page.getByRole('button', { name: 'Iniciar sesión', exact: true }).waitFor();
        await pristine();
        await page.locator('#Email').focus();
        await pristine();
        await blur();
        await count('Email', 1);
        assert.deepEqual(await errorsFor('Password'), []);
        await page.locator('#Email').fill('d');
        await blur();
        await count('Email', 1);
        assert.ok((await errorsFor('Email'))[0].includes('formato'));
        for (let i = 0; i < 3; i++)
            await page.locator('button[type="submit"]').click();
        await count('Email', 1);
        await count('Password', 1);
        assert.equal(await page.locator('.validation-summary-errors').count(), 0);
        await page.locator('#Email').fill(email);
        await page.locator('#Password').fill('NwRegistro123!');
        await blur();
        await count('Email', 0);
        await count('Password', 0);
        await page.getByRole('button', { name: 'Iniciar sesión', exact: true }).click();
        await page.waitForFunction(() => localStorage.getItem('tokens') !== null);
    });
    await check('Order starts pristine; customer selection and address focus do not validate other fields', async () => {
        await page.goto('http://localhost:5385/createorder');
        await page.getByRole('heading', { level: 1 }).waitFor();
        await page.waitForFunction(() => !document.querySelector('button[type="submit"]').disabled);
        await pristine();
        await page.getByRole('button', { name: 'Buscar cliente', exact: true }).click();
        await page.getByRole('dialog').getByLabel('Código', { exact: true }).fill('ALFKI');
        await page.getByRole('button', { name: 'Seleccionar ALFKI', exact: true }).click();
        await page.locator('#shipAddress').focus();
        await pristine();
    });
    await check('Blur of empty address shows only its required error; correcting it removes that error', async () => {
        await blur();
        await count('shipAddress', 1);
        assert.deepEqual(await errorsFor('shipCity'), []);
        assert.deepEqual(await errorsFor('shipCountry'), []);
        assert.equal(await page.locator('.validation-message').count(), 1);
        await page.locator('#shipAddress').fill('Calle 123');
        await blur();
        await count('shipAddress', 0);
        await pristine();
    });
    await check('Incomplete order submit shows required errors by field and near the products table', async () => {
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        await count('shipCity', 1);
        await count('shipCountry', 1);
        assert.equal(await page.locator('.validation-message').count(), 3);
        assert.equal(await page.locator('.validation-summary-errors').count(), 0);
        assert.ok((await errorsFor('shipCity'))[0].includes('especificar'));
        assert.ok((await errorsFor('shipCountry'))[0].includes('especificar'));
        assert.equal(await page.locator('.validation-message').filter({ hasText: /detalle|producto/i }).count(), 1);
        await page.locator('#shipCity').fill('123');
        await blur();
        await count('shipCity', 1);
        assert.ok((await errorsFor('shipCity'))[0].includes('letras'));
        await page.locator('#shipCity').fill('Quito');
        await blur();
        await count('shipCity', 0);
        await count('shipCountry', 1);
    });
    await check('Reset creates a pristine validation state and blur works again', async () => {
        await page.getByRole('button', { name: 'Nueva orden', exact: true }).click();
        await pristine();
        await page.locator('#shipCity').focus();
        await pristine();
        await blur();
        await count('shipCity', 1);
        assert.equal(await page.locator('.validation-message').count(), 1);
    });
    await check('Logout still clears JWT and no orders were created by validation', async () => {
        await page.getByRole('button', { name: 'Cerrar sesión', exact: true }).click();
        await page.getByRole('dialog', { name: '¿Cerrar sesión?', exact: true }).getByRole('button', { name: 'Sí, cerrar sesión', exact: true }).click();
        await page.waitForURL('http://localhost:5385/user/login');
        assert.equal(await page.evaluate(() => localStorage.getItem('tokens')), null);
        assert.equal(Number(sql('NorthWindDB', 'SELECT COUNT(*) FROM Orders')), beforeOrders);
        assert.deepEqual(runtimeErrors, []);
    });
    console.log('All progressive validation browser checks passed.');
})().catch(async error => {
    console.error(error);
    if (page) console.error(await page.locator('body').innerText());
    process.exitCode = 1;
}).finally(async () => {
    if (browser) await browser.close();
    for (const child of children.reverse()) if (child.exitCode === null)
        await new Promise(resolve => { child.once('exit', resolve); child.kill(); });
});
