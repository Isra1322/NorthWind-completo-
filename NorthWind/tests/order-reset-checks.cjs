const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require('../artifacts/verification/node_modules/playwright');
const root = process.cwd(), children = [], ids = [];
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const marker = 'Reset check ' + Date.now();
let browser, testPage, logs = '';
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
    throw new Error('Server readiness timeout: ' + logs);
}
const sql = (database, statement) => execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', database, '-E', '-b', '-h', '-1', '-Q', 'SET NOCOUNT ON; ' + statement], { encoding: 'utf8' }).trim();
async function check(name, work) { await work(); console.log('PASS ' + name); }
const fields = ['customerId', 'shipAddress', 'shipCity', 'shipCountry', 'shipPostalCode'];
const readFields = page => Promise.all(fields.map(id => page.locator('#' + id).inputValue()));
const stock = id => Number(sql('NorthWindDB', `SELECT UnitsInStock FROM Products WHERE Id=${id}`));
function number(text) {
    const match = text.match(/-?\d[\d,.]*/)[0];
    return Number(match.lastIndexOf(',') > match.lastIndexOf('.') ? match.replaceAll('.', '').replace(',', '.') : match.replaceAll(',', ''));
}
(async () => {
    const beforeCount = Number(sql('NorthWindDB', 'SELECT COUNT(*) FROM Orders'));
    const previousMax = Number(sql('NorthWindDB', 'SELECT MAX(Id) FROM Orders'));
    const fingerprint = table => sql('NorthWindDB', `SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM ${table} WHERE ${table === 'Orders' ? 'Id' : 'OrderId'} <= ${previousMax} ORDER BY ${table === 'Orders' ? 'Id' : 'OrderId,ProductId'} FOR JSON PATH,INCLUDE_NULL_VALUES)),2)`);
    const ordersBefore = fingerprint('Orders'), detailsBefore = fingerprint('OrderDetails');
    const stocksBefore = [stock(1), stock(3)];
    assert.ok(stocksBefore[0] >= 2 && stocksBefore[1] >= 1, 'Existing catalog needs stock for two real test sales');
    const email = sql('NorthWindUsersDB', "SELECT TOP(1) Email FROM AspNetUsers WHERE Email LIKE 'registro-ui-%@northwind.com' ORDER BY Email DESC");
    assert.ok(email.includes('@northwind.com'), 'The retained registration-test account is required for real login');
    const api = start(['bin/Debug/net8.0/NorthWind.Sales.WebApi.dll'], path.join(root, 'NorthWind.Sales.WebApi'), {
        ASPNETCORE_URLS: 'http://localhost:5275', ASPNETCORE_ENVIRONMENT: 'Development',
        JwtOptions__SecurityKey: crypto.randomBytes(48).toString('hex'), SalesTax__Enabled: 'true', SalesTax__Rate: '0.125'
    });
    await ready('http://localhost:5275/swagger/v1/swagger.json', api);
    const client = start(['run', '--project', 'NorthWind.Sales.BlazorClient', '--no-build', '--no-launch-profile', '--urls', 'http://localhost:5375'], root, { ASPNETCORE_ENVIRONMENT: 'Development' });
    await ready('http://localhost:5375/', client);
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    const context = await browser.newContext();
    await context.route('**/appsettings.json', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ WebApiAddress: 'http://localhost:5275' }) }));
    const page = await context.newPage(), errors = [];
    testPage = page;
    page.on('pageerror', error => errors.push(error.message));
    await check('Real login and JWT allow access to order creation', async () => {
        await page.goto('http://localhost:5375/user/login');
        await page.getByLabel('Correo electrónico', { exact: true }).fill(email);
        await page.getByLabel('Contraseña', { exact: true }).fill('NwRegistro123!');
        await page.getByRole('button', { name: 'Iniciar sesión', exact: true }).click();
        await page.waitForFunction(() => localStorage.getItem('tokens') !== null);
        await page.goto('http://localhost:5375/createorder');
        await page.getByRole('heading', { level: 1 }).waitFor();
    });
    let requests = 0;
    await page.route('**/CreateOrder', async route => { requests++; await delay(600); await route.continue(); });
    async function chooseCustomer(id) {
        await page.getByRole('button', { name: 'Buscar cliente', exact: true }).click();
        await page.getByRole('dialog').getByLabel('Código', { exact: true }).fill(id);
        await page.getByRole('button', { name: 'Seleccionar ' + id, exact: true }).click();
    }
    async function chooseProduct(id) {
        await page.getByRole('button', { name: 'Agregar producto', exact: true }).click();
        await page.getByRole('dialog').getByLabel('Código', { exact: true }).fill(String(id));
        await page.getByRole('button', { name: 'Seleccionar ' + id, exact: true }).click();
    }
    async function shipping() {
        await page.locator('#shipAddress').fill(marker);
        await page.locator('#shipCity').fill('Quito');
        await page.locator('#shipCountry').fill('Ecuador');
        await page.locator('#shipPostalCode').fill('170101');
    }
    async function cleanForm(id) {
        await page.getByText(`Orden ${id} creada.`, { exact: true }).waitFor();
        assert.deepEqual(await readFields(page), ['', '', '', '', '']);
        assert.equal(await page.getByRole('button', { name: 'Quitar', exact: true }).count(), 0);
        const totals = await page.locator('[aria-live="polite"]').innerText();
        assert.deepEqual(totals.split('\n').filter(line => /^(Subtotal|IVA|Total):/.test(line)).map(number), [0, 0, 0]);
        assert.equal(await page.locator('.validation-message, .validation-summary-errors li, input.modified, input.invalid').count(), 0);
        assert.ok(await page.getByRole('button', { name: 'Enviar', exact: true }).isEnabled());
        assert.equal(await page.getByRole('button', { name: 'Nueva orden', exact: true }).count(), 0);
        assert.equal(await page.locator('#invoiceId').count(), 0);
    }
    await check('Stock validation rejects excess quantity without losing the draft', async () => {
        await chooseCustomer('BONAP');
        await shipping();
        await chooseProduct(1);
        await page.getByLabel('Cantidad', { exact: true }).fill(String(stocksBefore[0] + 1));
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        await page.getByText('La cantidad debe ser mayor que cero y no superar el stock disponible.').waitFor();
        assert.equal(await page.locator('#customerId').inputValue(), 'BONAP');
        assert.equal(requests, 0);
        await page.getByLabel('Cantidad', { exact: true }).fill('2');
        await page.locator('#shipPostalCode').focus();
    });
    await check('Backend rejection preserves every field and selected product', async () => {
        const before = await readFields(page);
        const response = page.waitForResponse(r => r.url().endsWith('/CreateOrder') && r.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        assert.equal((await response).status(), 400);
        await page.getByText('Revise los campos indicados. La orden no se guardó.', { exact: true }).waitFor();
        assert.deepEqual(await readFields(page), before);
        assert.equal(await page.getByLabel('Cantidad', { exact: true }).inputValue(), '2');
        assert.equal(await page.locator('.alert-success').count(), 0);
        assert.equal(Number(sql('NorthWindDB', 'SELECT COUNT(*) FROM Orders')), beforeCount);
        assert.deepEqual([stock(1), stock(3)], stocksBefore);
    });
    await check('First real sale resets the form automatically and blocks overlapping sends', async () => {
        await chooseCustomer('ALFKI');
        const row = await page.locator('table tbody tr').innerText();
        const price = number(row.split('\t')[1]);
        const token = await page.evaluate(() => JSON.parse(localStorage.getItem('tokens')).AccessToken);
        const tax = await (await fetch('http://localhost:5275/sales/tax', { headers: { Authorization: 'Bearer ' + token } })).json();
        const totals = (await page.locator('[aria-live="polite"]').innerText()).split('\n').filter(line => /^(Subtotal|IVA|Total):/.test(line)).map(number);
        assert.equal(totals[0], price * 2);
        const base = Math.round(price * 2 * (1 - tax.discountRate) * 100) / 100;
        assert.equal(totals[1], Math.round(base * tax.rate * 100) / 100);
        assert.equal(totals[2], base + totals[1]);
        const response = page.waitForResponse(r => r.url().endsWith('/CreateOrder') && r.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        await page.waitForFunction(() => document.querySelector('button[type="submit"]').disabled);
        await page.locator('form').evaluate(form => { form.requestSubmit(); form.requestSubmit(); form.requestSubmit(); });
        const result = await response;
        assert.equal(result.status(), 200);
        const id = await result.json(); ids.push(id);
        await cleanForm(id);
        assert.equal(requests, 2);
        assert.equal(stock(1), stocksBefore[0] - 2);
        assert.equal(Number(sql('NorthWindDB', `SELECT COUNT(*) FROM Orders WHERE Id=${id} AND CustomerId='ALFKI' AND ShipAddress='${marker}'`)), 1);
        assert.equal(Number(sql('NorthWindDB', `SELECT Quantity FROM OrderDetails WHERE OrderId=${id} AND ProductId=1`)), 2);
        const invoice = await (await fetch(`http://localhost:5275/sales/invoices/${id}`, { headers: { Authorization: 'Bearer ' + token } })).json();
        assert.equal(invoice.subtotal, totals[0]);
        assert.equal(invoice.taxAmount, totals[1]);
        assert.equal(invoice.total, totals[2]);
        assert.equal(invoice.discountAmount, Math.round(price * 2 * tax.discountRate * 100) / 100);
        assert.equal(Number(sql('NorthWindDB', `SELECT Total FROM Orders WHERE Id=${id}`)), invoice.total);
        await page.getByRole('link', { name: 'Consultar facturas', exact: true }).click();
        await page.waitForURL('http://localhost:5375/invoices');
        await page.locator('#invoiceSearch').fill(String(id));
        await page.getByRole('button', { name: 'Buscar', exact: true }).click();
        await page.getByRole('button', { name: 'Ver PDF de factura ' + id, exact: true }).click();
        const downloadLink = page.getByRole('link', { name: 'Descargar PDF', exact: true });
        await downloadLink.waitFor();
        const src = await page.locator('#invoicePdf').getAttribute('src');
        const bytes = await page.evaluate(async url => [...new Uint8Array(await (await fetch(url)).arrayBuffer())], src);
        const pdf = Buffer.from(bytes).toString('latin1');
        const formatted = value => value.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        assert.ok(pdf.includes(formatted(invoice.total)) && pdf.includes(formatted(invoice.taxAmount)) && pdf.includes('TOTAL A PAGAR'));
        const download = page.waitForEvent('download');
        await downloadLink.click();
        assert.equal((await download).suggestedFilename(), `factura-${id}.pdf`);
        const historical = await (await fetch('http://localhost:5275/sales/invoices/91', { headers: { Authorization: 'Bearer ' + token } })).json();
        assert.equal(historical.id, 91);
        assert.equal(historical.total, 35);
        assert.equal(historical.taxAmount, 0);
        const historicalPdf = await (await fetch('http://localhost:5275/sales/invoices/91/pdf', { headers: { Authorization: 'Bearer ' + token } })).text();
        assert.ok(historicalPdf.includes('FACTURA') && historicalPdf.includes('TOTAL A PAGAR') && historicalPdf.includes('0 %'));
        await page.getByRole('link', { name: 'Crear orden', exact: true }).click();
        await page.waitForURL('http://localhost:5375/createorder');
        await page.locator('#customerId').waitFor();
    });
    await check('Second real sale works without reloading and receives a different identifier', async () => {
        await chooseCustomer('ANATR');
        await shipping();
        await chooseProduct(3);
        const response = page.waitForResponse(r => r.url().endsWith('/CreateOrder') && r.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        const result = await response;
        assert.equal(result.status(), 200);
        const id = await result.json(); ids.push(id);
        assert.notEqual(ids[0], id);
        await cleanForm(id);
        assert.equal(stock(3), stocksBefore[1] - 1);
        assert.equal(requests, 3);
        assert.equal(Number(sql('NorthWindDB', `SELECT COUNT(*) FROM Orders WHERE Id=${id} AND CustomerId='ANATR'`)), 1);
    });
    await check('New order only discards a local draft and preserves stored sales', async () => {
        await chooseCustomer('ALFKI');
        await shipping();
        await chooseProduct(1);
        await page.getByRole('button', { name: 'Nueva orden', exact: true }).click();
        assert.deepEqual(await readFields(page), ['', '', '', '', '']);
        assert.equal(await page.getByRole('button', { name: 'Quitar', exact: true }).count(), 0);
        assert.equal(requests, 3);
        assert.equal(Number(sql('NorthWindDB', 'SELECT COUNT(*) FROM Orders')), beforeCount + 2);
        assert.equal(fingerprint('Orders'), ordersBefore);
        assert.equal(fingerprint('OrderDetails'), detailsBefore);
    });
    await check('Logout still clears the JWT session', async () => {
        await page.getByRole('button', { name: 'Cerrar sesión', exact: true }).click();
        await page.getByRole('dialog', { name: '¿Cerrar sesión?', exact: true }).getByRole('button', { name: 'Sí, cerrar sesión', exact: true }).click();
        await page.waitForURL('http://localhost:5375/user/login');
        assert.equal(await page.evaluate(() => localStorage.getItem('tokens')), null);
        assert.deepEqual(errors, []);
    });
    console.log('Real test orders retained: ' + ids.join(', '));
    console.log('All reset checks passed; existing orders and details are unchanged, and stock changed only through the two normal sales.');
})().catch(async error => {
    console.error(error);
    console.error('Saved test order IDs: ' + ids.join(', '));
    if (testPage) console.error(await testPage.locator('body').innerText());
    process.exitCode = 1;
}).finally(async () => {
    if (browser) await browser.close();
    for (const child of children.reverse()) if (child.exitCode === null)
        await new Promise(resolve => { child.once('exit', resolve); child.kill(); });
});
