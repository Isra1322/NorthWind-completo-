const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium } = require('../../artifacts/verification/node_modules/playwright');

(async () => {
    const browser = await chromium.launch({ headless: true, channel: 'msedge' });
    try {
        const page = await browser.newPage();
        const markup = `<input id="registrationFirstName" maxlength="25"><input id="registrationLastName" maxlength="25">
            <input id="registrationEmail" type="email" maxlength="50"><input id="registrationPassword" type="password">
            <input id="registrationPasswordConfirmation" type="password">`;
        const origin = 'http://localhost:54329';
        await page.route(origin + '/**', route => route.fulfill({ contentType: 'text/html', body: markup }));
        await page.context().grantPermissions(['clipboard-read', 'clipboard-write'], { origin });
        await page.goto(origin + '/register');
        await page.addScriptTag({ path: path.resolve('NorthWind.Sales.BlazorClient/wwwroot/inputFilters.js') });
        await page.locator('input').evaluateAll(inputs => inputs.forEach(input => input.addEventListener('change', () => { input.dataset.boundValue = input.value; })));
        const paste = (input, text) => input.evaluate((element, value) => {
            const clipboard = new DataTransfer(); clipboard.setData('text/plain', value);
            element.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: clipboard }));
        }, text);
        for (const id of ['registrationFirstName', 'registrationLastName']) {
            const input = page.locator('#' + id);
            for (const invalid of ['12345', '----', "''''", '++++', "----''''", '....', '/\\=_{}[]@', '   ']) {
                await input.fill(''); await input.pressSequentially(invalid);
                assert.equal(await input.inputValue(), '');
                await paste(input, invalid);
                assert.equal(await input.inputValue(), '');
            }
            for (const valid of ['Juan', 'María José', 'José Luis', 'Ana-María', "O'Connor", 'Pérez García', 'Muñoz', "O'Neill", 'Pérez-García', 'J. Pérez']) {
                await input.fill(''); await input.pressSequentially(valid);
                assert.equal(await input.inputValue(), valid);
                assert.equal(await input.getAttribute('data-bound-value'), valid);
            }
            await input.fill('Juan'); await input.pressSequentially('+123@');
            assert.equal(await input.inputValue(), 'Juan');
            await input.pressSequentially('   Pérez');
            assert.equal(await input.inputValue(), 'Juan Pérez');
            await input.evaluate(element => element.setSelectionRange(0, 4));
            await paste(input, "--''123");
            assert.equal(await input.inputValue(), 'Juan Pérez');
            assert.deepEqual(await input.evaluate(element => [element.selectionStart, element.selectionEnd]), [0, 4]);
            await paste(input, '--María123');
            assert.equal(await input.inputValue(), 'María Pérez');
            assert.equal(await input.getAttribute('data-bound-value'), 'María Pérez');
            assert.equal(await input.evaluate(element => element.selectionStart), 5);
            await input.press('Backspace');
            assert.equal(await input.inputValue(), 'Marí Pérez');
            await input.fill('A'.repeat(25)); await input.press('B');
            assert.equal((await input.inputValue()).length, 25);
            await input.evaluate(element => {
                element.value = 'Mari\u0301a12 Ñ';
                element.dispatchEvent(new InputEvent('input', { bubbles: true, isComposing: true }));
                element.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true }));
            });
            assert.equal(await input.inputValue(), 'María Ñ');
            assert.equal(await input.getAttribute('data-bound-value'), 'María Ñ');
            console.log('PASS ' + id + ': keyboard, paste, selection, cursor, deletion, accents/composition, binding and length');
        }
        const email = page.locator('#registrationEmail');
        for (const valid of ['usuario@gmail.com', 'nombre.apellido@outlook.com', 'usuario123@hotmail.com', 'nombre_apellido@gmail.com', 'usuario+ventas@empresa.com', '_nombre@empresa.com', '+nombre@empresa.com', 'user-name@xn--bcher-kva.de']) {
            await email.fill(''); await email.pressSequentially(valid);
            assert.equal(await email.inputValue(), valid);
            assert.equal(await email.getAttribute('data-bound-value'), valid);
        }
        await email.fill(''); await email.pressSequentially('usuario@');
        assert.equal(await email.inputValue(), 'usuario@');
        await email.pressSequentially('@gmail..com');
        assert.equal(await email.inputValue(), 'usuario@gmail.com');
        await email.evaluate(element => element.setSelectionRange(0, 7));
        await email.press('Backspace');
        assert.equal(await email.inputValue(), '@gmail.com');
        await email.pressSequentially('nuevo');
        assert.equal(await email.inputValue(), 'nuevo@gmail.com');
        await email.fill(''); await email.pressSequentially('----++++');
        assert.ok((await email.inputValue()).length <= 1);
        await email.fill(''); await paste(email, '----++++');
        assert.equal(await email.inputValue(), '');
        await paste(email, ' usuario{}[]\\\"+ventas@@empresa..com\t');
        assert.equal(await email.inputValue(), 'usuario+ventas@empresa.com');
        assert.equal(await email.getAttribute('data-bound-value'), 'usuario+ventas@empresa.com');
        await email.evaluate(element => element.setSelectionRange(0, 7));
        await paste(email, '{}[]\\\" ');
        assert.equal(await email.inputValue(), 'usuario+ventas@empresa.com');
        assert.deepEqual(await email.evaluate(element => [element.selectionStart, element.selectionEnd]), [0, 7]);
        await paste(email, 'nuevo{} ');
        assert.equal(await email.inputValue(), 'nuevo+ventas@empresa.com');
        assert.equal(await email.evaluate(element => element.selectionStart), 5);
        await email.press('Backspace');
        assert.equal(await email.inputValue(), 'nuev+ventas@empresa.com');
        const maximumEmail = 'a'.repeat(38) + '@example.com';
        await email.fill(maximumEmail);
        await email.evaluate(element => element.setSelectionRange(3, 3));
        await paste(email, '+{}');
        assert.equal(await email.inputValue(), maximumEmail);
        assert.equal(await email.evaluate(element => element.selectionStart), 3);
        assert.equal(await email.evaluate(element => document.activeElement === element), true);
        console.log('PASS email: valid aliases, incomplete prefix, duplicate @/dots, symbols, safe paste, caret, focus and 50-character limit');
        for (const [id, pasted, expected] of [
            ['registrationFirstName', '--María123 José@@', 'María José'],
            ['registrationLastName', "Pérez---García123", 'Pérez-García'],
            ['registrationEmail', ' usuario{}+ventas@@empresa..com ', 'usuario+ventas@empresa.com']
        ]) {
            const input = page.locator('#' + id);
            await input.fill('');
            await page.evaluate(text => navigator.clipboard.writeText(text), pasted);
            await input.press('Control+V');
            assert.equal(await input.inputValue(), expected);
            assert.equal(await input.getAttribute('data-bound-value'), expected);
        }
        console.log('PASS actual Ctrl+V sanitizes registration fields and updates binding notifications');
        for (const id of ['registrationPassword', 'registrationPasswordConfirmation']) {
            const password = 'Aa1!@#$_+-{}[]\\"';
            await page.locator('#' + id).pressSequentially(password);
            assert.equal(await page.locator('#' + id).inputValue(), password);
        }
        console.log('PASS password and confirmation retain all security symbols');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
