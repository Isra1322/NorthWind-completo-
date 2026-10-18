const { chromium } = require('../verification/node_modules/playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const sql = query => execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', 'NorthWindDB', '-E', '-h', '-1', '-W', '-b', '-Q', `SET NOCOUNT ON; ${query}`], { encoding: 'utf8' }).trim();
const count = () => Number(sql('SELECT COUNT(*) FROM dbo.Orders;'));
const valid = () => ({ customerId: 'ALFKI', shipAddress: '31 Oriente 810', shipCity: 'Puebla', shipCountry: 'México', shipPostalCode: '72100', orderDetails: [{ productId: 1, unitPrice: 35, quantity: 10 }] });
const change = updates => Object.assign(valid(), updates);
const cases = [
    { name: '1', description: 'Orden válida', payload: valid(), valid: true },
    { name: '2', description: 'Cliente inexistente', payload: change({ customerId: 'ZZZZZ' }), server: true, errors: ['El identificador de cliente proporcionado no existe.'] },
    { name: '3', description: 'Cliente con adeudo', payload: change({ customerId: 'BONAP' }), server: true, errors: ['adeudo pendiente'] },
    { name: '4', description: 'Producto inexistente', payload: change({ orderDetails: [{ productId: 999, unitPrice: 35, quantity: 1 }] }), server: true, errors: ['El producto 999 no existe.'] },
    { name: '5', description: 'Producto sin existencias', payload: change({ orderDetails: [{ productId: 2, unitPrice: 35, quantity: 1 }] }), server: true, errors: ['no suficiente para producto 2.'] },
    { name: '6', description: 'Campos obligatorios vacíos', payload: { customerId: '', shipAddress: '', shipCity: '', shipCountry: '', shipPostalCode: '', orderDetails: [] }, errors: ['Debe especificar el identificador del cliente.', 'Debe especificar la dirección de envío.', 'Debe especificar la ciudad de envío.', 'Debe especificar el país de envío.', 'Debe especificar al menos un producto de la orden.'] },
    { name: '7', description: 'Datos de envío cortos y largos', payload: change({ shipAddress: 'A'.repeat(61), shipCity: 'AB', shipCountry: 'A'.repeat(16), shipPostalCode: 'A'.repeat(11) }), errors: ['La longitud máxima de la dirección es 60.', 'Debe especificar al menos 3 caracteres', 'La longitud máxima del país es 15.', 'La longitud máxima del código postal es 10.'] },
    { name: '8', description: 'Cantidad cero', payload: change({ orderDetails: [{ productId: 1, unitPrice: 35, quantity: 0 }] }), errors: ['Debe especificar la cantidad ordenada del producto.'], field: 'Quantity' },
    { name: '9', description: 'Precio cero', payload: change({ orderDetails: [{ productId: 1, unitPrice: 0, quantity: 1 }] }), errors: ['Debe especificarse el precio del producto.'], field: 'UnitPrice' },
    { name: '10', description: 'Productos vacíos', payload: change({ orderDetails: [] }), errors: ['Debe especificar al menos un producto de la orden.'] },
    { name: '11', description: 'Múltiples errores simultáneos y segunda fila', payload: change({ customerId: '', shipAddress: '', shipCity: 'AB', orderDetails: [{ productId: 1, unitPrice: 35, quantity: 1 }, { productId: 0, unitPrice: 0, quantity: 0 }] }), errors: ['Debe especificar el identificador del cliente.', 'Debe especificar la dirección de envío.', 'Debe especificar al menos 3 caracteres', 'Debe especificar el identificador del producto.', 'Debe especificarse el precio del producto.', 'Debe especificar la cantidad ordenada del producto.'], secondRow: true }
];

