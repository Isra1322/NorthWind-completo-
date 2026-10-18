const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require('../verification/node_modules/playwright');
const root = process.cwd();
const settings = path.join(root, 'NorthWind.Sales.WebApi/appsettings.json');
const interactor = path.join(root, 'NorthWind.Sales.Backend.UseCases/CreateOrder/CreateOrderInteractor.cs');
const settingsOriginal = fs.readFileSync(settings);
const interactorOriginal = fs.readFileSync(interactor);
const results = [];
const count = () => Number(execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', 'NorthWindDB', '-E', '-h', '-1', '-W', '-b', '-Q', 'SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Orders;'], { encoding: 'utf8' }).trim());
const payload = { customerId: 'ALFKI', shipAddress: '31 Oriente 810', shipCity: 'Puebla', shipCountry: 'México', shipPostalCode: '72100', orderDetails: [{ productId: 1, unitPrice: 35, quantity: 10 }, { productId: 3, unitPrice: 65, quantity: 20 }, { productId: 4, unitPrice: 75, quantity: 40 }] };
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
let api;
let browser;
let modifiedBinary = false;
fs.writeFileSync('artifacts/verification8/appsettings.original.json', settingsOriginal);
fs.writeFileSync('artifacts/verification8/CreateOrderInteractor.original.cs', interactorOriginal);
async function start(name) {
    const output = `artifacts/verification8/api-${name}.log`;
    fs.writeFileSync(output, '');
    api = spawn('dotnet', [path.join(root, 'NorthWind.Sales.WebApi/bin/Debug/net8.0/NorthWind.Sales.WebApi.dll')], { cwd: path.join(root, 'NorthWind.Sales.WebApi'), env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'https://localhost:7154;http://localhost:5254' }, windowsHide: true });
    api.stdout.on('data', data => fs.appendFileSync(output, data));
    api.stderr.on('data', data => fs.appendFileSync(output, data));
    for (let i = 0; i < 60; i++) {
        if (api.exitCode !== null) throw new Error(`API ${name} stopped: ${fs.readFileSync(output, 'utf8')}`);
        try { if ((await fetch('http://localhost:5254/swagger/v1/swagger.json')).ok) return output; } catch {}
        await delay(500);
    }
    throw new Error('API readiness timeout');
}
async function stop() {
    if (!api) return;
    const child = api;
    api = null;
    if (child.exitCode !== null) return;
    await new Promise(resolve => { child.once('exit', resolve); child.kill(); });
}
function build(name) {
    try {
        const output = execFileSync('dotnet', ['build', 'NorthWind.Sales.WebApi/NorthWind.Sales.WebApi.csproj', '--no-restore', '-v', 'minimal'], { encoding: 'utf8', windowsHide: true, timeout: 120000 });
        fs.writeFileSync(`artifacts/verification8/build-${name}.log`, output);
    } catch (error) {
        fs.writeFileSync(`artifacts/verification8/build-${name}.log`, String(error.stdout) + String(error.stderr));
        throw error;
    }
}
async function apiCheck(name, expectedInstance, title, detail) {
    const before = count();
    const response = await fetch('http://localhost:5254/CreateOrder', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
    const body = await response.json();
    assert.equal(response.status, 500);
    assert.match(response.headers.get('content-type'), /^application\/problem\+json/);
    assert.equal(body.instance, expectedInstance);
    assert.equal(body.title, title);
    assert.equal(body.detail, detail);
    assert.equal(body.status, 500);
    assert.equal(body.errors, undefined);
    assert.equal(count(), before);
    const result = { name, passed: true, status: 500, body, before, after: count() };
    results.push(result);
    console.log(JSON.stringify(result));
}
async function fill(page) {
    await page.goto('https://localhost:7254/createorder');
    await page.getByRole('heading', { name: 'Crear orden de compra' }).waitFor();
    for (const [label, property] of [['Id del cliente', 'customerId'], ['Dirección', 'shipAddress'], ['Ciudad', 'shipCity'], ['País', 'shipCountry'], ['Código Postal', 'shipPostalCode']])
        await page.getByLabel(label, { exact: true }).fill(payload[property]);
    await page.getByRole('button', { name: 'Agregar producto' }).click();
    for (const [label, value] of [['Id de Producto', '1'], ['Precio', '35'], ['Cantidad', '10']])
        await page.getByLabel(label, { exact: true }).fill(value);
}
async function blazorCheck(name, message) {
    const page = await browser.newPage();
    try {
        await fill(page);
        const before = count();
        const pending = page.waitForResponse(response => response.url().endsWith('/CreateOrder') && response.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        assert.equal((await pending).status(), 500);
        const alert = page.locator('.alert-danger');
        await alert.waitFor();
        assert.equal(await alert.innerText(), message);
        assert.equal(count(), before);
        await page.screenshot({ path: `artifacts/verification8/${name}.png`, fullPage: true });
        await page.getByRole('button', { name: 'Aceptar', exact: true }).click();
        await page.getByRole('button', { name: 'Enviar', exact: true }).waitFor();
        assert.equal(await page.locator('.alert-danger').count(), 0);
        results.push({ name, passed: true, status: 500, friendlyMessage: message, boundaryRecovery: true, before, after: count() });
        console.log(JSON.stringify(results.at(-1)));
    } finally { await page.close(); }
}
(async () => {
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    try {
        // La configuración y el código originales se restauran siempre, incluso si falla una prueba.
        try {
            const broken = JSON.parse(settingsOriginal.toString('utf8'));
            broken.DBOptions.ConnectionString = 'Server=(localdb)\\mssqllocaldb;Database=NorthWindDB_Practica8_NoExiste;Connect Timeout=3';
            fs.writeFileSync(settings, JSON.stringify(broken, null, 2));
            const log = await start('general-error');
            await apiCheck('API-6-General', 'ProblemDetails/InvalidOperationException', 'El servicio no se encuentra disponible.', 'Consulte al administrador.');
            await blazorCheck('Blazor-5-General', 'El servicio no se encuentra disponible. Consulte al administrador.');
            assert.match(fs.readFileSync(log, 'utf8'), /SqlException/);
            assert.match(fs.readFileSync(log, 'utf8'), /UnhandledExceptionHandler/);
        } finally { fs.writeFileSync(settings, settingsOriginal); await stop(); }
        try {
            const source = interactorOriginal.toString('utf8');
            const anchor = 'OrderAggregate Order = OrderAggregate.From(orderDto);';
            assert.equal(source.split(anchor).length, 2);
            fs.writeFileSync(interactor, source.replace(anchor, `${anchor}\n        Order.CustomerId = "";`));
            modifiedBinary = true;
            build('temporary-update');
            const log = await start('update-error');
            await apiCheck('API-7-Update', 'ProblemDetails/UpdateException', 'Error de actualización.', 'Se encontraron uno o más errores de actualización de datos. Consulte al administrador.');
            await blazorCheck('Blazor-6-Update', 'Error de actualización. Se encontraron uno o más errores de actualización de datos. Consulte al administrador.');
            assert.match(fs.readFileSync(log, 'utf8'), /DbUpdateException/);
            assert.match(fs.readFileSync(log, 'utf8'), /FOREIGN KEY/);
            assert.match(fs.readFileSync(log, 'utf8'), /OrderAggregate/);
        } finally { fs.writeFileSync(interactor, interactorOriginal); await stop(); build('restored'); modifiedBinary = false; }
        await start('recovery');
        const before = count();
        const response = await fetch('http://localhost:5254/CreateOrder', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
        assert.equal(response.status, 200);
        const orderId = await response.json();
        assert.equal(count(), before + 1);
        results.push({ name: 'API-8-Recuperacion', passed: true, status: 200, orderId, before, after: count() });
        const page = await browser.newPage();
        await fill(page);
        const accepted = page.waitForResponse(r => r.url().endsWith('/CreateOrder') && r.request().method() === 'POST');
        await page.getByRole('button', { name: 'Enviar', exact: true }).click();
        const success = await accepted;
        assert.equal(success.status(), 200);
        const blazorId = await success.json();
        await page.getByRole('status').filter({ hasText: `Orden ${blazorId} creada.` }).waitFor();
        await page.screenshot({ path: 'artifacts/verification8/blazor-recovery.png', fullPage: true });
        results.push({ name: 'Blazor-7-Recuperacion', passed: true, status: 200, orderId: blazorId });
        await page.close();
    } finally {
        fs.writeFileSync(settings, settingsOriginal);
        fs.writeFileSync(interactor, interactorOriginal);
        await stop();
        if (modifiedBinary) build('restored-finally');
        if (browser) await browser.close();
        fs.writeFileSync('artifacts/verification8/fault-results.json', JSON.stringify(results, null, 2));
    }
    assert.ok(fs.readFileSync(settings).equals(settingsOriginal));
    assert.ok(fs.readFileSync(interactor).equals(interactorOriginal));
    console.log(JSON.stringify({ passed: true, configurationRestored: true, sourceRestored: true, results: results.length }));
})().catch(error => { console.error(error); process.exitCode = 1; });
