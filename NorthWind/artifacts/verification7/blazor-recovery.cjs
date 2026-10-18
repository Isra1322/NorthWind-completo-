const { chromium } = require('../verification/node_modules/playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const count = () => Number(execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', 'NorthWindDB', '-E', '-h', '-1', '-W', '-b', '-Q', 'SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Orders;'], { encoding: 'utf8' }).trim());

(async () => {
    const browser = await chromium.launch({ channel: 'chrome', headless: true });
    try {
        const page = await browser.newPage();
        let posts = 0;
        page.on('request', request => { if (request.url().endsWith('/CreateOrder') && request.method() === 'POST') posts++; });
        const initialCount = count();
        await page.goto('https://localhost:7254/createorder');
        await page.getByRole('heading', { name: 'Crear orden de compra' }).waitFor();
        await page.getByRole('button', { name: 'Agregar producto' }).click();
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        await page.waitForFunction(() => document.querySelectorAll('.validation-errors li').length === 7);
        assert.equal(posts, 0);
        for (const [label, value] of [['Id del cliente', 'ALFKI'], ['Dirección', '31 Oriente 810'], ['Ciudad', 'Puebla'], ['País', 'México'], ['Código Postal', '72100'], ['Id de Producto', '1'], ['Precio', '35'], ['Cantidad', '1']])
            await page.getByLabel(label, { exact: true }).fill(value);
        await page.getByRole('heading', { name: 'Crear orden de compra' }).click();
        await page.waitForFunction(() => !document.querySelector('.validation-errors') && !document.querySelector('.validation-message'));
        assert.equal(posts, 0, 'Corregir campos no debe enviar una orden');
        const successResponse = page.waitForResponse(response => response.url().endsWith('/CreateOrder') && response.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        const success = await successResponse;
        const firstOrderId = await success.json();
        await page.getByRole('status').filter({ hasText: `Orden ${firstOrderId} creada.` }).waitFor();
        assert.equal(posts, 1);

        // Una segunda validación debe borrar errores y conservar las suscripciones sin duplicarlas.
        await page.getByLabel('Cantidad', { exact: true }).fill('0');
        await page.getByRole('heading', { name: 'Crear orden de compra' }).click();
        await page.waitForFunction(() => document.querySelector('.validation-message')?.textContent.includes('cantidad ordenada'));
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        await page.waitForTimeout(150);
        assert.equal(posts, 1);
        await page.getByLabel('Cantidad', { exact: true }).fill('1');
        await page.getByLabel('Id del cliente', { exact: true }).fill('ZZZZZ');
        const rejectedResponse = page.waitForResponse(response => response.url().endsWith('/CreateOrder') && response.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        assert.equal((await rejectedResponse).status(), 500);
        await page.locator('.alert-danger').waitFor();
        await page.getByRole('button', { name: 'Aceptar', exact: true }).click();
        await page.getByLabel('Id del cliente', { exact: true }).fill('ALFKI');
        const recoveredResponse = page.waitForResponse(response => response.url().endsWith('/CreateOrder') && response.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        const recovered = await recoveredResponse;
        assert.equal(recovered.status(), 200);
        const secondOrderId = await recovered.json();
        await page.getByRole('status').filter({ hasText: `Orden ${secondOrderId} creada.` }).waitFor();
        assert.equal(posts, 3);
        assert.equal(count(), initialCount + 2);
        const result = { passed: true, initialCount, finalCount: count(), firstOrderId, secondOrderId,
            posts, errorsClearedOnChange: true, serverErrorRecoveryPassed: true, duplicatedSubmissions: false };
        fs.writeFileSync('artifacts/verification7/blazor-recovery-results.json', JSON.stringify(result, null, 2));
        console.log(JSON.stringify(result));
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