async function fill(page, payload) {
    for (const [label, property] of [['Id del cliente', 'customerId'], ['Dirección', 'shipAddress'], ['Ciudad', 'shipCity'], ['País', 'shipCountry'], ['Código Postal', 'shipPostalCode']])
        await page.getByLabel(label, { exact: true }).fill(payload[property]);
    for (let index = 0; index < payload.orderDetails.length; index++) {
        await page.getByRole('button', { name: 'Agregar producto' }).click();
        const detail = payload.orderDetails[index];
        await page.getByLabel('Id de Producto', { exact: true }).nth(index).fill(String(detail.productId));
        await page.getByLabel('Precio', { exact: true }).nth(index).fill(String(detail.unitPrice));
        await page.getByLabel('Cantidad', { exact: true }).nth(index).fill(String(detail.quantity));
    }
}

(async () => {
    const browser = await chromium.launch({ channel: 'chrome', headless: true });
    const results = [];
    try {
        for (const test of cases) {
            const page = await browser.newPage();
            const before = count();
            let posts = 0;
            const errors = [];
            page.on('pageerror', error => errors.push(error.message));
            page.on('request', request => {
                if (request.url().endsWith('/CreateOrder') && request.method() === 'POST') posts++;
            });
            try {
                await page.goto('https://localhost:7254/createorder');
                await page.getByRole('heading', { name: 'Crear orden de compra' }).waitFor();
                await fill(page, test.payload);
                const responsePromise = (test.valid || test.server) ? page.waitForResponse(response =>
                    response.url().endsWith('/CreateOrder') && response.request().method() === 'POST') : null;
                await page.getByRole('button', { name: 'Enviar', exact: true }).click();
                let status = null;
                let orderId = null;
                if (test.valid) {
                    const response = await responsePromise;
                    status = response.status();
                    orderId = await response.json();
                    assert.equal(status, 200);
                    await page.getByRole('status').filter({ hasText: `Orden ${orderId} creada.` }).waitFor();
                    assert.equal(posts, 1, 'Debe enviarse exactamente una petición');
                    assert.equal(count(), before + 1);
                } else if (test.server) {
                    const response = await responsePromise;
                    status = response.status();
                    assert.equal(status, 500);
                    const alert = page.locator('.alert-danger');
                    await alert.waitFor();
                    for (const error of test.errors) assert.ok((await alert.textContent()).includes(error));
                    assert.equal(posts, 1);
                    assert.equal(count(), before);
                    await page.getByRole('button', { name: 'Aceptar', exact: true }).click();
                    await page.getByRole('button', { name: 'Enviar', exact: true }).waitFor();
                } else {
                    const summary = page.locator('.validation-errors');
                    await summary.waitFor();
                    for (const error of test.errors)
                        await page.waitForFunction(message => document.querySelector('.validation-errors')?.textContent.includes(message), error);
                    await page.waitForTimeout(150);
                    assert.equal(posts, 0, 'La validación básica debe impedir la petición HTTP');
                    assert.equal(count(), before);
                    if (test.field) {
                        const label = test.field === 'Quantity' ? 'Cantidad' : 'Precio';
                        const cell = page.getByLabel(label, { exact: true }).locator('..');
                        assert.ok((await cell.locator('.validation-message').textContent()).includes(test.errors[0]));
                    }
                    if (test.secondRow) {
                        const row = page.locator('tbody tr').nth(1);
                        assert.equal(await row.locator('.validation-message').count(), 3);
                    }
                }
                assert.deepEqual(errors, []);
                const result = { name: test.name, description: test.description, passed: true, posts, status, orderId, before, after: count(), certificateErrorsIgnored: false };
                if (test.name === '1') await page.screenshot({ path: 'artifacts/verification7/blazor-valid.png', fullPage: true });
                if (test.name === '11') await page.screenshot({ path: 'artifacts/verification7/blazor-validation.png', fullPage: true });
                results.push(result);
                fs.writeFileSync('artifacts/verification7/blazor-results.json', JSON.stringify(results, null, 2));
                console.log(JSON.stringify(result));
            } catch (error) {
                await page.screenshot({ path: `artifacts/verification7/blazor-failure-${test.name}.png`, fullPage: true });
                throw error;
            } finally {
                await page.close();
            }
        }
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
