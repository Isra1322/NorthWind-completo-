using System.Globalization;
using System.Text;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Entities.Dtos;

namespace NorthWind.Sales.Backend.Presenters;

internal class InvoicePdfPresenter : IInvoicePdfPresenter
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private static string Money(decimal value) => value.ToString("N2", Spanish);
    private static string N(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)").Replace('\r', ' ').Replace('\n', ' ');
    private static decimal Width(string text, decimal size) => text.Sum(c => char.IsDigit(c) ? .556m : "ilI.,:;'!| ".Contains(c) ? .278m : "MW@%".Contains(c) ? 1m : .65m) * size;
    private static IEnumerable<string> Wrap(string text, decimal width, decimal size)
    {
        text = text.Replace('\r', ' ').Replace('\n', ' ');
        while (Width(text, size) > width)
        {
            int length = 1;
            while (length < text.Length && Width(text[..(length + 1)], size) <= width) length++;
            int space = text.LastIndexOf(' ', length - 1, length);
            int split = space > 0 ? space : length;
            yield return text[..split];
            text = text[split..].TrimStart();
        }
        yield return text;
    }
    public byte[] Render(InvoiceDto invoice)
    {
        var pages = new List<StringBuilder>();
        StringBuilder canvas = null!;
        decimal y = 0;
        void Text(string text, decimal x, decimal baseline, decimal size = 10, bool bold = false, bool right = false)
        {
            if (right) x -= Width(text, size);
            canvas.Append($"BT /{(bold ? "F2" : "F1")} {N(size)} Tf 0.12 0.17 0.23 rg 1 0 0 1 {N(x)} {N(baseline)} Tm ({Escape(text)}) Tj ET\n");
        }
        void Rule(decimal baseline) => canvas.Append($"0.82 0.85 0.88 RG 0.5 w 40 {N(baseline)} m 555 {N(baseline)} l S\n");
        void NewPage(bool continuation)
        {
            canvas = new StringBuilder(); pages.Add(canvas);
            Text("NorthWind Sales", 40, 794, 20, true);
            Text("FACTURA", 555, 794, 20, true, true);
            Text($"N.º {invoice.Id}", 555, 766, 12, true, true);
            Text($"Emisión: {invoice.OrderDate:dd/MM/yyyy HH:mm}", 40, 766);
            Text($"Versión {invoice.VersionNumber}", 555, 748, 9, true, true);
            if (invoice.ModifiedAt is { } modifiedAt) Text($"Modificada: {modifiedAt:dd/MM/yyyy HH:mm}", 40, 748, 9);
            Rule(738); y = 714;
            if (!continuation)
            {
                Text("DATOS DEL CLIENTE", 40, y, 11, true); y -= 23;
                foreach (string item in new[] { $"Cliente: {invoice.CustomerDisplayName}", $"Identificación: {invoice.CustomerIdentification ?? "—"}", $"Dirección del cliente: {invoice.CustomerAddress ?? "—"}", $"Correo: {invoice.CustomerEmail ?? "—"}", $"Teléfono: {invoice.CustomerPhone ?? "—"}", $"Dirección: {invoice.ShipAddress}", $"Ciudad: {invoice.ShipCity}", $"País: {invoice.ShipCountry}", $"Código postal: {invoice.ShipPostalCode ?? "—"}" })
                    foreach (string line in Wrap(item, 510, 10)) { Text(line, 40, y); y -= 16; }
                y -= 20;
            }
            else
            {
                foreach (string line in Wrap($"Cliente: {invoice.CustomerDisplayName} · Continuación", 510, 10))
                { Text(line, 40, y); y -= 16; }
                y -= 12;
            }
            canvas.Append($"0.92 0.95 0.97 rg 40 {N(y - 9)} 515 27 re f\n");
            Text("Código", 105, y, 9, true, true); Text("Producto", 115, y, 9, true);
            Text("Precio unitario", 397, y, 9, true, true); Text("Cantidad", 455, y, 9, true, true); Text("Importe", 549, y, 9, true, true);
            y -= 32;
        }
        NewPage(false);
        foreach (var line in invoice.Lines)
        {
            var name = Wrap(line.ProductName, 170, 10).ToArray();
            decimal height = Math.Max(30, name.Length * 14 + 12);
            if (y - height < 80) NewPage(true);
            Text(line.ProductId.ToString(CultureInfo.InvariantCulture), 105, y, right: true);
            for (int i = 0; i < name.Length; i++) Text(name[i], 115, y - i * 14);
            Text(Money(line.UnitPrice), 397, y, right: true);
            Text(line.Quantity.ToString(CultureInfo.InvariantCulture), 455, y, right: true);
            Text(Money(line.Amount), 549, y, right: true);
            y -= height; Rule(y + 12);
        }
        if (y < 210) NewPage(true);
        y -= 24;
        void Summary(string label, decimal value, bool highlight = false)
        {
            Text(label, 305, y, highlight ? 12 : 10, highlight);
            Text((highlight ? "$" : "") + Money(value), 549, y, highlight ? 12 : 10, highlight, true); y -= 24;
        }
        Summary("Subtotal", invoice.Subtotal);
        if (invoice.TaxRecorded) Summary(invoice.TaxLabel, invoice.TaxAmount);
        else
        {
            Text("IVA", 305, y);
            Text("—", 549, y, right: true); y -= 24;
        }
        Rule(y + 8); y -= 10; Summary("TOTAL A PAGAR", invoice.Total, true); y -= 8;
        string? note = invoice.HistoricalNote;
        if (note is not null) foreach (string line in Wrap(note, 510, 9)) { Text(line, 40, y, 9); y -= 14; }
        for (int i = 0; i < pages.Count; i++)
        {
            canvas = pages[i]; Rule(54); Text("NorthWind Sales", 40, 36, 9);
            Text($"Página {i + 1} de {pages.Count}", 555, 36, 9, right: true);
        }
        return Assemble(pages);
    }
    private static byte[] Assemble(IReadOnlyList<StringBuilder> pages)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(1252);
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", pages.Select((_, i) => $"{5 + i * 2} 0 R"))}] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
        };
        foreach (var page in pages)
        {
            int contentId = objects.Count + 2;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentId} 0 R >>");
            objects.Add($"<< /Length {encoding.GetByteCount(page.ToString())} >>\nstream\n{page}endstream");
        }
        using var stream = new MemoryStream();
        void Write(string value) => stream.Write(encoding.GetBytes(value));
        Write("%PDF-1.4\n%âãÏÓ\n");
        var offsets = new List<long> { 0 };
        for (int i = 0; i < objects.Count; i++) { offsets.Add(stream.Position); Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        long xref = stream.Position;
        Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) Write($"{offset:0000000000} 00000 n \n");
        Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }
}
