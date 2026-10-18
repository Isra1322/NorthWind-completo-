export function create(bytes) {
    return URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
}
export function release(url) { URL.revokeObjectURL(url); }
let reservedForm = null;
let reservedWindow = null;

function reserveInvoiceTab() {
    if (reservedWindow && !reservedWindow.closed) return;
    reservedWindow = window.open('', '_blank');
    if (reservedWindow) {
        try {
            reservedWindow.document.title = 'Preparando factura';
            reservedWindow.document.body.innerHTML = '<p style="font:16px sans-serif;padding:1rem">Preparando la factura…</p>';
        } catch { }
    }
}

export function attachSubmitReservation(formId) {
    const form = document.getElementById(formId);
    if (!form || form === reservedForm) return;
    detachSubmitReservation();
    reservedForm = form;
    reservedForm.addEventListener('submit', reserveInvoiceTab, true);
}

export function detachSubmitReservation() {
    reservedForm?.removeEventListener('submit', reserveInvoiceTab, true);
    reservedForm = null;
}

export function closeReserved() {
    if (reservedWindow && !reservedWindow.closed) reservedWindow.close();
    reservedWindow = null;
}

export function showReserved(url) {
    if (!reservedWindow || reservedWindow.closed) {
        reservedWindow = null;
        return false;
    }
    const target = reservedWindow;
    reservedWindow = null;
    try {
        target.opener = null;
        target.location.replace(url);
        return true;
    } catch {
        try { target.close(); } catch { }
        return false;
    }
}

export function download(url, filename) {
    try {
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        link.style.display = 'none';
        document.body.appendChild(link);
        link.click();
        link.remove();
        return true;
    } catch {
        return false;
    }
}
export function reserveFromClick() {
    if (reservedWindow && !reservedWindow.closed) return true;
    reservedWindow = window.open('', '_blank');
    if (reservedWindow) reservedWindow.document.body.innerHTML = '<p style="font:16px sans-serif;padding:1rem">Preparando factura…</p>';
    return !!reservedWindow;
}

export function releaseAfter(url) {
    window.setTimeout(() => URL.revokeObjectURL(url), 60000);
}
export function print(id) {
    const frame = document.getElementById(id);
    if (!frame?.contentWindow) throw new Error('PDF no disponible');
    frame.contentWindow.focus();
    frame.contentWindow.print();
}
