export function create(bytes) {
    return URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
}
export function release(url) { URL.revokeObjectURL(url); }
export function print(id) {
    const frame = document.getElementById(id);
    if (!frame?.contentWindow) throw new Error('PDF no disponible');
    frame.contentWindow.focus();
    frame.contentWindow.print();
}
