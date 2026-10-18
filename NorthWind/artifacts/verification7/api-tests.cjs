const fs = require('node:fs');
const { execFileSync } = require('node:child_process');
const assert = require('node:assert/strict');
const sql = query => execFileSync('sqlcmd', ['-S', '(localdb)\\mssqllocaldb', '-d', 'NorthWindDB', '-E', '-h', '-1', '-W', '-b', '-Q', `SET NOCOUNT ON; ${query}`], { encoding: 'utf8' }).trim();
const count = () => Number(sql('SELECT COUNT(*) FROM dbo.Orders;'));
const queryExecutions = () => Number(sql("SELECT COALESCE(SUM(qs.execution_count),0) FROM sys.dm_exec_query_stats qs CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st WHERE (CHARINDEX('FROM [Customers]', st.text) > 0 OR CHARINDEX('FROM [Products]', st.text) > 0) AND CHARINDEX('sys.dm_exec_query_stats', st.text) = 0;"));
const valid = () => ({ customerId: 'ALFKI', shipAddress: '31 Oriente 810', shipCity: 'Puebla', shipCountry: 'México', shipPostalCode: '72100', orderDetails: [{ productId: 1, unitPrice: 35, quantity: 10 }, { productId: 3, unitPrice: 65, quantity: 20 }, { productId: 4, unitPrice: 75, quantity: 40 }] });
const change = updates => Object.assign(valid(), updates);
const cases = [
    { name: 'A', description: 'Datos válidos', payload: valid(), valid: true },
    { name: 'B', description: 'Cliente vacío', payload: change({ customerId: '' }), errors: ['CustomerId:', 'Debe especificar el identificador del cliente.'], basic: true },
    { name: 'C', description: 'Cliente y productos vacíos', payload: change({ customerId: '', orderDetails: [] }), errors: ['CustomerId:', 'OrderDetails:'], basic: true },
    { name: 'D', description: 'Longitudes de envío', payload: change({ customerId: 'AB', shipAddress: 'A'.repeat(61), shipCity: 'AB', shipCountry: 'A'.repeat(16), shipPostalCode: 'A'.repeat(11) }), errors: ['CustomerId:', 'ShipAddress:', 'ShipCity:', 'ShipCountry:', 'ShipPostalCode:'], basic: true },
    { name: 'E', description: 'Producto, precio y cantidad inválidos', payload: change({ orderDetails: [{ productId: 0, unitPrice: 0, quantity: 0 }] }), errors: ['OrderDetails[0].ProductId:', 'OrderDetails[0].UnitPrice:', 'OrderDetails[0].Quantity:'], basic: true },
    { name: 'F', description: 'Cliente inexistente', payload: change({ customerId: 'ZZZZZ' }), errors: ['CustomerId:', 'proporcionado no existe'] },
    { name: 'G', description: 'Cliente con adeudo', payload: change({ customerId: 'BONAP' }), errors: ['CustomerId:', 'adeudo pendiente'] },
    { name: 'H', description: 'Producto inexistente', payload: change({ orderDetails: [{ productId: 999, unitPrice: 35, quantity: 1 }] }), errors: ['OrderDetails[0].ProductId:', 'El producto 999 no existe.'] },
    { name: 'I', description: 'Existencias insuficientes', payload: change({ orderDetails: [{ productId: 2, unitPrice: 35, quantity: 1 }] }), errors: ['OrderDetails[0].Quantity:', 'no suficiente para producto 2'] },
    { name: 'J', description: 'Cliente inexistente y producto sin existencias', payload: change({ customerId: 'ZZZZZ', orderDetails: [{ productId: 2, unitPrice: 35, quantity: 1 }] }), errors: ['CustomerId:', 'OrderDetails[0].Quantity:'] },
    { name: 'K', description: 'Cliente inválido bloquea consultas SQL', payload: change({ customerId: 'X', orderDetails: [{ productId: 2, unitPrice: 35, quantity: 1 }] }), errors: ['CustomerId:', '5 caracteres'], basic: true },
    { name: 'L', description: 'Productos repetidos superan existencias', payload: change({ orderDetails: [{ productId: 10, unitPrice: 35, quantity: 6 }, { productId: 10, unitPrice: 35, quantity: 6 }] }), errors: ['OrderDetails[0].Quantity:', 'Cantidad 10 de 12 no suficiente para producto 10.'] },
    { name: 'M', description: 'OrderDetails null', payload: change({ orderDetails: null }), errors: ['OrderDetails:', 'Debe especificar los productos'], basic: true },
    { name: 'N', description: 'Código postal opcional null', payload: change({ shipPostalCode: null }), valid: true },
    { name: 'O', description: 'Productos repetidos válidos se agrupan', payload: change({ orderDetails: [{ productId: 10, unitPrice: 35, quantity: 2 }, { productId: 10, unitPrice: 35, quantity: 3 }] }), valid: true, duplicate: true },
    { name: 'P', description: 'Errores en segunda fila de productos', payload: change({ orderDetails: [{ productId: 1, unitPrice: 35, quantity: 1 }, { productId: 0, unitPrice: -1, quantity: -1 }] }), errors: ['OrderDetails[1].ProductId:', 'OrderDetails[1].Quantity:', 'OrderDetails[1].UnitPrice:'], basic: true }
];

(async () => {
    const results = [];
    const initialCount = count();
    for (const test of cases) {
        const before = count();
        const queriesBefore = queryExecutions();
        const response = await fetch('http://localhost:5254/CreateOrder', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(test.payload) });
        const body = await response.text();
        await new Promise(resolve => setTimeout(resolve, 150));
        const after = count();
        const queryCount = queryExecutions() - queriesBefore;
        const result = { name: test.name, description: test.description, status: response.status, before, after, queryCount };
        if (test.valid) {
            assert.equal(response.status, 200, body);
            assert.equal(after, before + 1);
            result.orderId = JSON.parse(body);
            assert.ok(result.orderId > 0);
            assert.equal(sql(`SELECT COUNT(*) FROM dbo.Orders WHERE Id = ${result.orderId} AND ShippingType = 3 AND DiscountType = 1 AND Discount = 10;`), '1');
            if (test.duplicate) assert.equal(sql(`SELECT COUNT(*) FROM dbo.OrderDetails WHERE OrderId = ${result.orderId} AND ProductId = 10 AND Quantity = 5;`), '1');
        } else {
            assert.equal(response.status, 500, body);
            assert.equal(after, before, 'Se insertó una orden inválida');
            for (const error of test.errors) assert.ok(body.includes(error), `No aparece: ${error}\n${body}`);
            result.errors = body.split('\n')[0].replace(/^System.Exception: /, '').trim();
            if (test.basic) {
                assert.equal(queryCount, 0, 'La validación básica no debe consultar SQL');
                assert.ok(!body.includes('adeudo pendiente') && !body.includes('no suficiente para producto'));
            } else assert.equal(queryCount, 2, 'Deben ejecutarse las consultas de cliente y productos');
        }
        result.passed = true;
        results.push(result);
        fs.writeFileSync('artifacts/verification7/api-results.json', JSON.stringify({ initialCount, finalCount: count(), results }, null, 2));
        console.log(JSON.stringify(result));
    }
    const originalOrders = sql('SELECT COUNT(*) FROM dbo.Orders WHERE Id BETWEEN 1 AND 5;');
    assert.equal(originalOrders, '5', 'Deben conservarse las órdenes anteriores');
})().catch(error => { console.error(error); process.exitCode = 1; });
