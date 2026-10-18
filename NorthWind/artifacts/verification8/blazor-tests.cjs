const { chromium } = require('../verification/node_modules/playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const count = () => Number(execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', 'NorthWindDB', '-E', '-h', '-1', '-W', '-b', '-Q', 'SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Orders;'], { encoding: 'utf8' }).trim());
const valid = () => ({ customerId: 'ALFKI', shipAddress: '31 Oriente 810', shipCity: 'Puebla', shipCountry: 'México', shipPostalCode: '72100', orderDetails: [{ productId: 1, unitPrice: 35, quantity: 10 }] });
const change = updates => Object.assign(valid(), updates);
const cases = [
    { name: 'Blazor-1', payload: change({ customerId: 'ALFKQ' }), fields: [{ label: 'Id del cliente', index: 0, message: 'cliente proporcionado no existe' }] },
    { name: 'Blazor-2-ANTON', payload: change({ customerId: 'ANTON' }), fields: [{ label: 'Id del cliente', index: 0, message: 'cliente proporcionado no existe' }], fixtureDifference: true },
    { name: 'Blazor-2-BONAP', payload: change({ customerId: 'BONAP' }), fields: [{ label: 'Id del cliente', index: 0, message: 'adeudo pendiente' }] },
    { name: 'Blazor-3', payload: change({ orderDetails: [{ productId: 10, unitPrice: 35, quantity: 1 }, { productId: 40, unitPrice: 35, quantity: 1 }] }), fields: [{ label: 'Id de Producto', index: 1, message: 'El producto 40 no existe.' }], fixtureDifference: true },
    { name: 'Blazor-4', payload: change({ orderDetails: [{ productId: 1, unitPrice: 35, quantity: 10 }, { productId: 2, unitPrice: 35, quantity: 1 }, { productId: 3, unitPrice: 65, quantity: 20 }, { productId: 4, unitPrice: 75, quantity: 40 }, { productId: 1, unitPrice: 35, quantity: 11 }] }), fields: [{ label: 'Cantidad', index: 1, message: 'no suficiente para producto 2.' }], fixtureDifference: true },
    { name: 'Blazor-4-Acumulacion', payload: change({ orderDetails: [{ productId: 1, unitPrice: 35, quantity: 60 }, { productId: 1, unitPrice: 35, quantity: 60 }] }), fields: [{ label: 'Cantidad', index: 0, message: 'Cantidad 100 de 120 no suficiente para producto 1.' }] },
    { name: 'P7-Local', payload: change({ customerId: '', shipCity: 'AB', orderDetails: [{ productId: 0, unitPrice: 0, quantity: 0 }] }), local: true, fields: [{ label: 'Id del cliente', index: 0, message: 'Debe especificar el identificador' }, { label: 'Ciudad', index: 0, message: 'al menos 3 caracteres' }, { label: 'Id de Producto', index: 0, message: 'identificador del producto' }, { label: 'Precio', index: 0, message: 'precio del producto' }, { label: 'Cantidad', index: 0, message: 'cantidad ordenada' }] }
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
        const swagger = await browser.newPage();
        const swaggerBefore = count();
        await swagger.goto('https://localhost:7154/swagger/index.html');
        const operation = swagger.locator('.opblock-post').filter({ hasText: '/CreateOrder' });
        await operation.locator('.opblock-summary').click();
        await operation.getByRole('button', { name: 'Try it out' }).click();
        const swaggerPayload = valid();
        swaggerPayload.orderDetails = [{ productId: 1, unitPrice: 35, quantity: 10 }, { productId: 3, unitPrice: 65, quantity: 20 }, { productId: 4, unitPrice: 75, quantity: 40 }];
        await operation.locator('textarea').fill(JSON.stringify(swaggerPayload));
        const swaggerResponse = swagger.waitForResponse(response => response.url().endsWith('/CreateOrder') && response.request().method() === 'POST');
        await operation.getByRole('button', { name: 'Execute', exact: true }).click();
        const response = await swaggerResponse;
        assert.equal(response.status(), 200);
        const swaggerOrderId = await response.json();
        assert.equal(count(), swaggerBefore + 1);
        await swagger.screenshot({ path: 'artifacts/verification8/swagger-valid.png', fullPage: true });
        results.push({ name: 'Swagger-POST', passed: true, status: 200, orderId: swaggerOrderId });
        await swagger.close();

        for (const test of cases) {
            const page = await browser.newPage();
            let posts = 0;
            page.on('request', request => { if (request.url().endsWith('/CreateOrder') && request.method() === 'POST') posts++; });
            try {
                const before = count();
                await page.goto('https://localhost:7254/createorder');
                await page.getByRole('heading', { name: 'Crear orden de compra' }).waitFor();
                await fill(page, test.payload);
                for (let attempt = 0; attempt < (test.local ? 1 : 2); attempt++) {
                    const responsePromise = test.local ? null : page.waitForResponse(response => response.url().endsWith('/CreateOrder') && response.request().method() === 'POST');
                    await page.getByRole('button', { name: 'Enviar', exact: true }).click();
                    if (responsePromise) assert.equal((await responsePromise).status(), 400);
                    for (const field of test.fields) {
                        const parent = page.getByLabel(field.label, { exact: true }).nth(field.index).locator('..');
                        await parent.locator('.validation-message').filter({ hasText: field.message }).waitFor();
                    }
                    assert.equal(await page.locator('.validation-errors li').count(), test.fields.length, 'Mensajes duplicados o perdidos');
                    assert.equal(await page.locator('.alert-danger').count(), 0, 'Una validación no debe reemplazar el formulario');
                    assert.equal(count(), before);
                }
                assert.equal(posts, test.local ? 0 : 2);
                if (test.name === 'Blazor-4-Acumulacion') await page.screenshot({ path: 'artifacts/verification8/blazor-server-validation.png', fullPage: true });
                const result = { name: test.name, passed: true, posts, status: test.local ? null : 400, before, after: count(), fixtureDifference: !!test.fixtureDifference, duplicatedErrors: false };
                results.push(result);
                fs.writeFileSync('artifacts/verification8/blazor-results.json', JSON.stringify(results, null, 2));
                console.log(JSON.stringify(result));
            } catch (error) {
                await page.screenshot({ path: `artifacts/verification8/failure-${test.name}.png`, fullPage: true });
                throw error;
            } finally {
                await page.close();
            }
        }
        // Corregir un rechazo del servidor y enviar una orden válida conserva el formulario.
        const page = await browser.newPage();
        await page.goto('https://localhost:7254/createorder');
        await page.getByRole('heading', { name: 'Crear orden de compra' }).waitFor();
        await fill(page, change({ customerId: 'ALFKQ' }));
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        await page.locator('.validation-message').filter({ hasText: 'cliente proporcionado no existe' }).first().waitFor();
        await page.getByLabel('Id del cliente', { exact: true }).fill('ALFKI');
        const validResponse = page.waitForResponse(response => response.url().endsWith('/CreateOrder') && response.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        const accepted = await validResponse;
        assert.equal(accepted.status(), 200);
        const orderId = await accepted.json();
        await page.getByRole('status').filter({ hasText: `Orden ${orderId} creada.` }).waitFor();
        assert.equal(await page.locator('.validation-message').count(), 0);
        assert.equal(await page.locator('.validation-errors').count(), 0);
        results.push({ name: 'Blazor-ValidacionCorregida', passed: true, status: 200, orderId });
        fs.writeFileSync('artifacts/verification8/blazor-results.json', JSON.stringify(results, null, 2));
        await page.screenshot({ path: 'artifacts/verification8/blazor-valid.png', fullPage: true });
        console.log(JSON.stringify(results.at(-1)));
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
