using System.Drawing.Printing;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>Which printer a cheque goes to, and a guard against "printers" that only make files.</summary>
public static class Printers
{
    // Windows printers that save a file instead of printing on paper
    static readonly string[] FileOnly = { "pdf", "xps", "onenote", "fax", "send to", "document writer", "print to file", "image writer", "snagit" };

    public static List<string> Installed()
    {
        try { return PrinterSettings.InstalledPrinters.Cast<string>().ToList(); }
        catch { return new(); }
    }

    public static bool IsFileOnly(string name) =>
        FileOnly.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));

    static string WindowsDefault()
    {
        try { return new PrinterSettings().PrinterName ?? ""; }
        catch { return ""; }
    }

    /// <summary>
    /// The printer for this cheque book: the one chosen for it before; otherwise the Windows default
    /// when it is a real printer; otherwise the first real printer.
    /// </summary>
    public static string For(BankLayout bank)
    {
        var all = Installed();
        if (bank.PrinterName.Length > 0 && all.Contains(bank.PrinterName)) return bank.PrinterName;
        var def = WindowsDefault();
        if (def.Length > 0 && !IsFileOnly(def) && all.Contains(def)) return def;
        return all.FirstOrDefault(p => !IsFileOnly(p)) ?? def;
    }

    /// <summary>
    /// Checks the printer before printing. Real cheques are never sent to a file-only printer;
    /// for a test print the user may go ahead anyway.
    /// </summary>
    public static bool Ready(IWin32Window owner, string printer, bool realCheque)
    {
        if (printer.Length == 0)
        {
            MessageBox.Show(owner, "No printer is installed on this PC.\nAdd your printer in Windows Settings > Bluetooth & devices > Printers & scanners.",
                "Printer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (!IsFileOnly(printer)) return true;
        var msg = $"\"{printer}\" does not print on paper - it saves a file instead.\n\n" +
                  "Choose your real printer in the Printer box of the main window.";
        if (realCheque)
        {
            MessageBox.Show(owner, msg, "Choose your printer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return MessageBox.Show(owner, msg + "\n\nSave the test print as a file anyway?", "Choose your printer",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    }
}

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
        var printer = Printers.For(bank);
        if (printer.Length > 0) doc.PrinterSettings.PrinterName = printer;
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
            // the cheque's corner on the page (10 mm in from the paper corner, like Chrysanth Cheque Writer)
            g.TranslateTransform((float)bank.ChequeLeftMm, (float)bank.ChequeTopMm);
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
