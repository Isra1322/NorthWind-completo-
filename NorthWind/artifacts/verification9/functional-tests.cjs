const fs = require('node:fs');
const path = require('node:path');
const net = require('node:net');
const assert = require('node:assert/strict');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require('../verification/node_modules/playwright');
const root = process.cwd();
const settingsPath = 'NorthWind.Sales.WebApi/appsettings.json';
const settingsOriginal = fs.readFileSync(settingsPath);
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const sql = statement => execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', 'NorthWindDB', '-E', '-h', '-1', '-b', '-y', '4096', '-w', '65535', '-Q', 'SET NOCOUNT ON; ' + statement], { encoding: 'utf8' }).trim();
const count = () => Number(sql('SELECT COUNT(*) FROM dbo.Orders;'));
const payload = details => ({ customerId: 'ALFKI', shipAddress: '31 Oriente 810', shipCity: 'Puebla', shipCountry: 'México', shipPostalCode: '72500', orderDetails: details.map(([productId, unitPrice, quantity]) => ({ productId, unitPrice, quantity })) });
const three = [[1,35,10],[3,65,20],[4,75,40]];
const duplicates = [...three,[1,35,5]];
const special = [...duplicates,[5,50,5]];
const results = [];
const sockets = new Set();
let connections = 0;
let api;
let browser;
let logs = '';
const logPath = 'artifacts/verification9/api-smtp-failure.log';
// Servidor de prueba local que rechaza STARTTLS: nunca acepta ni entrega correos.
const smtp = net.createServer(socket => {
    connections++;
    sockets.add(socket);
    socket.on('close', () => sockets.delete(socket));
    socket.on('error', () => {});
    socket.write('220 localhost SMTP test\r\n');
    socket.on('data', data => {
        if (data.toString().toUpperCase().startsWith('EHLO')) socket.write('250 localhost\r\n');
        else socket.end('550 Test server rejects delivery\r\n');
    });
});
async function stopApi() {
    if (api && api.exitCode === null) await new Promise(resolve => { api.once('exit', resolve); api.kill(); });
    api = null;
}
async function ready() {
    for (let i=0; i<100; i++) {
        if (api.exitCode !== null) throw new Error('API stopped: ' + logs);
        try { if ((await fetch('http://localhost:5254/swagger/v1/swagger.json')).ok) return; } catch {}
        await delay(300);
    }
    throw new Error('API readiness timeout');
}
function stored(id, details) {
    const rows = JSON.parse(sql(`SELECT ProductId,Quantity FROM dbo.OrderDetails WHERE OrderId=${id} ORDER BY ProductId FOR JSON PATH;`));
    const grouped = new Map();
    for (const d of details) grouped.set(d.productId, (grouped.get(d.productId) || 0) + d.quantity);
    assert.deepEqual(rows, [...grouped].sort((a,b)=>a[0]-b[0]).map(([ProductId,Quantity])=>({ProductId,Quantity})));
    const order = JSON.parse(sql(`SELECT CustomerId,ShippingType,DiscountType,Discount FROM dbo.Orders WHERE Id=${id} FOR JSON PATH;`))[0];
    assert.equal(order.CustomerId, 'ALFKI');
    assert.equal(order.ShippingType, 3);
    assert.equal(order.DiscountType, 1);
    assert.equal(order.Discount, 10);
}
async function apiTest(name, data, expected, mailAttempts) {
    const before = count(), smtpBefore = connections;
    const response = await fetch('http://localhost:5254/CreateOrder', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(data) });
    const body = await response.json();
    assert.equal(response.status, expected);
    assert.equal(connections - smtpBefore, mailAttempts);
    if (expected === 200) { assert.equal(count(), before+1); stored(body, data.orderDetails); }
    else { assert.equal(count(), before); assert.equal(body.instance, 'ProblemDetails/ValidationException'); assert.match(response.headers.get('content-type'), /^application\/problem\+json/); assert.ok(body.errors.length); }
    results.push({ name, passed:true, status:expected, smtpAttempts:connections-smtpBefore, before, after:count(), orderId:expected===200?body:undefined, errors:expected===400?body.errors:undefined });
    console.log(JSON.stringify(results.at(-1)));
}
async function fill(page, data) {
    await page.goto('https://localhost:7254/createorder');
    await page.getByRole('heading', {name:'Crear orden de compra'}).waitFor();
    for (const [label, prop] of [['Id del cliente','customerId'],['Dirección','shipAddress'],['Ciudad','shipCity'],['País','shipCountry'],['Código Postal','shipPostalCode']]) await page.getByLabel(label,{exact:true}).fill(data[prop]);
    for (let i=0;i<data.orderDetails.length;i++) {
        await page.getByRole('button',{name:'Agregar producto'}).click();
        for (const [label,prop] of [['Id de Producto','productId'],['Precio','unitPrice'],['Cantidad','quantity']]) await page.getByLabel(label,{exact:true}).nth(i).fill(String(data.orderDetails[i][prop]));
    }
}
async function blazorTest(name, data, attempts) {
    const page=await browser.newPage();
    try {
        await fill(page,data);
        const before=count(), smtpBefore=connections;
        const pending=page.waitForResponse(r=>r.url().endsWith('/CreateOrder') && r.request().method()==='POST');
        await page.getByRole('button',{name:'Enviar',exact:true}).click();
        const response=await pending;
        assert.equal(response.status(),200);
        const id=await response.json();
        await page.getByRole('status').filter({hasText:`Orden ${id} creada.`}).waitFor();
        assert.equal(count(),before+1);
        assert.equal(connections-smtpBefore,attempts);
        assert.equal(await page.locator('.alert-danger').count(),0);
        stored(id,data.orderDetails);
        await page.screenshot({path:`artifacts/verification9/${name}.png`,fullPage:true});
        results.push({name,passed:true,status:200,orderId:id,smtpAttempts:attempts});
        console.log(JSON.stringify(results.at(-1)));
    } finally {await page.close();}
}
(async()=>{
    fs.writeFileSync(logPath,'');
    await new Promise(resolve=>smtp.listen(0,'127.0.0.1',resolve));
    try {
        api=spawn('dotnet',[path.join(root,'NorthWind.Sales.WebApi/bin/Debug/net8.0/NorthWind.Sales.WebApi.dll')],{cwd:path.join(root,'NorthWind.Sales.WebApi'),windowsHide:true,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'https://localhost:7154;http://localhost:5254',SmtpOptions__SmtpHost:'127.0.0.1',SmtpOptions__SmtpHostPort:String(smtp.address().port),SmtpOptions__SmtpUserName:'',SmtpOptions__SmtpPassword:''}});
        for(const stream of [api.stdout,api.stderr]) stream.on('data',data=>{logs+=data.toString();fs.appendFileSync(logPath,data);});
        await ready();
        const swagger=await (await fetch('http://localhost:5254/swagger/v1/swagger.json')).json();
        assert.ok(swagger.paths['/CreateOrder'].post);
        results.push({name:'Swagger',passed:true});
        await apiTest('API-1-three',payload(three),200,0);
        await apiTest('API-2-duplicates',payload(duplicates),200,0);
        await apiTest('API-3-special-SMTP-failure',payload(special),200,1);
        await apiTest('API-5-customer', {...payload(special),customerId:'ALFKQ'},400,0);
        await apiTest('API-5-product',payload([...special,[40,20,1]]),400,0);
        await apiTest('API-5-stock',payload([[1,35,60],[3,65,20],[4,75,40],[1,35,60],[5,50,5]]),400,0);
        await apiTest('API-5-required', {...payload(special),customerId:'',shipAddress:''},400,0);
        assert.match(logs,/Error al enviar la notificaci.n de orden especial/);
        assert.match(logs,/SmtpException/);
        browser=await chromium.launch({channel:'chrome',headless:true});
        await blazorTest('Blazor-1-three',payload(three),0);
        await blazorTest('Blazor-2-duplicates',payload(duplicates),0);
        await blazorTest('Blazor-3-special-SMTP-failure',payload(special),1);
        const page=await browser.newPage();
        await fill(page,{...payload(special),customerId:'ALFKQ'});
        let posted=0;
        page.on('request',r=>{if(r.method()==='POST' && r.url().endsWith('/CreateOrder'))posted++;});
        const before=count(),smtpBefore=connections;
        await page.getByRole('button',{name:'Enviar',exact:true}).click();
        await page.locator('div.validation-message').filter({hasText:'cliente proporcionado no existe'}).waitFor();
        assert.equal(count(),before);assert.equal(connections,smtpBefore);assert.equal(posted,1);
        results.push({name:'Blazor-P7-P8-server-validation',passed:true,status:400,smtpAttempts:0});
        await page.getByLabel('Id del cliente',{exact:true}).fill('');
        await page.getByRole('button',{name:'Enviar',exact:true}).click();
        await page.locator('div.validation-message').filter({hasText:'Debe especificar el identificador del cliente'}).waitFor();
        await delay(200);
        assert.equal(posted,1);assert.equal(count(),before);assert.equal(connections,smtpBefore);
        results.push({name:'Blazor-P7-local-validation',passed:true,additionalPosts:0,smtpAttempts:0});
        await page.close();
        assert.equal(connections,2);
        assert.ok(fs.readFileSync(settingsPath).equals(settingsOriginal));
        results.push({name:'Configuration-preserved',passed:true,smtpAttemptsTotal:connections});
    } finally {
        await stopApi();
        if(browser)await browser.close();
        for(const socket of sockets)socket.destroy();
        await new Promise(resolve=>smtp.close(resolve));
        fs.writeFileSync('artifacts/verification9/functional-results.json',JSON.stringify(results,null,2));
    }
    console.log(JSON.stringify({passed:true,results:results.length,realMailtrapDelivery:false}));
})().catch(error=>{console.error(error);process.exitCode=1;});
