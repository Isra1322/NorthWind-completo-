// Capture before Blazor receives input so typing, paste and autofill share the same rules.
(() => {
    const letter = c => /[A-Za-z\u00c0-\u024f]/u.test(c) && /\p{L}/u.test(c) && !'ºª'.includes(c);
    const rules = {
        email: c => /[A-Za-z0-9@._+\-]/.test(c),
        digits: c => /[0-9]/.test(c),
        name: c => letter(c) || " -.'’".includes(c),
        place: c => letter(c) || " -'’".includes(c),
        address: c => letter(c) || /[0-9]/.test(c) || " .,-'#/ºª°():’&".includes(c),
        business: c => letter(c) || /[0-9]/.test(c) || " -.'’&,()/".includes(c),
        product: c => letter(c) || /[0-9]/.test(c) || " -.'’&,()/:;%+ºª°#[]\"".includes(c)
    };
    const fixed = {
        registrationEmail: 'email',
        registrationFirstName: 'name', registrationLastName: 'name',
        shipAddress: 'address', shipCity: 'place', shipCountry: 'place',
        shipPostalCode: 'digits', invoiceSearchValue: 'digits', searchId: 'digits'
    };
    const ruleFor = element => element instanceof HTMLInputElement && rules[element.dataset.inputRule || fixed[element.id]];
    const clean = (value, rule) => [...value.normalize('NFC')].filter(rule).join('');
    // Only the customer lookup uses contextual separators. A trailing separator
    // is retained while typing O'Connor or Jean-Pierre.
    const customerName = value => {
        let result = '';
        for (const c of value.normalize('NFC')) {
            const previous = result.at(-1);
            if (letter(c) || (" -.'’".includes(c) && previous &&
                (letter(previous) || (c === ' ' && previous === '.')))) result += c;
        }
        return result;
    };
    const isCustomerName = input => input instanceof HTMLInputElement && input.dataset.inputRule === 'customerName';
    const isDeliveryPlace = input => input instanceof HTMLInputElement && ['shipCity', 'shipCountry'].includes(input.id);
    const placeName = value => {
        let result = '';
        for (const c of value.normalize('NFC')) {
            if (letter(c) || (" -'’".includes(c) && result.length > 0 && letter(result.at(-1)))) result += c;
        }
        return result;
    };
    const productName = value => {
        let result = '', significant = '';
        const alphanumeric = c => letter(c) || /^[0-9]$/.test(c);
        for (const c of value.normalize('NFC')) {
            if (!rules.product(c)) continue;
            if (alphanumeric(c) || (result.length > 0 && (c === ' '
                ? result.at(-1) !== ' '
                : alphanumeric(significant) || (')]\"'.includes(c) && c !== significant && '%ºª°)]'.includes(significant))))) {
                result += c;
                if (c !== ' ') significant = c;
            }
        }
        return result;
    };
    const isProductName = input => input instanceof HTMLInputElement && input.dataset.inputRule === 'product';
    const isRegistrationName = input => input instanceof HTMLInputElement && ['registrationFirstName', 'registrationLastName'].includes(input.id);
    const isRegistrationEmail = input => input instanceof HTMLInputElement && input.id === 'registrationEmail';
    // Keep incomplete addresses editable. Complete syntax remains a blur/submit rule.
    const emailInput = (value, prefixOnly = false) => {
        let result = '', domain = false, labelStart = false;
        for (const c of value) {
            if (!rules.email(c)) continue;
            const previous = result.at(-1);
            if (c === '@') {
                if (domain || previous === '.') continue;
                domain = true; labelStart = true;
            } else if (c === '.') {
                if (!result || previous === '.' || previous === '@' || (domain && previous === '-')) continue;
                labelStart = domain;
            } else if (domain) {
                if (c === '_' || c === '+' || (c === '-' && labelStart)) continue;
                labelStart = false;
            }
            result += c;
        }
        // One leading local-part sign can be a legitimate prefix (_user, +user).
        // A run of signs without a letter/digit is never retained.
        return prefixOnly || /[A-Za-z0-9]/.test(result) || /^[+_@-]$/.test(result) ? result : '';
    };
    const contextualCleaner = input => isRegistrationEmail(input) ? emailInput : isRegistrationName(input) ? customerName : isDeliveryPlace(input) ? placeName : isCustomerName(input) ? customerName : isProductName(input) ? productName : null;
    // Email inputs do not expose selection ranges. Text + email input mode keeps
    // the same appearance/mobile keyboard and lets filtering preserve the caret.
    const prepareEmail = () => {
        const input = document.getElementById('registrationEmail');
        if (input instanceof HTMLInputElement && input.type === 'email') {
            input.type = 'text';
            input.inputMode = 'email';
        }
    };
    new MutationObserver(prepareEmail).observe(document.documentElement, { childList: true, subtree: true });
    prepareEmail();
    document.addEventListener('paste', event => {
        if (!contextualCleaner(event.target) || !event.clipboardData) return;
        const insertion = new InputEvent('beforeinput', {
            bubbles: true, cancelable: true, inputType: 'insertFromPaste',
            data: event.clipboardData.getData('text/plain')
        });
        if (!event.target.dispatchEvent(insertion)) event.preventDefault();
    }, true);
    document.addEventListener('beforeinput', event => {
        if (isRegistrationEmail(event.target) && !event.isComposing && event.data && event.inputType.startsWith('insert')) {
            const input = event.target, start = input.selectionStart, end = input.selectionEnd;
            const prefix = input.value.slice(0, start), suffix = input.value.slice(end);
            const capacity = Math.max(0, input.maxLength - input.value.length + end - start);
            const addition = clean(event.data, rules.email).slice(0, capacity);
            const proposed = prefix + addition + suffix;
            const value = emailInput(proposed).slice(0, input.maxLength);
            if (addition === event.data && value === proposed) return;
            event.preventDefault();
            if (!addition || !value) return;
            // Reject an impossible insertion without deleting selected valid text.
            if (emailInput(prefix + addition + suffix) === emailInput(prefix + suffix)) return;
            input.value = value;
            const caret = Math.min(emailInput(prefix + addition, true).length, value.length);
            input.setSelectionRange(caret, caret);
            input.dispatchEvent(new Event('input', { bubbles: true }));
            return;
        }
        const sanitize = contextualCleaner(event.target);
        if (sanitize && !event.isComposing && event.data && event.inputType.startsWith('insert')) {
            const input = event.target, start = input.selectionStart, end = input.selectionEnd;
            const prefix = input.value.slice(0, start);
            const inserted = sanitize(prefix + event.data).slice(prefix.length);
            const capacity = Math.max(0, input.maxLength - input.value.length + end - start);
            const addition = inserted.slice(0, capacity);
            const proposed = prefix + addition + input.value.slice(end);
            const value = sanitize(proposed);
            if (addition === event.data && value === proposed) return;
            event.preventDefault();
            if (!addition) return;
            input.value = value;
            input.setSelectionRange(prefix.length + addition.length, prefix.length + addition.length);
            input.dispatchEvent(new Event('input', { bubbles: true }));
            return;
        }
        const input = event.target, rule = ruleFor(input);
        if (!rule || event.isComposing || !event.data || !event.inputType.startsWith('insert')) return;
        const filtered = clean(event.data, rule);
        if (filtered === event.data) return;
        event.preventDefault();
        if (!filtered) return;
        const capacity = input.maxLength < 0 ? Infinity : input.maxLength - input.value.length + input.selectionEnd - input.selectionStart;
        input.setRangeText(filtered.slice(0, capacity), input.selectionStart, input.selectionEnd, 'end');
        input.dispatchEvent(new Event('input', { bubbles: true }));
    }, true);
    document.addEventListener('input', event => {
        const sanitize = contextualCleaner(event.target);
        if (sanitize) {
            if (event.isComposing) { event.stopImmediatePropagation(); return; }
            const input = event.target, raw = input.value, start = input.selectionStart, end = input.selectionEnd;
            const value = sanitize(raw).slice(0, input.maxLength);
            if (value !== raw) {
                input.value = value;
                const caretCleaner = isRegistrationEmail(input) ? value => emailInput(value, true) : sanitize;
                input.setSelectionRange(caretCleaner(raw.slice(0, start)).length, caretCleaner(raw.slice(0, end)).length);
            }
            // Delivery InputText uses change binding; publish only the sanitized value.
            if (isDeliveryPlace(input) || isRegistrationName(input) || isRegistrationEmail(input))
                input.dispatchEvent(new Event('change', { bubbles: true }));
            return;
        }
        const input = event.target, rule = ruleFor(input);
        if (!rule || event.isComposing) return;
        const start = input.selectionStart, end = input.selectionEnd, raw = input.value;
        let value = clean(raw, rule);
        if (input.maxLength >= 0) value = value.slice(0, input.maxLength);
        if (raw !== value) {
            input.value = value;
            if (start !== null) input.setSelectionRange(clean(raw.slice(0, start), rule).length, clean(raw.slice(0, end), rule).length);
        }
        // InputText binds on change; notify it as the user corrects a touched field.
        if (['registrationEmail', 'registrationFirstName', 'registrationLastName', 'shipAddress', 'shipCity', 'shipCountry'].includes(input.id))
            input.dispatchEvent(new Event('change', { bubbles: true }));
    }, true);
    document.addEventListener('compositionend', event => {
        if (ruleFor(event.target) || isCustomerName(event.target)) event.target.dispatchEvent(new Event('input', { bubbles: true }));
    }, true);
})();
