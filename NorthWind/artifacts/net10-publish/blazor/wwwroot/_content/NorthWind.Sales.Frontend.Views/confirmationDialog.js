const states = new WeakMap();

export function open(dialog, reference) {
    const opener = document.activeElement;
    const cancel = event => {
        event.preventDefault();
        reference.invokeMethodAsync('CancelFromKeyboard');
    };
    const keydown = event => {
        if (event.key !== 'Tab') return;
        const buttons = [...dialog.querySelectorAll('button:not(:disabled)')];
        const first = buttons[0], last = buttons.at(-1);
        if (!first) return;
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    };
    states.set(dialog, { opener, cancel, keydown });
    dialog.addEventListener('cancel', cancel);
    dialog.addEventListener('keydown', keydown);
    dialog.showModal();
}

export function close(dialog, confirmed, confirmedFocusId = null) {
    const state = states.get(dialog);
    dialog.close();
    if (confirmed) document.getElementById(confirmedFocusId)?.focus();
    else if (state?.opener?.isConnected) state.opener.focus();
}

export function dispose(dialog) {
    const state = states.get(dialog);
    if (state) dialog.removeEventListener('cancel', state.cancel);
    if (state) dialog.removeEventListener('keydown', state.keydown);
    if (dialog.open) dialog.close();
    states.delete(dialog);
}
