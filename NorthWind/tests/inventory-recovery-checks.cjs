const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require('../artifacts/verification/node_modules/playwright');
const root = process.cwd();
const database = 'NorthWindInventoryCheck_' + crypto.randomBytes(16).toString('hex');
const helper = path.join(root, 'tests/NorthWind.Sales.RequirementsChecks/bin/Debug/net8.0/NorthWind.Sales.RequirementsChecks.dll');
const key = crypto.randomBytes(48).toString('hex');
const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
const header = encode({ alg: 'HS256', typ: 'JWT' });
const claims = encode({ iss: 'NorthWindSales', aud: 'NorthWindSalesClient', unique_name: 'inventory-check',
    FullName: 'Prueba de inventario', exp: Math.floor(Date.now() / 1000) + 3600 });
const signature = crypto.createHmac('sha256', key).update(`${header}.${claims}`).digest('base64url');
const token = `${header}.${claims}.${signature}`;
const children = [];
let browser, logs = '';
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
function start(args, cwd, env) {
    const child = spawn('dotnet', args, { cwd, windowsHide: true, env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
    for (const pipe of [child.stdout, child.stderr]) pipe.on('data', data => { logs = (logs + data).slice(-10000); });
    children.push(child);
    return child;
}
async function ready(url, child) {
    for (let i = 0; i < 150; i++) {
        if (child.exitCode !== null) throw new Error('Server stopped: ' + logs);
        try { if ((await fetch(url)).ok) return; } catch { }
        await pause(200);
    }
    throw new Error('Server readiness timeout: ' + logs);
}
const sql = statement => execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', database, '-E', '-b', '-h', '-1', '-Q', 'SET NOCOUNT ON; ' + statement], { encoding: 'utf8', windowsHide: true }).trim();
async function check(name, work) { await work(); console.log('PASS ' + name); }
(async () => {
    execFileSync('dotnet', [helper, '--inventory-fixture-create', database], { stdio: 'pipe', windowsHide: true });
    const connection = `Server=(localdb)\\mssqllocaldb;Database=${database};Integrated Security=true;TrustServerCertificate=true`;
    const api = start(['bin/Debug/net8.0/NorthWind.Sales.WebApi.dll'], path.join(root, 'NorthWind.Sales.WebApi'), {
        ASPNETCORE_URLS: 'http://localhost:5295', ASPNETCORE_ENVIRONMENT: 'Development', JwtOptions__SecurityKey: key,
        DBOptions__ConnectionString: connection, DBOptions__DomainLogsConnectionString: connection.replace(database, database + '_Logs'),
        MembershipDBOptions__ConnectionString: connection
    });
    await ready('http://localhost:5295/swagger/v1/swagger.json', api);
    const client = start(['run', '--project', 'NorthWind.Sales.BlazorClient', '--no-build', '--no-launch-profile', '--urls', 'http://localhost:5395'], root, { ASPNETCORE_ENVIRONMENT: 'Development' });
    await ready('http://localhost:5395/', client);
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    const context = await browser.newContext({ acceptDownloads: true });
    await context.addInitScript(token => {
        if (window === window.top && location.origin === 'http://localhost:5395' && !sessionStorage.getItem('testTokenInstalled')) {
            localStorage.setItem('tokens', JSON.stringify({ AccessToken: token }));
            sessionStorage.setItem('testTokenInstalled', 'true');
        }
    }, token);
    await context.route('**/appsettings.json', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ WebApiAddress: 'http://localhost:5295' }) }));
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto('http://localhost:5395/createorder');
    await page.getByRole('button', { name: 'Enviar', exact: true }).waitFor();
    let requests = 0;
    page.on('request', req => { if (req.url().endsWith('/CreateOrder') && req.method() === 'POST') requests++; });
    await check('Prepare a 20-unit draft with actual stock 20', async () => {
        await page.getByRole('button', { name: 'Buscar cliente', exact: true }).click();
        await page.getByRole('dialog').getByLabel('Código', { exact: true }).fill('ALFKI');
        await page.getByRole('button', { name: 'Seleccionar ALFKI', exact: true }).click();
        await page.locator('#shipAddress').fill('Prueba de concurrencia');
        await page.locator('#shipCity').fill('Quito');
        await page.locator('#shipCountry').fill('Ecuador');
        await page.locator('#shipPostalCode').fill('170101');
        await page.getByRole('button', { name: 'Agregar producto', exact: true }).click();
        await page.getByRole('dialog').getByLabel('Código', { exact: true }).fill('1');
        await page.getByRole('button', { name: 'Seleccionar 1', exact: true }).click();
        await page.getByLabel('Cantidad', { exact: true }).fill('20');
        await page.getByRole('heading', { level: 1 }).click();
        assert.equal(Number(sql('SELECT UnitsInStock FROM Products WHERE Id=1')), 20);
    });
    const draft = await page.locator('form input').evaluateAll(inputs => inputs.map(input => input.value));
    await check('Direct SQL update to zero returns HTTP 409 and releases the same form', async () => {
        sql('UPDATE Products SET UnitsInStock=0 WHERE Id=1');
        const response = page.waitForResponse(r => r.url().endsWith('/CreateOrder') && r.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        const result = await response;
        assert.equal(result.status(), 409);
        const problem = await result.json();
        assert.ok(problem.errors.some(error => error.message.includes('Stock disponible: 0') && error.message.includes('Cantidad solicitada: 20')));
        await page.locator('.alert-danger').filter({ hasText: 'Stock disponible: 0' }).waitFor();
        assert.equal(page.url(), 'http://localhost:5395/createorder');
        assert.ok(await page.getByRole('button', { name: 'Enviar', exact: true }).isEnabled());
        assert.deepEqual(await page.locator('form input').evaluateAll(inputs => inputs.map(input => input.value)), draft);
        assert.equal(Number(sql('SELECT COUNT(*) FROM Orders')), 0);
        assert.equal(Number(sql('SELECT COUNT(*) FROM OrderDetails')), 0);
    });
    let savedId;
    await check('Restore SQL stock to 20 and retry without reload, reselection or duplicate orders', async () => {
        sql('UPDATE Products SET UnitsInStock=20 WHERE Id=1');
        await page.route('**/CreateOrder', async route => { await pause(300); await route.continue(); });
        const response = page.waitForResponse(r => r.url().endsWith('/CreateOrder') && r.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        await page.locator('form').evaluate(form => { form.requestSubmit(); form.requestSubmit(); });
        const result = await response;
        assert.equal(result.status(), 200);
        savedId = await result.json();
        await page.getByText(`Orden ${savedId} creada.`, { exact: true }).waitFor();
        assert.ok(await page.getByRole('button', { name: 'Enviar', exact: true }).isEnabled());
        assert.equal(Number(sql('SELECT COUNT(*) FROM Orders')), 1);
        assert.equal(Number(sql('SELECT UnitsInStock FROM Products WHERE Id=1')), 0);
        assert.equal(Number(sql('SELECT Quantity FROM OrderDetails')), 20);
        assert.equal(Number(sql('SELECT Total FROM Orders')), 207);
        assert.equal(requests, 2);
        assert.equal(await page.locator('.validation-message').count(), 0);
        assert.equal(await page.locator('.alert-danger').count(), 0);
        assert.equal(await page.getByRole('button', { name: 'Quitar', exact: true }).count(), 0);
    });
    await check('Recovered invoice is listed with original totals and PDF can be opened, downloaded and printed', async () => {
        await page.getByRole('link', { name: 'Consultar facturas', exact: true }).click();
        await page.getByRole('button', { name: 'Ver PDF de factura ' + savedId, exact: true }).click();
        const link = page.getByRole('link', { name: 'Descargar PDF', exact: true });
        await link.waitFor();
        const bytes = await page.evaluate(async () => [...new Uint8Array(await (await fetch(document.querySelector('#invoicePdf').src)).arrayBuffer())]);
        const pdf = Buffer.from(bytes).toString('latin1');
        assert.ok(['(200,00)', '(20,00)', '(180,00)', '(27,00)', '(207,00)'].every(value => pdf.includes(value)));
        const download = page.waitForEvent('download');
        await link.click();
        assert.equal((await download).suggestedFilename(), `factura-${savedId}.pdf`);
        await page.evaluate(() => { window.printCalls = 0; document.getElementById('invoicePdf').contentWindow.print = () => window.printCalls++; });
        await page.getByRole('button', { name: 'Imprimir', exact: true }).click();
        await page.waitForFunction(() => window.printCalls === 1);
    });
    await check('JWT logout confirmation and protected pages still work after stock recovery', async () => {
        await page.getByRole('button', { name: 'Cerrar sesión', exact: true }).click();
        await page.getByRole('dialog', { name: '¿Cerrar sesión?', exact: true }).getByRole('button', { name: 'Sí, cerrar sesión', exact: true }).click();
        await page.waitForURL('http://localhost:5395/user/login');
        assert.equal(await page.evaluate(() => localStorage.getItem('tokens')), null);
        await page.goto('http://localhost:5395/createorder');
        await page.getByRole('link', { name: 'Iniciar sesión', exact: true }).waitFor();
        assert.equal(await page.locator('#customerId').count(), 0);
        assert.deepEqual(errors, []);
    });
    console.log('All isolated inventory recovery browser checks passed; no real database was changed.');
})().catch(error => { console.error(error); process.exitCode = 1; }).finally(async () => {
    if (browser) await browser.close();
    for (const child of children.reverse()) if (child.exitCode === null)
        await new Promise(resolve => { child.once('exit', resolve); child.kill(); });
    execFileSync('dotnet', [helper, '--inventory-fixture-delete', database], { stdio: 'pipe', windowsHide: true });
});
