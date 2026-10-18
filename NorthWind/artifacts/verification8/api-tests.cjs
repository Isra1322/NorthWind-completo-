const fs = require('node:fs');
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const sql = query => execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', 'NorthWindDB', '-E', '-h', '-1', '-W', '-b', '-Q', `SET NOCOUNT ON; ${query}`], { encoding: 'utf8' }).trim();
const count = () => Number(sql('SELECT COUNT(*) FROM dbo.Orders;'));
const queries = () => Number(sql("SELECT COALESCE(SUM(qs.execution_count),0) FROM sys.dm_exec_query_stats qs CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st WHERE (CHARINDEX('FROM [Customers]', st.text) > 0 OR CHARINDEX('FROM [Products]', st.text) > 0) AND CHARINDEX('sys.dm_exec_query_stats', st.text) = 0;"));
const valid = () => ({ customerId: 'ALFKI', shipAddress: '31 Oriente 810', shipCity: 'Puebla', shipCountry: 'México', shipPostalCode: '72100', orderDetails: [{ productId: 1, unitPrice: 35, quantity: 10 }, { productId: 3, unitPrice: 65, quantity: 20 }, { productId: 4, unitPrice: 75, quantity: 40 }] });
const change = updates => Object.assign(valid(), updates);
const cases = [
    { name: 'API-1', description: 'Orden válida', payload: valid(), valid: true },
    { name: 'API-2', description: 'Cliente ALFKQ inexistente', payload: change({ customerId: 'ALFKQ' }), messages: ['cliente proporcionado no existe'], fields: ['CustomerId'] },
    { name: 'API-3-ANTON', description: 'ANTON no existe en los datos actuales', payload: change({ customerId: 'ANTON' }), messages: ['cliente proporcionado no existe'], fields: ['CustomerId'], fixtureDifference: true },
    { name: 'API-3-BONAP', description: 'Cliente existente con adeudo', payload: change({ customerId: 'BONAP' }), messages: ['adeudo pendiente'], fields: ['CustomerId'] },
    { name: 'API-4', description: 'Productos 10, 40 y 3; solamente 40 es inexistente', payload: change({ orderDetails: [{ productId: 10, unitPrice: 35, quantity: 1 }, { productId: 40, unitPrice: 65, quantity: 1 }, { productId: 3, unitPrice: 65, quantity: 20 }] }), messages: ['El producto 40 no existe.'], fields: ['OrderDetails[1].ProductId'], fixtureDifference: true },
    { name: 'API-5', description: 'Cantidades indicadas; producto 2 sin existencias', payload: change({ orderDetails: [{ productId: 1, unitPrice: 35, quantity: 10 }, { productId: 2, unitPrice: 35, quantity: 1 }, { productId: 3, unitPrice: 65, quantity: 20 }, { productId: 4, unitPrice: 75, quantity: 40 }, { productId: 1, unitPrice: 35, quantity: 11 }] }), messages: ['no suficiente para producto 2.'], fields: ['OrderDetails[1].Quantity'], fixtureDifference: true },
    { name: 'API-5-Acumulacion', description: 'Producto 1 repetido: 60+60 supera 100', payload: change({ orderDetails: [{ productId: 1, unitPrice: 35, quantity: 60 }, { productId: 1, unitPrice: 35, quantity: 60 }] }), messages: ['Cantidad 100 de 120 no suficiente para producto 1.'], fields: ['OrderDetails[0].Quantity'] },
    { name: 'P7-Basicos', description: 'Regresión de campos y productos inválidos', payload: change({ customerId: '', shipAddress: 'A'.repeat(61), shipCity: 'AB', shipCountry: '', shipPostalCode: 'A'.repeat(11), orderDetails: [{ productId: 0, unitPrice: 0, quantity: 0 }] }), fields: ['CustomerId', 'ShipAddress', 'ShipCity', 'ShipCountry', 'ShipPostalCode', 'OrderDetails[0].ProductId', 'OrderDetails[0].Quantity', 'OrderDetails[0].UnitPrice'], basic: true },
    { name: 'P7-Coleccion', description: 'Colección null', payload: change({ orderDetails: null }), fields: ['OrderDetails'], basic: true },
    { name: 'P7-Vacia', description: 'Colección vacía', payload: change({ orderDetails: [] }), fields: ['OrderDetails'], basic: true },
    { name: 'P7-K', description: 'Errores básicos bloquean consultas', payload: change({ customerId: 'X', orderDetails: [{ productId: 2, unitPrice: 35, quantity: 1 }] }), fields: ['CustomerId'], basic: true },
    { name: 'P7-PostalOpcional', description: 'Código postal opcional', payload: change({ shipPostalCode: null }), valid: true }
];

(async () => {
    const results = [];
    for (const test of cases) {
        const before = count();
        const queriesBefore = queries();
        const response = await fetch('http://localhost:5254/CreateOrder', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(test.payload) });
        const body = await response.json();
        const after = count();
        const result = { name: test.name, description: test.description, status: response.status, before, after, queryCount: queries() - queriesBefore, fixtureDifference: !!test.fixtureDifference };
        if (test.valid) {
            assert.equal(response.status, 200);
            assert.equal(after, before + 1);
            assert.ok(body > 0);
            result.orderId = body;
        } else {
            assert.equal(response.status, 400);
            assert.equal(body.status, 400);
            assert.equal(response.headers.get('content-type').split(';')[0], 'application/problem+json');
            assert.equal(body.type, 'https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1');
            assert.equal(body.instance, 'ProblemDetails/ValidationException');
            assert.equal(body.title, 'Error en los datos de entrada.');
            assert.equal(body.detail, 'Se encontraron uno o más errores de validación de datos.');
            for (const property of test.fields) assert.ok(body.errors.some(error => error.propertyName === property));
            for (const message of test.messages || []) assert.ok(body.errors.some(error => error.message.includes(message)));
            assert.equal(after, before, 'Se insertó una orden inválida');
            assert.ok(!JSON.stringify(body).includes('StackTrace') && !JSON.stringify(body).includes(' at '));
            if (test.basic) assert.equal(result.queryCount, 0);
            result.errors = body.errors;
        }
        result.passed = true;
        results.push(result);
        fs.writeFileSync('artifacts/verification8/api-results.json', JSON.stringify(results, null, 2));
        console.log(JSON.stringify(result));
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
