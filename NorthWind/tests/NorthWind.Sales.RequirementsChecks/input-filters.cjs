const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium } = require('../../artifacts/verification/node_modules/playwright');
(async () => {
    const browser = await chromium.launch({ headless: true, channel: 'msedge' });
    try {
        const page = await browser.newPage();
        await page.setContent(`<input id="registrationFirstName" maxlength="25"><input id="registrationLastName" maxlength="25"><input id="registrationEmail" type="email" maxlength="50">
            <input id="shipAddress" maxlength="60"><input id="shipCity" maxlength="15"><input id="shipCountry" maxlength="15">
            <input id="shipPostalCode" maxlength="10"><input id="searchId" maxlength="10"><input id="searchName" data-input-rule="customerName" maxlength="50"><input id="productName" data-input-rule="product" maxlength="50">`);
        await page.addScriptTag({ path: path.resolve('NorthWind.Sales.BlazorClient/wwwroot/inputFilters.js') });
        assert.equal(await page.locator('#registrationEmail').getAttribute('type'), 'text');
        assert.equal(await page.locator('#registrationEmail').getAttribute('inputmode'), 'email');
        await page.locator('#registrationEmail').fill("ventas+quito{} []\\\"@empresa.com");
        assert.equal(await page.locator('#registrationEmail').inputValue(), 'ventas+quito@empresa.com');
        console.log('PASS email input and paste filtering preserves plus aliases');
        await page.locator('#registrationEmail').fill('oscar@gmail.com');
        await page.locator('#registrationEmail').evaluate(input => {
            input.focus(); input.setSelectionRange(5, 5);
            input.dispatchEvent(new InputEvent('beforeinput', { bubbles: true, cancelable: true, inputType: 'insertText', data: '{}[]\\ ' }));
        });
        assert.equal(await page.locator('#registrationEmail').inputValue(), 'oscar@gmail.com');
        assert.equal(await page.locator('#registrationEmail').evaluate(input => input.selectionStart), 5);
        await page.keyboard.type('+');
        assert.equal(await page.locator('#registrationEmail').inputValue(), 'oscar+@gmail.com');
        console.log('PASS blocked email typing preserves selection and legitimate plus remains typeable');
        for (const [id, raw, expected] of [
            ['registrationFirstName', 'José12+@ Ñü', 'José Ñü'], ['registrationLastName', "O’Connor-Pérez.12", 'O’Connor-Pérez.'],
            ['shipCity', 'Puebla+++12', 'Puebla'], ['shipCountry', '529455644864', ''],
            ['shipPostalCode', '0017A+-', '0017'], ['searchId', '0910@abc', '0910'],
            ['shipAddress', 'Av. Los Shyris N.º 25-10', 'Av. Los Shyris N.º 25-10'],
            ['shipAddress', 'Calle Bolívar #123', 'Calle Bolívar #123'], ['searchName', 'José12+ Ñúñez', 'José Ñúñez'],
            ['productName', "Chef Anton's Cajun Seasoning", "Chef Anton's Cajun Seasoning"],
            ['productName', 'Chocolate 70% @*', 'Chocolate 70% ']
        ]) {
            await page.locator('#' + id).fill(raw);
            assert.equal(await page.locator('#' + id).inputValue(), expected);
            console.log('PASS typing/paste filtering: ' + id);
        }
        await page.locator('#registrationFirstName').fill('José Pérez');
        await page.locator('#registrationFirstName').evaluate(input => { input.focus(); input.setSelectionRange(4, 4); });
        await page.keyboard.type('12+');
        assert.equal(await page.locator('#registrationFirstName').inputValue(), 'José Pérez');
        assert.equal(await page.locator('#registrationFirstName').evaluate(input => input.selectionStart), 4);
        await page.keyboard.press('ArrowLeft');
        await page.keyboard.press('Backspace');
        assert.equal(await page.locator('#registrationFirstName').inputValue(), 'Joé Pérez');
        console.log('PASS invalid typing preserves cursor; navigation and deletion remain available');
        await page.locator('#registrationFirstName').evaluate(input => {
            input.value = 'Ana'; input.setSelectionRange(0, 3);
            input.dispatchEvent(new InputEvent('beforeinput', { bubbles: true, cancelable: true, inputType: 'insertFromPaste', data: 'María+12' }));
        });
        assert.equal(await page.locator('#registrationFirstName').inputValue(), 'María');
        console.log('PASS mixed paste replaces selection with valid characters');
        for (const id of ['shipCity', 'shipCountry']) {
            const place = page.locator('#' + id);
            await place.evaluate(input => input.addEventListener('change', () => { input.dataset.boundValue = input.value; }));
            for (const invalid of ['----', '....', "''''", '++++', '12345', "--''..", '@@@', '   ', '"*#%']) {
                await place.fill('');
                await place.pressSequentially(invalid);
                assert.equal(await place.inputValue(), '');
                await place.evaluate((input, text) => {
                    const clipboard = new DataTransfer(); clipboard.setData('text/plain', text);
                    input.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: clipboard }));
                }, invalid);
                assert.equal(await place.inputValue(), '');
            }
            for (const valid of ['Ambato', 'Quito', 'Santo Domingo', 'Baños', 'Ecuador', 'Costa Rica', 'Reino Unido', 'Timor-Leste', "L'Aquila", 'L’Haÿ']) {
                await place.fill('');
                await place.pressSequentially(valid);
                assert.equal(await place.inputValue(), valid);
                assert.equal(await place.getAttribute('data-bound-value'), valid);
            }
            await place.fill('');
            await place.pressSequentially('Ecuador123');
            assert.equal(await place.inputValue(), 'Ecuador');
            await place.fill('');
            await place.pressSequentially('Quito+++');
            assert.equal(await place.inputValue(), 'Quito');
            await place.fill('');
            await place.pressSequentially("Timor---''Leste");
            assert.equal(await place.inputValue(), 'Timor-Leste');
            await place.evaluate(input => { input.focus(); input.setSelectionRange(0, 5); });
            await place.evaluate(input => {
                const clipboard = new DataTransfer(); clipboard.setData('text/plain', "--''..123");
                input.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: clipboard }));
            });
            assert.equal(await place.inputValue(), 'Timor-Leste');
            assert.deepEqual(await place.evaluate(input => [input.selectionStart, input.selectionEnd]), [0, 5]);
            await place.evaluate(input => {
                const clipboard = new DataTransfer(); clipboard.setData('text/plain', '..Quito++123');
                input.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: clipboard }));
            });
            assert.equal(await place.inputValue(), 'Quito-Leste');
            assert.equal(await place.getAttribute('data-bound-value'), 'Quito-Leste');
            assert.equal(await place.evaluate(input => input.selectionStart), 5);
            await place.press('Backspace');
            assert.equal(await place.inputValue(), 'Quit-Leste');
            await place.evaluate(input => input.setSelectionRange(0, 4));
            await place.press('Backspace');
            assert.equal(await place.inputValue(), 'Leste');
            assert.equal(await place.getAttribute('data-bound-value'), 'Leste');
            await place.fill('');
            await place.pressSequentially('Baños de Agua Santa');
            assert.equal((await place.inputValue()).length, 15);
            await place.fill('');
            await place.evaluate(input => {
                input.value = '--Qu1ito++'; input.setSelectionRange(6, 6);
                input.dispatchEvent(new InputEvent('input', { bubbles: true }));
            });
            assert.equal(await place.inputValue(), 'Quito');
            assert.equal(await place.getAttribute('data-bound-value'), 'Quito');
            assert.equal(await place.evaluate(input => input.selectionStart), 3);
            console.log('PASS ' + id + ': keyboard, paste, valid places, binding notification, selection, caret, deletion, input fallback and 15-character limit');
        }
        const customer = page.locator('#searchName');
        for (const value of ['---', "'''", "--''", '+++', '@@@', '123456', '   ...']) {
            await customer.fill('');
            await customer.pressSequentially(value);
            assert.equal(await customer.inputValue(), '');
        }
        for (const value of ['Juan Pérez', 'María José López', 'Comercial Alfa', 'Distribuidora Anatra', "O'Connor", 'Jean-Pierre', 'Comercial S.A.']) {
            await customer.fill('');
            await customer.pressSequentially(value);
            assert.equal(await customer.inputValue(), value);
        }
        await customer.fill('Jean');
        await customer.pressSequentially("---''Pierre");
        assert.equal(await customer.inputValue(), 'Jean-Pierre');
        await customer.evaluate(input => {
            input.focus(); input.setSelectionRange(0, 4);
            const data = new DataTransfer(); data.setData('text/plain', '---123@@@');
            input.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: data }));
        });
        assert.equal(await customer.inputValue(), 'Jean-Pierre');
        assert.equal(await customer.evaluate(input => input.selectionStart), 0);
        await customer.evaluate(input => {
            const data = new DataTransfer(); data.setData('text/plain', '--María123');
            input.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: data }));
        });
        assert.equal(await customer.inputValue(), 'María-Pierre');
        assert.equal(await customer.evaluate(input => input.selectionStart), 5);
        await customer.press('Backspace');
        assert.equal(await customer.inputValue(), 'Marí-Pierre');
        await customer.evaluate(input => { input.setSelectionRange(0, 5); });
        await customer.press('Backspace');
        assert.equal(await customer.inputValue(), 'Pierre');
        await customer.fill('A'.repeat(50));
        await customer.press('B');
        assert.equal((await customer.inputValue()).length, 50);
        console.log('PASS customer names: contextual separators, real names, safe paste, selection, caret, deletion and 50-character limit');
        const product = page.locator('#productName');
        await product.evaluate(input => input.addEventListener('input', () => { input.dataset.boundValue = input.value; }));
        for (const invalid of ['----', "''''", '////', "--''//", '.....', '++++', '@@@@', '   ']) {
            await product.fill('');
            await product.pressSequentially(invalid);
            assert.equal(await product.inputValue(), '');
            await product.evaluate((input, text) => {
                const data = new DataTransfer(); data.setData('text/plain', text);
                input.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: data }));
            }, invalid);
            assert.equal(await product.inputValue(), '');
        }
        for (const valid of ['Chai', 'Aniseed Syrup', "Chef Anton's Cajun Seasoning", 'Coca-Cola', 'Producto 123', 'Café molido', '7 Up', 'Chocolate (70%)', 'Mix + vitamina C', 'Aceite 1/2 L', 'Queso, leche y sal', 'Salsa "Extra"']) {
            await product.fill('');
            await product.pressSequentially(valid);
            assert.equal(await product.inputValue(), valid);
            assert.equal(await product.getAttribute('data-bound-value'), valid);
        }
        await product.fill('Coca-Cola');
        await product.evaluate(input => { input.focus(); input.setSelectionRange(0, 4); });
        await product.evaluate(input => {
            const data = new DataTransfer(); data.setData('text/plain', "--''//@@");
            input.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: data }));
        });
        assert.equal(await product.inputValue(), 'Coca-Cola');
        assert.deepEqual(await product.evaluate(input => [input.selectionStart, input.selectionEnd]), [0, 4]);
        await product.evaluate(input => {
            const data = new DataTransfer(); data.setData('text/plain', '@@Café@@ 123');
            input.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: data }));
        });
        assert.equal(await product.inputValue(), 'Café 123-Cola');
        assert.equal(await product.getAttribute('data-bound-value'), 'Café 123-Cola');
        assert.equal(await product.evaluate(input => input.selectionStart), 8);
        await product.press('Backspace');
        assert.equal(await product.inputValue(), 'Café 12-Cola');
        await product.evaluate(input => input.setSelectionRange(0, 7));
        await product.press('Backspace');
        assert.equal(await product.inputValue(), 'Cola');
        await product.fill('');
        await product.pressSequentially('Coca----Cola');
        assert.equal(await product.inputValue(), 'Coca-Cola');
        await product.fill('');
        await product.pressSequentially('A'.repeat(51));
        assert.equal((await product.inputValue()).length, 50);
        assert.equal(await product.evaluate(input => document.activeElement === input), true);
        console.log('PASS product input: symbols blocked, commercial names, paste, selection, caret, deletion, binding and maximum length');
        await page.addStyleTag({ path: path.resolve('NorthWind.Sales.Frontend.Views/Components/CreateOrderComponent.razor.css') });
        await page.setContent('<div id="container"><table style="width:100%"><tr><td>Productos</td></tr></table><div class="order-totals"><span>Subtotal:</span><span>100,00</span><span>IVA (15 %):</span><span>15,00</span><strong>Total:</strong><strong>115,00</strong></div></div>');
        await page.addStyleTag({ path: path.resolve('NorthWind.Sales.Frontend.Views/Components/CreateOrderComponent.razor.css') });
        for (const width of [375, 1280]) {
            await page.setViewportSize({ width, height: 800 });
            const layout = await page.evaluate(() => {
                const totals = document.querySelector('.order-totals');
                return {
                    right: totals.getBoundingClientRect().right,
                    containerRight: document.querySelector('#container').getBoundingClientRect().right,
                    amounts: [...totals.children].filter((_, i) => i % 2 === 1).map(x => x.getBoundingClientRect().right),
                    overflows: document.documentElement.scrollWidth > innerWidth,
                    values: totals.textContent
                };
            });
            assert.ok(Math.abs(layout.right - layout.containerRight) < 1);
            assert.ok(layout.amounts.every(right => Math.abs(right - layout.right) < 1));
            assert.equal(layout.overflows, false);
            assert.equal(layout.values, 'Subtotal:100,00IVA (15 %):15,00Total:115,00');
            console.log('PASS totals align right without overflow or value changes at width ' + width);
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
