const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require('../artifacts/verification/node_modules/playwright');
const path = require('node:path');
const root = process.cwd();
const skipDownloads = process.env.NORTHWIND_SKIP_DOWNLOAD === '1';
const key = crypto.randomBytes(48).toString('hex');
const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
const header = encode({ alg: 'HS256', typ: 'JWT' });
const claims = encode({ iss: 'NorthWindSales', aud: 'NorthWindSalesClient', unique_name: 'requirements-check',
    FullName: 'Requirements Check', exp: Math.floor(Date.now() / 1000) + 3600 });
const signature = crypto.createHmac('sha256', key).update(`${header}.${claims}`).digest('base64url');
const token = `${header}.${claims}.${signature}`;
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const children = [];
let browser;
let logs = '';
function start(args, cwd, env) {
    const child = spawn('dotnet', args, { cwd, env: { ...process.env, ...env }, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    for (const pipe of [child.stdout, child.stderr]) pipe.on('data', data => { logs = (logs + data).slice(-12000); });
    children.push(child);
    return child;
}
async function ready(url, child) {
    for (let i = 0; i < 100; i++) {
        if (child.exitCode !== null) throw new Error('Server stopped: ' + logs);
        try { if ((await fetch(url)).ok) return; } catch { }
        await delay(200);
    }
    throw new Error('Server did not start: ' + logs);
}
async function check(name, work) { await work(); console.log('PASS ' + name); }
async function request(route, data, authenticated = true) {
    return fetch('http://localhost:5255' + route, {
        method: data === undefined ? 'GET' : 'POST',
        headers: { 'Content-Type': 'application/json', ...(authenticated ? { Authorization: 'Bearer ' + token } : {}) },
        ...(data === undefined ? {} : { body: JSON.stringify(data) })
    });
}
const sql = query => execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', 'NorthWindDB', '-E', '-h', '-1', '-b', '-Q', 'SET NOCOUNT ON; ' + query], { encoding: 'utf8' }).trim();
(async () => {
    const before = Number(sql('SELECT COUNT(*) FROM Orders'));
    const existingId = Number(sql('SELECT TOP(1) Id FROM Orders ORDER BY Id'));
    const unconfiguredApi = start(['bin/Debug/net8.0/NorthWind.Sales.WebApi.dll'], path.join(root, 'NorthWind.Sales.WebApi'), {
        ASPNETCORE_URLS: 'http://localhost:5256', ASPNETCORE_ENVIRONMENT: 'Development',
        JwtOptions__SecurityKey: key, SalesTax__Enabled: 'true', SalesTax__Rate: ''
    });
    await ready('http://localhost:5256/swagger/v1/swagger.json', unconfiguredApi);
    await check('Missing tax configuration blocks tax calculation while historical invoices remain accessible', async () => {
        const headers = { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' };
        assert.equal((await fetch('http://localhost:5256/sales/tax', { headers })).status, 500);
        assert.equal((await fetch('http://localhost:5256/sales/invoices/91/pdf', { headers })).status, 200);
        const products = await (await fetch('http://localhost:5256/sales/products?id=1', { headers })).json();
        const product = products.items[0];
        const response = await fetch('http://localhost:5256/CreateOrder', { method: 'POST', headers,
            body: JSON.stringify({ customerId: 'ALFKI', shipAddress: 'Calle 123', shipCity: 'Quito', shipCountry: 'Ecuador', shipPostalCode: '',
                orderDetails: [{ productId: 1, unitPrice: product.unitPrice, quantity: 1 }] }) });
        assert.equal(response.status, 500);
        assert.equal(Number(sql('SELECT COUNT(*) FROM Orders')), before);
        assert.equal(Number(sql('SELECT UnitsInStock FROM Products WHERE Id=1')), product.unitsInStock);
    });
    const api = start(['bin/Debug/net8.0/NorthWind.Sales.WebApi.dll'], path.join(root, 'NorthWind.Sales.WebApi'), {
        ASPNETCORE_URLS: 'http://localhost:5255', ASPNETCORE_ENVIRONMENT: 'Development',
        JwtOptions__SecurityKey: key
    });
    await ready('http://localhost:5255/swagger/v1/swagger.json', api);
    await check('Sales endpoints require JWT', async () => {
        for (const route of ['/sales/products', '/sales/customers', '/sales/tax', '/sales/invoices', `/sales/invoices/${existingId}`, `/sales/invoices/${existingId}/pdf`])
            assert.equal((await request(route, undefined, false)).status, 401);
    });
    await check('API search and historical invoice reconstruction', async () => {
        const response = await request('/sales/products?name=Chai&id=1&page=1');
        assert.equal(response.status, 200);
        const result = await response.json();
        assert.equal(result.items.length, 1);
        assert.ok(result.items[0].unitsInStock > 0);
        const invoice = await (await request(`/sales/invoices/${existingId}`)).json();
        assert.equal(invoice.id, existingId);
        assert.ok(invoice.lines.length > 0);
        assert.equal(invoice.total, invoice.lines.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0));
        assert.equal((await request('/sales/invoices/2147483647')).status, 404);
        const pdf = await request(`/sales/invoices/${existingId}/pdf`);
        assert.equal(pdf.headers.get('content-type'), 'application/pdf');
        assert.ok((await pdf.text()).startsWith('%PDF-1.4'));
    });
    await check('Existing registration and login reject invalid credentials', async () => {
        assert.equal((await request('/user/register', { firstName: 'Test', lastName: 'User', email: 'invalid', password: 'invalid' }, false)).status, 400);
        const invalidNames = await request('/user/register', { firstName: '123', lastName: '456', email: 'validation-only@invalid.example', password: 'Valid123!' }, false);
        assert.equal(invalidNames.status, 400);
        const body = await invalidNames.json();
        assert.ok(body.errors.some(e => e.propertyName === 'FirstName'));
        assert.ok(body.errors.some(e => e.propertyName === 'LastName'));
        assert.equal((await request('/user/Login', { email: 'no-existing-user@invalid.example', password: 'Invalid123!' }, false)).status, 400);
    });
    await check('API rejects duplicated products and preserves sales', async () => {
        const detail = { productId: 1, unitPrice: 35, quantity: 1 };
        const response = await request('/CreateOrder', { customerId: 'ALFKI', shipAddress: 'Calle 123', shipCity: 'Quito', shipCountry: 'Ecuador', shipPostalCode: '', orderDetails: [detail, detail] });
        assert.equal(response.status, 400);
        assert.ok((await response.json()).errors.some(e => e.message.includes('una vez')));
        assert.equal(Number(sql('SELECT COUNT(*) FROM Orders')), before);
    });
    const client = start(['run', '--project', 'NorthWind.Sales.BlazorClient', '--no-build', '--no-launch-profile', '--urls', 'http://localhost:5355'], root, { ASPNETCORE_ENVIRONMENT: 'Development' });
    await ready('http://localhost:5355/', client);
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    const context = await browser.newContext({ acceptDownloads: true });
    await context.addInitScript(token => {
        if (window === window.top && location.origin === 'http://localhost:5355' && !sessionStorage.getItem('testTokenInstalled')) {
            localStorage.setItem('tokens', JSON.stringify({ AccessToken: token }));
            sessionStorage.setItem('testTokenInstalled', 'true');
        }
    }, token);
    await context.route('**/appsettings.json', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ WebApiAddress: 'http://localhost:5255' }) }));
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await check('Unconfigured IVA is clearly indicated and cannot be submitted from Blazor', async () => {
        await page.route('**/sales/tax', async route => {
            const response = await fetch('http://localhost:5256/sales/tax', { headers: { Authorization: 'Bearer ' + token } });
            await route.fulfill({ status: response.status, contentType: 'application/json', body: await response.text() });
        });
        await page.goto('http://localhost:5355/createorder');
        await page.getByText(/Verifique SalesTax:Enabled/).waitFor();
        assert.equal(await page.getByRole('button', { name: 'Enviar', exact: true }).isDisabled(), true);
        assert.ok((await page.locator('[aria-live="polite"]').innerText()).includes('pendiente de configuración'));
        await page.unroute('**/sales/tax');
    });
    await page.goto('http://localhost:5355/createorder');
    await page.getByRole('heading', { level: 1 }).waitFor();
    assert.equal(await page.getByRole('heading', { level: 1 }).innerText(), 'Crear orden de compra');
    assert.equal(await page.locator('#invoiceId').count(), 0);
    assert.equal(await page.getByText('Consultar factura guardada', { exact: true }).count(), 0);
    assert.equal(await page.getByRole('button', { name: 'Consultar factura', exact: true }).count(), 0);
    assert.equal(await page.getByRole('link', { name: 'Consultar facturas', exact: true }).count(), 1);
    assert.ok(await page.getByRole('link', { name: 'Crear orden', exact: true }).evaluate(el => el.classList.contains('active')));
    async function verifyTaxTotals() {
        const tax = await (await request('/sales/tax')).json();
        assert.equal(tax.enabled, true);
        assert.equal(tax.rate, 0.15);
        assert.equal(tax.discountRate, 0.10);
        const rows = await page.locator('table').first().locator('tbody tr').evaluateAll(rows => rows.map(row => ({
            id: Number(row.cells[0].textContent.split('·')[0].trim()),
            quantity: Number(row.querySelector('input').value)
        })));
        let subtotal = 0;
        for (const row of rows) {
            const product = (await (await request('/sales/products?id=' + row.id)).json()).items[0];
            subtotal += Math.round(product.unitPrice * row.quantity * 100) / 100;
        }
        const discount = Math.round(subtotal * .10 * 100) / 100;
        const base = Math.round((subtotal - discount) * 100) / 100;
        const iva = Math.round(Math.round(base * 100) * 15 / 100) / 100;
        const total = Math.round((base + iva) * 100) / 100;
        const format = value => value.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        const text = await page.locator('[aria-live="polite"]').innerText();
        assert.ok(!text.includes('pendiente de configuración'));
        for (const amount of [subtotal, discount, base, iva, total]) assert.ok(text.includes(format(amount)), text);
        assert.ok(text.includes('(15%)'));
        console.log('PASS Browser totals match backend catalog with 10% discount and 15% IVA');
    }
    await page.waitForFunction(() => document.querySelector('[aria-live="polite"]')?.textContent.includes('(15%)'));
    await verifyTaxTotals();
    await check('Customer search matches name and identification and selects current read-only data', async () => {
        await page.getByRole('button', { name: 'Buscar cliente', exact: true }).click();
        const customerDialog = page.getByRole('dialog');
        await customerDialog.getByLabel('Nombre o identificación', { exact: true }).fill('Alfa');
        await customerDialog.getByText('Comercial Alfa (ficticio)', { exact: true }).waitFor();
        await customerDialog.getByLabel('Nombre o identificación', { exact: true }).fill('DEMO-ALFKI');
        await customerDialog.getByText('DEMO-ALFKI', { exact: true }).waitFor();
        await page.getByRole('dialog').getByLabel('Código', { exact: true }).fill('ALFKI');
        await page.getByRole('button', { name: 'Seleccionar ALFKI', exact: true }).click();
        await customerDialog.waitFor({ state: 'detached' });
        const fields = { customerId: 'id', customerName: 'name', customerIdentification: 'identification', customerEmail: 'email',
            customerPhone: 'phone', shipAddress: 'address', shipCity: 'city', shipCountry: 'country', shipPostalCode: 'postalCode' };
        const alfki = (await (await request('/sales/customers?id=ALFKI')).json()).items[0];
        for (const [field, property] of Object.entries(fields)) {
            assert.equal(await page.locator('#' + field).inputValue(), alfki[property] ?? '');
            assert.equal(await page.locator('#' + field).evaluate(input => input.readOnly), field.startsWith('customer'));
        }
        assert.equal(await page.locator('.validation-message').count(), 0);
        await page.locator('#customerName').focus();
        await page.keyboard.type('No debe cambiar');
        assert.equal(await page.locator('#customerName').inputValue(), alfki.name);
        await page.locator('#shipAddress').fill('Entrega editada');
        await page.locator('#shipCity').fill('Quito');
        await page.locator('#shipCountry').fill('Ecuador');
        await page.locator('#shipPostalCode').fill('170101');
        assert.deepEqual((await (await request('/sales/customers?id=ALFKI')).json()).items[0], alfki);
        await page.getByRole('button', { name: 'Buscar cliente', exact: true }).click();
        await customerDialog.getByLabel('Código', { exact: true }).fill('ANATR');
        await page.getByRole('button', { name: 'Seleccionar ANATR', exact: true }).click();
        await customerDialog.waitFor({ state: 'detached' });
        const anatr = (await (await request('/sales/customers?id=ANATR')).json()).items[0];
        for (const [field, property] of Object.entries(fields))
            assert.equal(await page.locator('#' + field).inputValue(), anatr[property] ?? '');
        assert.notEqual(await page.locator('#customerName').inputValue(), alfki.name);
        // Return to the same customer used by the existing order regression checks.
        await page.getByRole('button', { name: 'Buscar cliente', exact: true }).click();
        await customerDialog.getByLabel('Código', { exact: true }).fill('ALFKI');
        await page.getByRole('button', { name: 'Seleccionar ALFKI', exact: true }).click();
        await customerDialog.waitFor({ state: 'detached' });
        await page.locator('#shipAddress').fill('Calle 123');
        await page.locator('#shipCity').fill('Quito');
        await page.locator('#shipCountry').fill('Ecuador');
    });
    async function choose(id) {
        await page.getByRole('dialog').getByLabel('Código', { exact: true }).fill(String(id));
        await page.getByRole('button', { name: `Seleccionar ${id}`, exact: true }).click();
    }
    await check('Product search excludes empty stock and prevents duplicates', async () => {
        await page.getByRole('button', { name: 'Agregar producto', exact: true }).click();
        await page.getByRole('dialog').getByLabel('Código', { exact: true }).fill('2');
        await page.getByRole('dialog').getByText('No hay resultados disponibles.').waitFor();
        assert.equal(await page.getByRole('dialog').getByRole('button', { name: 'Seleccionar 2', exact: true }).count(), 0);
        await choose(1);
        await page.getByRole('button', { name: 'Agregar producto', exact: true }).click();
        await choose(1);
        await page.getByRole('dialog').getByText('El producto ya está en la orden.').waitFor();
        await page.getByRole('button', { name: 'Cerrar', exact: true }).click();
        await verifyTaxTotals();
    });
    await check('Changing and removing products preserve shipping fields', async () => {
        await page.getByLabel('Cantidad', { exact: true }).fill('2');
        await page.getByRole('button', { name: 'Cambiar', exact: true }).click();
        await choose(3);
        await verifyTaxTotals();
        assert.equal(await page.getByLabel('Cantidad', { exact: true }).inputValue(), '2');
        assert.equal(await page.locator('#shipAddress').inputValue(), 'Calle 123');
        assert.equal(await page.locator('#shipCity').inputValue(), 'Quito');
        await page.getByRole('button', { name: 'Agregar producto', exact: true }).click();
        await choose(1);
        await verifyTaxTotals();
        const snapshot = () => sql('SELECT * FROM Products ORDER BY Id; SELECT * FROM Orders ORDER BY Id; SELECT * FROM OrderDetails ORDER BY OrderId, ProductId');
        const dataBefore = snapshot();
        const rowsBefore = await page.locator('table').first().locator('tbody').innerText();
        const totalsBefore = await page.locator('[aria-live="polite"]').innerText();
        await page.getByRole('button', { name: 'Quitar', exact: true }).last().click();
        const dialog = page.getByRole('dialog', { name: 'Confirmar eliminación' });
        await dialog.waitFor();
        assert.ok((await dialog.innerText()).includes('Chai'));
        assert.equal(await dialog.getByRole('button', { name: 'Cancelar', exact: true }).evaluate(el => el === document.activeElement), true);
        for (let i = 0; i < 4; i++) {
            await page.keyboard.press('Tab');
            assert.equal(await dialog.evaluate(el => el.contains(document.activeElement)), true);
        }
        await dialog.getByRole('button', { name: 'Cancelar', exact: true }).click();
        assert.equal(await page.locator('table').first().locator('tbody').innerText(), rowsBefore);
        assert.equal(await page.locator('[aria-live="polite"]').innerText(), totalsBefore);
        assert.equal(await page.getByRole('button', { name: 'Quitar', exact: true }).last().evaluate(el => el === document.activeElement), true);
        await page.getByRole('button', { name: 'Quitar', exact: true }).last().click();
        await dialog.waitFor();
        await page.keyboard.press('Escape');
        await dialog.waitFor({ state: 'detached' });
        assert.equal(await page.locator('[aria-live="polite"]').innerText(), totalsBefore);
        await page.getByRole('button', { name: 'Quitar', exact: true }).last().click();
        await dialog.getByRole('button', { name: 'Sí, quitar', exact: true }).click();
        await dialog.waitFor({ state: 'detached' });
        assert.equal(await page.getByRole('button', { name: 'Quitar', exact: true }).count(), 1);
        assert.equal(await page.getByLabel('Cantidad', { exact: true }).inputValue(), '2');
        assert.notEqual(await page.locator('[aria-live="polite"]').innerText(), totalsBefore);
        assert.equal(snapshot(), dataBefore);
        await verifyTaxTotals();
        assert.equal(await page.locator('#addOrderProduct').evaluate(el => el === document.activeElement), true);
        assert.equal(await page.locator('#shipAddress').inputValue(), 'Calle 123');
    });
    await check('Excess quantity is blocked before sending a sale', async () => {
        await page.getByLabel('Cantidad', { exact: true }).fill('101');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        await page.getByText('La cantidad debe ser mayor que cero y no superar el stock disponible.').waitFor();
        assert.equal(Number(sql('SELECT COUNT(*) FROM Orders')), before);
        await page.getByLabel('Cantidad', { exact: true }).fill('2');
        await page.getByRole('heading', { level: 1 }).click();
        await verifyTaxTotals();
    });
    await check('Mobile layout remains usable', async () => {
        await page.setViewportSize({ width: 390, height: 844 });
        assert.ok(await page.getByRole('button', { name: 'Agregar producto', exact: true }).isVisible());
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 2));
        assert.deepEqual(errors, []);
    });
    await check('Logout confirmation preserves the page, draft and JWT until confirmed', async () => {
        const url = page.url();
        const fields = await page.locator('form input').evaluateAll(inputs => inputs.map(input => input.value));
        const storedTokens = await page.evaluate(() => localStorage.getItem('tokens'));
        const totals = await page.locator('[aria-live="polite"]').innerText();
        const trigger = page.getByRole('button', { name: 'Cerrar sesión', exact: true });
        const dialog = page.getByRole('dialog', { name: '¿Cerrar sesión?', exact: true });
        await trigger.click();
        await dialog.waitFor();
        assert.equal(page.url(), url);
        assert.equal(await page.evaluate(() => localStorage.getItem('tokens')), storedTokens);
        assert.ok((await dialog.innerText()).includes('¿Estás seguro de que deseas cerrar tu sesión?'));
        assert.equal(await dialog.getByRole('button', { name: 'Cancelar', exact: true }).evaluate(el => el === document.activeElement), true);
        const bounds = await dialog.boundingBox();
        const viewport = page.viewportSize();
        assert.ok(Math.abs(bounds.x + bounds.width / 2 - viewport.width / 2) < 2);
        assert.ok(Math.abs(bounds.y + bounds.height / 2 - viewport.height / 2) < 2);
        assert.equal(await dialog.evaluate(el => getComputedStyle(el, '::backdrop').backgroundColor), 'rgba(15, 23, 42, 0.45)');
        for (let i = 0; i < 4; i++) {
            await page.keyboard.press('Tab');
            assert.equal(await dialog.evaluate(el => el.contains(document.activeElement)), true);
        }
        await dialog.getByRole('button', { name: 'Cancelar', exact: true }).click();
        await dialog.waitFor({ state: 'hidden' });
        assert.equal(await trigger.evaluate(el => el === document.activeElement), true);
        await trigger.click();
        await dialog.waitFor();
        await page.keyboard.press('Escape');
        await dialog.waitFor({ state: 'hidden' });
        assert.equal(page.url(), url);
        assert.equal(await page.evaluate(() => localStorage.getItem('tokens')), storedTokens);
        assert.deepEqual(await page.locator('form input').evaluateAll(inputs => inputs.map(input => input.value)), fields);
        assert.equal(await page.locator('[aria-live="polite"]').innerText(), totals);
        assert.equal(Number(sql('SELECT COUNT(*) FROM Orders')), before);
    });
    await check('UI automatically clears a saved order and enables a new draft (simulated save response)', async () => {
        await page.route('**/CreateOrder', route => route.fulfill({ contentType: 'application/json', body: String(existingId) }));
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        await page.getByText(`Orden ${existingId} creada.`, { exact: true }).waitFor();
        assert.ok(await page.locator('#shipAddress').isEnabled());
        assert.ok(await page.getByRole('button', { name: 'Enviar', exact: true }).isEnabled());
        assert.equal(await page.locator('#shipAddress').inputValue(), '');
        assert.equal(await page.locator('#customerId').inputValue(), '');
        assert.equal(await page.getByRole('button', { name: 'Quitar', exact: true }).count(), 0);
        assert.equal(await page.getByRole('button', { name: 'Nueva orden', exact: true }).count(), 0);
        assert.equal(Number(sql('SELECT COUNT(*) FROM Orders')), before);
        await page.unroute('**/CreateOrder');
    });
    await check('PDF viewer reconstructs and displays an existing invoice', async () => {
        await page.setViewportSize({ width: 1280, height: 800 });
        await page.getByRole('link', { name: 'Consultar facturas', exact: true }).click();
        await page.waitForURL('http://localhost:5355/invoices');
        await page.getByRole('heading', { name: 'Consultar facturas', exact: true, level: 1 }).waitFor();
        assert.ok(await page.getByRole('link', { name: 'Consultar facturas', exact: true }).evaluate(el => el.classList.contains('active')));
        assert.ok(await page.getByRole('link', { name: 'Consultar facturas', exact: true }).locator('.bi-file-earmark-text').isVisible());
        const menuIcon = page.getByRole('link', { name: 'Consultar facturas', exact: true }).locator('svg');
        assert.equal(await menuIcon.locator('path').count(), 1);
        assert.deepEqual(await menuIcon.evaluate(el => [getComputedStyle(el).width, getComputedStyle(el).height]), ['20px', '20px']);
        assert.equal(await page.locator('#customerId').count(), 0);
        assert.equal(await page.locator('#invoiceId').count(), 0);
        assert.equal(await page.getByRole('button', { name: 'Consultar factura', exact: true }).count(), 0);
        assert.equal(await page.getByText('Ingrese un número de factura válido, mayor que cero.', { exact: true }).count(), 0);
        assert.equal(await page.getByRole('region', { name: 'Consulta de facturas', exact: true }).count(), 0);
        await page.locator('#invoiceTable').waitFor();
        const listed = await (await request('/sales/invoices')).json();
        assert.equal(listed.totalCount, before);
        const rowIds = () => page.locator('#invoiceTable tbody tr').evaluateAll(rows => rows.map(row => Number(row.cells[0].textContent)));
        assert.deepEqual(await rowIds(), listed.items.map(item => item.id));
        await page.getByText('Facturas encontradas: ' + listed.totalCount, { exact: true }).waitFor();
        if (listed.totalPages > 1) {
            await page.getByRole('button', { name: 'Siguiente', exact: true }).click();
            await page.getByText('Página 2 de ' + listed.totalPages, { exact: true }).waitFor();
            const second = await (await request('/sales/invoices?page=2')).json();
            assert.deepEqual(await rowIds(), second.items.map(item => item.id));
            await page.getByRole('button', { name: 'Anterior', exact: true }).click();
            await page.getByText('Página 1 de ' + listed.totalPages, { exact: true }).waitFor();
            await page.getByRole('button', { name: 'Siguiente', exact: true }).click();
            await page.getByText('Página 2 de ' + listed.totalPages, { exact: true }).waitFor();
        }
        const partial = listed.items[0].customerName ? listed.items[0].customerName.slice(0, 4) : listed.items[0].customerId;
        await page.locator('#invoiceSearch').fill(partial);
        await page.getByRole('button', { name: 'Buscar', exact: true }).click();
        const matches = await (await request('/sales/invoices?search=' + encodeURIComponent(partial))).json();
        await page.getByText('Página 1 de ' + matches.totalPages, { exact: true }).waitFor();
        await page.getByText('Facturas encontradas: ' + matches.totalCount, { exact: true }).waitFor();
        assert.deepEqual(await rowIds(), matches.items.map(item => item.id));
        const selectedId = matches.items[0].id;
        const selectedInvoice = await (await request('/sales/invoices/' + selectedId)).json();
        await page.getByRole('button', { name: 'Ver PDF de factura ' + selectedId, exact: true }).click();
        await page.getByRole('link', { name: 'Descargar PDF', exact: true }).waitFor();
        assert.equal(await page.getByRole('link', { name: 'Descargar PDF', exact: true }).getAttribute('download'), `factura-${selectedId}.pdf`);
        const selectedBytes = await page.evaluate(async () => [...new Uint8Array(await (await fetch(document.querySelector('#invoicePdf').src)).arrayBuffer())]);
        assert.ok(Buffer.from(selectedBytes).toString('latin1').includes(selectedInvoice.total.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 })));
        await page.locator('#invoiceSearch').fill(String(listed.items[0].id));
        await page.getByRole('button', { name: 'Buscar', exact: true }).click();
        await page.getByRole('button', { name: 'Ver PDF de factura ' + listed.items[0].id, exact: true }).waitFor();
        await page.locator('#invoiceSearch').fill('no-matching-invoice-' + crypto.randomUUID());
        await page.getByRole('button', { name: 'Buscar', exact: true }).click();
        await page.getByText('No se encontraron facturas', { exact: true }).waitFor();
        assert.equal(await page.locator('#invoiceTable tbody tr').count(), 0);
        await page.route('**/sales/invoices?*', route => route.fulfill({ status: 500, body: '' }));
        await page.getByRole('button', { name: 'Limpiar', exact: true }).click();
        await page.getByText('No se pudieron cargar las facturas. Verifique la conexión e intente nuevamente.', { exact: true }).waitFor();
        await page.unroute('**/sales/invoices?*');
        await page.getByRole('button', { name: 'Buscar', exact: true }).click();
        await page.getByText('Facturas encontradas: ' + listed.totalCount, { exact: true }).waitFor();
        console.log('PASS Stored invoice table, partial search, pagination, row PDF and list failure recovery');
        const pdfId = selectedId;
        const viewButton = page.getByRole('button', { name: 'Ver PDF de factura ' + pdfId, exact: true });
        await page.getByRole('link', { name: 'Descargar PDF', exact: true }).waitFor();
        const src = await page.locator('#invoicePdf').getAttribute('src');
        assert.ok(src.startsWith('blob:'));
        const bytes = await page.evaluate(async url => [...new Uint8Array(await (await fetch(url)).arrayBuffer())], src);
        assert.ok(Buffer.from(bytes).toString('latin1').startsWith('%PDF-1.4'));
        if (!skipDownloads) {
            const download = page.waitForEvent('download');
            await page.getByRole('link', { name: 'Descargar PDF', exact: true }).click();
            const downloaded = await download;
            assert.equal(downloaded.suggestedFilename(), `factura-${pdfId}.pdf`);
        } else {
            console.log('SKIP Disk download; PDF bytes and download link were verified in memory');
        }
        await page.evaluate(() => {
            window.invoicePrintCalls = 0;
            document.getElementById('invoicePdf').contentWindow.print = () => window.invoicePrintCalls++;
        });
        await page.getByRole('button', { name: 'Imprimir', exact: true }).click();
        await page.waitForFunction(() => window.invoicePrintCalls === 1);
        await page.route('**/sales/invoices/' + pdfId, route => route.fulfill({ status: 404, body: '' }));
        await viewButton.click();
        await page.getByText('No se encontró la factura.', { exact: true }).waitFor();
        assert.equal(await page.locator('#invoicePdf').count(), 0);
        await page.unroute('**/sales/invoices/' + pdfId);
        await page.route('**/sales/invoices/' + pdfId, route => route.fulfill({ status: 500, body: '' }));
        await viewButton.click();
        await page.getByText('No se pudo consultar la factura. Intente nuevamente.', { exact: true }).waitFor();
        await page.unroute('**/sales/invoices/' + pdfId);
        await viewButton.click();
        await page.getByRole('link', { name: 'Descargar PDF', exact: true }).waitFor();
        await page.setViewportSize({ width: 390, height: 844 });
        assert.ok(await viewButton.isVisible());
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 2));
        if (!await page.getByRole('link', { name: 'Crear orden', exact: true }).isVisible())
            await page.getByRole('button', { name: 'Alternar navegación', exact: true }).click();
        await page.getByRole('link', { name: 'Crear orden', exact: true }).click();
        await page.waitForURL('http://localhost:5355/createorder');
        await page.locator('#customerId').waitFor();
        assert.ok(await page.locator('nav a[href="createorder"]').evaluate(el => el.classList.contains('active')));
        assert.equal(await page.locator('#invoiceId').count(), 0);
    });
    await check('Failed token removal preserves authentication and permits retry', async () => {
        await page.setViewportSize({ width: 1280, height: 800 });
        await page.getByRole('button', { name: 'Cerrar sesión', exact: true }).click();
        const dialog = page.getByRole('dialog', { name: '¿Cerrar sesión?', exact: true });
        await dialog.waitFor();
        const storedTokens = await page.evaluate(() => localStorage.getItem('tokens'));
        await page.evaluate(() => {
            window.originalRemoveItem = Storage.prototype.removeItem;
            window.failTokenRemoval = true;
            Storage.prototype.removeItem = function(key) {
                if (key === 'tokens' && window.failTokenRemoval) throw new Error('Simulated storage failure');
                return window.originalRemoveItem.call(this, key);
            };
        });
        try {
            await dialog.getByRole('button', { name: 'Sí, cerrar sesión', exact: true }).click();
            await dialog.getByRole('alert').waitFor();
            assert.equal(await page.evaluate(() => localStorage.getItem('tokens')), storedTokens);
            assert.equal(page.url(), 'http://localhost:5355/createorder');
            assert.ok(await dialog.getByRole('button', { name: 'Sí, cerrar sesión', exact: true }).isEnabled());
        } finally {
            await page.evaluate(() => {
                window.failTokenRemoval = false;
                Storage.prototype.removeItem = window.originalRemoveItem;
            });
        }
    });
    await check('Confirmed logout removes JWT, redirects to login and blocks protected pages', async () => {
        const visited = [];
        page.on('framenavigated', frame => { if (frame === page.mainFrame()) visited.push(frame.url()); });
        await page.getByRole('dialog', { name: '¿Cerrar sesión?', exact: true })
            .getByRole('button', { name: 'Sí, cerrar sesión', exact: true }).evaluate(button => { button.click(); button.click(); });
        await page.waitForURL('http://localhost:5355/user/login');
        assert.equal(await page.evaluate(() => localStorage.getItem('tokens')), null);
        await page.getByRole('button', { name: 'Iniciar sesión', exact: true }).waitFor();
        assert.equal(await page.getByRole('button', { name: 'Cerrar sesión', exact: true }).count(), 0);
        await page.goBack();
        assert.equal(await page.locator('#customerId').count(), 0);
        await page.goto('http://localhost:5355/createorder');
        await page.getByRole('link', { name: 'Iniciar sesión', exact: true }).waitFor();
        assert.equal(await page.locator('#customerId').count(), 0);
        await page.reload();
        await page.getByRole('link', { name: 'Iniciar sesión', exact: true }).waitFor();
        assert.equal(await page.locator('#customerId').count(), 0);
        assert.equal(await page.evaluate(() => localStorage.getItem('tokens')), null);
        await page.goto('http://localhost:5355/invoices');
        await page.getByRole('link', { name: 'Iniciar sesión', exact: true }).waitFor();
        assert.equal(await page.locator('#invoiceId').count(), 0);
        assert.equal(await page.getByRole('link', { name: 'Consultar facturas', exact: true }).count(), 0);
        assert.ok(!visited.some(url => url.includes('/user/logout')));
        for (const route of ['/sales/products', '/sales/tax', '/sales/invoices', `/sales/invoices/${existingId}/pdf`])
            assert.equal((await request(route, undefined, false)).status, 401);
        assert.deepEqual(errors, []);
    });
    assert.equal(Number(sql('SELECT COUNT(*) FROM Orders')), before);
    console.log('All API and Blazor checks passed; no test orders were saved.');
})().catch(error => { console.error(error); process.exitCode = 1; }).finally(async () => {
    if (browser) await browser.close();
    for (const child of children.reverse()) {
        if (child.exitCode === null) await new Promise(resolve => { child.once('exit', resolve); child.kill(); });
    }
});
