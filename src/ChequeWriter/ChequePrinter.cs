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
            PlaceCheque(g, bank, e.PageSettings);
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

    /// <summary>
    /// Moves (and for a portrait feed turns) the drawing so that cheque coordinates land where the cheque
    /// physically is in the printer. Units are millimetres from the paper's top-left corner.
    /// </summary>
    public static void PlaceCheque(Graphics g, BankLayout bank, PageSettings? page = null)
    {
        if (bank.Feed == ChequeFeed.Landscape)
        {
            // A4 landscape, cheque 10 mm in from the corner (like Chrysanth's landscape template)
            g.TranslateTransform((float)bank.ChequeLeftMm, (float)bank.ChequeTopMm);
            return;
        }
        // Portrait: the cheque stands up, narrow side first, centred across the tray
        var pageWidth = Math.Min(bank.PaperWidthMm, bank.PaperHeightMm);
        var across = (float)((pageWidth - bank.ChequeHeightMm) / 2 + bank.PortraitAcrossMm);
        var lead = (float)bank.PortraitLeadMm;
        if (bank.PortraitTurn == FeedTurn.Clockwise)
        {
            // cheque top edge faces right; its left (perforated) edge enters the printer first
            g.TranslateTransform(across + (float)bank.ChequeHeightMm, lead);
            g.RotateTransform(90);
        }
        else
        {
            // cheque top edge faces left; its right edge enters the printer first
            g.TranslateTransform(across, lead + (float)bank.ChequeWidthMm);
            g.RotateTransform(-90);
        }
    }

    /// <summary>
    /// RAK Special's own print popup (<see cref="PrintPopup"/>): the user picks the printer, the page is set to
    /// the cheque book's paper and orientation, and the document is printed. Returns false when cancelled or failed.
    /// </summary>
    public static bool PrintWithDialog(IWin32Window owner, PrintDocument doc, BankLayout bank)
    {
        if (!PrintPopup.Choose(owner, doc, bank)) return false;
        try
        {
            doc.Print();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "Printing failed:\n" + ex.Message, "Print", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>Uses a standard paper size when the printer has one that matches, otherwise a custom size.</summary>
    public static void ApplyPaper(PrintDocument doc, BankLayout bank)
    {
        var w = bank.PaperWidthMm; var h = bank.PaperHeightMm;
        // portrait feed: the same paper (A4) used upright
        var landscape = bank.Feed == ChequeFeed.Landscape && w > h;
        var shortMm = Math.Min(w, h); var longMm = Math.Max(w, h);
        var ps = doc.DefaultPageSettings;
        ps.PrinterSettings = doc.PrinterSettings;
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
