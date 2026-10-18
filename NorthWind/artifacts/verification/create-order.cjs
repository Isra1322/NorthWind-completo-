const { chromium } = require('playwright');
const fs = require('node:fs');

(async () => {
    const browser = await chromium.launch({ channel: 'chrome', headless: true });
    const context = await browser.newContext();
    const page = await context.newPage();
    const errors = [];
    page.on('response', response => {
        if (response.status() >= 400) errors.push(`${response.status()} ${response.url()}`);
    });
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => {
        if (message.type() === 'error') errors.push(message.text());
    });
    try {
        await page.goto('https://localhost:7254/createorder');
        await page.getByRole('heading', { name: 'Crear orden de compra' }).waitFor();
        await page.getByLabel('Id del cliente', { exact: true }).fill('ALFKI');
        await page.getByLabel('Dirección', { exact: true }).fill('31 Oriente 810');
        await page.getByLabel('Ciudad', { exact: true }).fill('Puebla');
        await page.getByLabel('País', { exact: true }).fill('México');
        await page.getByLabel('Código Postal', { exact: true }).fill('72500');
        await page.getByRole('button', { name: 'Agregar producto' }).click();
        await page.getByLabel('Id de Producto', { exact: true }).fill('1');
        await page.getByLabel('Precio', { exact: true }).fill('17');
        await page.getByLabel('Cantidad', { exact: true }).fill('5');
        const responsePromise = page.waitForResponse(response =>
            response.url().endsWith('/CreateOrder') && response.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        const response = await responsePromise;
        const orderId = await response.json();
        const message = `Orden ${orderId} creada.`;
        await page.getByRole('status').filter({ hasText: message }).waitFor();
        if (response.status() !== 200 || orderId <= 0 || errors.length)
            throw new Error(JSON.stringify({ status: response.status(), orderId, errors }));
        await page.screenshot({ path: 'artifacts/verification/create-order.png', fullPage: true });
        const result = { status: response.status(), orderId, message, errors,
            frontend: 'https://localhost:7254/createorder',
            backend: response.url(), certificateErrorsIgnored: false };
        fs.writeFileSync('artifacts/verification/browser-result.json', JSON.stringify(result, null, 2));
        console.log(JSON.stringify(result, null, 2));
    } catch (error) {
        await page.screenshot({ path: 'artifacts/verification/create-order-failure.png', fullPage: true });
        console.error(error);
        console.error(JSON.stringify(errors));
        process.exitCode = 1;
    } finally {
        await browser.close();
    }
})();
