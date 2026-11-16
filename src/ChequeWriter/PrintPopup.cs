using System.Drawing.Printing;
using System.Runtime.InteropServices;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>
/// RAK Special's own print popup (printer, Properties, copies), in the style of Chrysanth Cheque Writer.
/// The Windows 11 print dialog is not used: it applies its own page settings, and a cheque fed into the
/// printer then comes out blank. With this popup the page stays exactly as the cheque book needs it.
/// </summary>
public class PrintPopup : Form
{
    readonly PrintDocument _doc;
    readonly ComboBox _printer = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 318 };
    readonly Label _status = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    readonly NumericUpDown _copies = new() { Minimum = 1, Maximum = 20, Value = 1, Width = 70 };
    bool _propertiesChanged;

    PrintPopup(PrintDocument doc, string selected, BankLayout bank)
    {
        _doc = doc;
        Text = "Print";
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96F, 96F);   // laid out at 100 %; Windows scaling enlarges it evenly
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, 330);
        AppIcon.Apply(this);

        // right: how to put the cheque into the printer, drawn from the real print position
        var insertBox = new GroupBox { Text = "Insert the cheque", Bounds = new Rectangle(520, 10, 228, 272) };
        insertBox.Controls.Add(new InsertDiagram(bank) { Dock = DockStyle.Fill });
        var how = new Label
        {
            Bounds = new Rectangle(14, 194, 494, 24), ForeColor = SystemColors.GrayText,
            Text = bank.Feed == ChequeFeed.PortraitCentred
                ? "Insert the cheque as shown: standing up, centred, arrow end first."
                : "Insert the cheque as shown, arrow end first.",
        };

        var printers = Printers.Installed();
        foreach (var p in printers) _printer.Items.Add(p);
        if (printers.Count > 0) _printer.SelectedItem = printers.Contains(selected) ? selected : printers[0];
        _printer.SelectedIndexChanged += (_, _) => { _propertiesChanged = false; ShowStatus(); };

        var printerBox = new GroupBox { Text = "Printer", Bounds = new Rectangle(12, 10, 496, 96) };
        printerBox.Controls.Add(new Label { Text = "Name:", AutoSize = true, Location = new Point(14, 32) });
        _printer.Location = new Point(70, 28);
        printerBox.Controls.Add(_printer);
        var props = new Button { Text = "Properties...", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6, 2, 6, 2) };
        props.Location = new Point(484 - props.PreferredSize.Width, 26);
        _printer.Width = props.Left - 8 - _printer.Left;
        props.Click += (_, _) => ShowProperties();
        printerBox.Controls.Add(props);
        printerBox.Controls.Add(new Label { Text = "Status:", AutoSize = true, Location = new Point(14, 64) });
        _status.Location = new Point(70, 64);
        printerBox.Controls.Add(_status);

        var copiesBox = new GroupBox { Text = "Copies", Bounds = new Rectangle(12, 114, 496, 64) };
        copiesBox.Controls.Add(new Label { Text = "Number of copies:", AutoSize = true, Location = new Point(14, 29) });
        _copies.Location = new Point(150, 26);
        copiesBox.Controls.Add(_copies);

        var ok = new Button { Text = "OK", Bounds = new Rectangle(576, 290, 82, 30), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Bounds = new Rectangle(666, 290, 82, 30), DialogResult = DialogResult.Cancel };
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { printerBox, copiesBox, how, insertBox, ok, cancel });
        ShowStatus();
    }

    string Selected => _printer.SelectedItem as string ?? "";

    void ShowStatus() => _status.Text =
        Selected.Length == 0 ? "No printer installed"
        : Printers.IsFileOnly(Selected) ? "Saves a file - does not print on paper"
        : "Ready";

    /// <summary>
    /// Shows the popup. On OK the document is set to the chosen printer and copies, and the page is set
    /// again to the cheque book's paper (A4 landscape), so nothing the printer settings do can move it.
    /// </summary>
    public static bool Choose(IWin32Window owner, PrintDocument doc, BankLayout bank)
    {
        using var f = new PrintPopup(doc, Printers.For(bank), bank);
        if (f.ShowDialog(owner) != DialogResult.OK || f.Selected.Length == 0) return false;
        if (!f._propertiesChanged) doc.PrinterSettings.PrinterName = f.Selected;
        doc.PrinterSettings.Copies = (short)f._copies.Value;
        bank.PrinterName = f.Selected;
        ChequePrinter.ApplyPaper(doc, bank);
        return true;
    }

    // ---- the printer driver's own Properties window (paper tray, quality...) ----

    [DllImport("winspool.drv", CharSet = CharSet.Unicode)]
    static extern int DocumentProperties(IntPtr hwnd, IntPtr hPrinter, string pDeviceName, IntPtr pDevModeOutput, IntPtr pDevModeInput, int fMode);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr hMem);

    void ShowProperties()
    {
        if (Selected.Length == 0) return;
        try
        {
            _doc.PrinterSettings.PrinterName = Selected;
            ChequePrinter.ApplyPaper(_doc, BankForPaper);
            var hDevMode = _doc.PrinterSettings.GetHdevmode(_doc.DefaultPageSettings);
            var pIn = GlobalLock(hDevMode);
            var size = DocumentProperties(Handle, IntPtr.Zero, Selected, IntPtr.Zero, IntPtr.Zero, 0);
            var pOut = Marshal.AllocHGlobal(size);
            try
            {
                const int DM_IN_BUFFER = 8, DM_IN_PROMPT = 4, DM_OUT_BUFFER = 2;
                var result = DocumentProperties(Handle, IntPtr.Zero, Selected, pOut, pIn, DM_IN_BUFFER | DM_IN_PROMPT | DM_OUT_BUFFER);
                if (result == 1)   // OK
                {
                    _doc.PrinterSettings.SetHdevmode(pOut);
                    _doc.DefaultPageSettings.SetHdevmode(pOut);
                    _propertiesChanged = true;
                }
            }
            finally
            {
                GlobalUnlock(hDevMode);
                GlobalFree(hDevMode);
                Marshal.FreeHGlobal(pOut);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not open the printer properties:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Paper and orientation of the document as it is now (A4 landscape for the RAK books).</summary>
    BankLayout BankForPaper
    {
        get
        {
            var ps = _doc.DefaultPageSettings;
            double w = ps.PaperSize.Width * 0.254, h = ps.PaperSize.Height * 0.254;
            return ps.Landscape ? new BankLayout { PaperWidthMm = Math.Max(w, h), PaperHeightMm = Math.Min(w, h), Feed = ChequeFeed.Landscape }
                                : new BankLayout { PaperWidthMm = Math.Min(w, h), PaperHeightMm = Math.Max(w, h), Feed = ChequeFeed.PortraitCentred };
        }
    }
}

/// <summary>
/// Small picture of the paper with the cheque in the exact place and direction RAK Special prints it,
/// and an arrow for the edge that goes into the printer first.
/// </summary>
public class InsertDiagram : Control
{
    readonly BankLayout _bank;

    public InsertDiagram(BankLayout bank)
    {
        _bank = bank;
        DoubleBuffered = true;
        ResizeRedraw = true;
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.Clear(BackColor);

        var portrait = _bank.Feed == ChequeFeed.PortraitCentred;
        float shortMm = (float)Math.Min(_bank.PaperWidthMm, _bank.PaperHeightMm);
        float longMm = (float)Math.Max(_bank.PaperWidthMm, _bank.PaperHeightMm);
        float pw = portrait ? shortMm : longMm, ph = portrait ? longMm : shortMm;

        // "into the printer" arrow on top
        const int top = 38;
        var scale = Math.Min((Width - 16f) / pw, (Height - top - 8f) / ph);
        float ox = (Width - pw * scale) / 2, oy = top;
        var arrowColor = Color.FromArgb(0, 102, 204);
        using (var b = new SolidBrush(arrowColor))
        {
            var cx = Width / 2f;
            g.FillPolygon(b, new[] { new PointF(cx, 2), new PointF(cx - 9, 13), new PointF(cx + 9, 13) });
            g.FillRectangle(b, cx - 3, 12, 6, 7);
            TextRenderer.DrawText(g, "into the printer", Font, new Rectangle(0, 19, Width, 16), arrowColor,
                TextFormatFlags.HorizontalCenter);
        }

        // the paper
        using (var paper = new SolidBrush(Color.White)) g.FillRectangle(paper, ox, oy, pw * scale, ph * scale);
        using (var pen = new Pen(Color.Silver) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
            g.DrawRectangle(pen, ox, oy, pw * scale, ph * scale);
        TextRenderer.DrawText(g, "A4", new Font("Segoe UI", 8f), new Point((int)(ox + 4), (int)(oy + ph * scale - 16)), Color.Silver);

        // the cheque, placed and turned exactly like the print
        var state = g.Save();
        g.TranslateTransform(ox, oy);
        g.ScaleTransform(scale, scale);
        ChequePrinter.PlaceCheque(g, _bank);
        var rect = new RectangleF(0, 0, (float)_bank.ChequeWidthMm, (float)_bank.ChequeHeightMm);
        if (ChequePictures.For(_bank) is { } pic) g.DrawImage(pic, rect);
        else using (var fill = new SolidBrush(Color.FromArgb(235, 242, 250))) g.FillRectangle(fill, rect);
        using (var pen = new Pen(Color.FromArgb(212, 175, 106), 1.2f / scale)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        g.Restore(state);
    }
}
