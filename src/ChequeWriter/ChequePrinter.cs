using System.Drawing.Printing;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>Builds a PrintDocument that prints cheques at exact millimetre positions.</summary>
public static class ChequePrinter
{
    /// <summary>
    /// One cheque. The cheque picture is shown in print preview and, when the setting is on,
    /// printed on plain-paper test prints. It is never printed on a real cheque.
    /// </summary>
    public static PrintDocument Create(BankLayout bank, ChequeData data, AppSettings settings, bool testPrint) =>
        CreateSeries(bank, new[] { data }, settings, testPrint,
            testPrint ? "RAK Special test print" : $"RAK Special cheque {data.ChequeNo}");

    /// <summary>Several cheques, one per page (used to preview a PDC series).</summary>
    public static PrintDocument CreateSeries(BankLayout bank, IReadOnlyList<ChequeData> cheques, AppSettings settings,
        bool testPrint, string documentName)
    {
        var doc = new PrintDocument { DocumentName = documentName };
        if (!string.IsNullOrWhiteSpace(bank.PrinterName) &&
            PrinterSettings.InstalledPrinters.Cast<string>().Contains(bank.PrinterName))
        {
            doc.PrinterSettings.PrinterName = bank.PrinterName;
        }
        ApplyPaper(doc, bank);

        var page = 0;
        doc.BeginPrint += (_, _) => page = 0;
        doc.PrintPage += (_, e) =>
        {
            var data = cheques[page];
            var text = ChequeText.Build(data, bank, settings);
            var g = e.Graphics!;
            g.PageUnit = GraphicsUnit.Millimeter;
            var preview = doc.PrintController?.IsPreview == true;
            // .NET draws from the printer's printable-area corner; move back to the real paper corner
            // so positions are measured from the edge of the paper. Not needed in print preview.
            if (!preview)
            {
                g.TranslateTransform(-(float)(e.PageSettings.HardMarginX * 0.254), -(float)(e.PageSettings.HardMarginY * 0.254));
            }
            var showPicture = preview || (testPrint && settings.TestPrintPicture);
            ChequeRenderer.Draw(g, bank, data, text, new RenderOptions
            {
                ApplyPrinterOffset = true,
                ShowChequeOutline = testPrint,
                ShowFieldBoxes = testPrint && !showPicture,
                Background = showPicture ? ChequePictures.For(bank) : null,
            });
            page++;
            e.HasMorePages = page < cheques.Count;
        };
        return doc;
    }

    /// <summary>Uses a standard paper size when the printer has one that matches, otherwise a custom size.</summary>
    static void ApplyPaper(PrintDocument doc, BankLayout bank)
    {
        var w = bank.PaperWidthMm; var h = bank.PaperHeightMm;
        var landscape = w > h;
        var shortMm = Math.Min(w, h); var longMm = Math.Max(w, h);
        var ps = doc.DefaultPageSettings;
        ps.Margins = new Margins(0, 0, 0, 0);
        ps.Landscape = landscape;

        PaperSize? match = null;
        try
        {
            match = doc.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(p =>
                Math.Abs(p.Width * 0.254 - shortMm) < 1.5 && Math.Abs(p.Height * 0.254 - longMm) < 1.5);
        }
        catch (InvalidPrinterException) { /* no printer installed: preview still works */ }

        ps.PaperSize = match ?? new PaperSize("Cheque", (int)Math.Round(shortMm / 0.254), (int)Math.Round(longMm / 0.254));
    }
}
