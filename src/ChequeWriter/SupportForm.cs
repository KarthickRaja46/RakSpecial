using System.ComponentModel;
using System.Diagnostics;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>Help &amp; support / About: contact by email, WhatsApp or phone, report a problem, program details.</summary>
public class SupportForm : Form
{
    static readonly Color Navy = Color.FromArgb(11, 13, 18);
    static readonly Color Gold = Color.FromArgb(212, 175, 106);

    readonly string _details;

    public SupportForm(DataStore store, BankLayout? book)
    {
        Text = $"Help & support - {AppInfo.Name}";
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AppIcon.Apply(this);
        _details = Support.Details(store, book);

        var body = new TableLayoutPanel
        {
            AutoSize = true, ColumnCount = 1, Padding = new Padding(18, 6, 18, 4), Margin = new Padding(0),
        };

        body.Controls.Add(Heading("Contact support"));
        body.Controls.Add(Text_("Questions, printing problems or ideas: we usually reply within one working day (UAE time)."));

        var email = Row();
        email.Controls.Add(Button_("✉  Email support", (_, _) => Support.Email(this, _details), primary: true));
        email.Controls.Add(Link(AppInfo.SupportEmail, () => Support.Email(this, _details)));
        email.Controls.Add(Link("Open in Gmail", () => Support.Open(Support.GmailLink(_details))));
        email.Controls.Add(Link("Copy address", () => Support.Copy(this, AppInfo.SupportEmail)));
        body.Controls.Add(email);

        foreach (var p in AppInfo.Phones)
        {
            var row = Row();
            row.Controls.Add(new Label { Text = p.Country, AutoSize = true, MinimumSize = new Size(110, 0), Margin = new Padding(3, 9, 3, 3) });
            row.Controls.Add(new Label { Text = p.Display, AutoSize = true, MinimumSize = new Size(160, 0), Margin = new Padding(3, 9, 3, 3), Font = new Font(Font, FontStyle.Bold) });
            row.Controls.Add(Button_("WhatsApp", (_, _) => Support.Open(p.WhatsAppLink(Support.WhatsAppMessage()))));
            row.Controls.Add(Link("Copy number", () => Support.Copy(this, p.Display)));
            body.Controls.Add(row);
        }

        var web = Row();
        web.Controls.Add(Button_("Report a problem on GitHub", (_, _) => Support.Open(AppInfo.NewIssuePage)));
        web.Controls.Add(Button_("Project page", (_, _) => Support.Open(AppInfo.ProjectPage)));
        web.Controls.Add(Button_("Check for updates", (_, _) => { DialogResult = DialogResult.Retry; Close(); }));
        body.Controls.Add(web);

        var tip = Text_("Tip: send a photo of a test print on plain paper. Never send a photo of a real or signed cheque, " +
                        "an account number or a signature.");
        tip.ForeColor = Color.FromArgb(180, 83, 9);
        body.Controls.Add(tip);

        body.Controls.Add(Heading("This copy of RAK Special"));
        var details = new TextBox
        {
            Text = _details.Replace("\n", Environment.NewLine), ReadOnly = true, Multiline = true, Width = 560, Height = 96,
            BackColor = Color.FromArgb(246, 247, 249), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9f),
            Margin = new Padding(3, 3, 3, 6), ScrollBars = ScrollBars.Vertical,
        };
        body.Controls.Add(details);
        var dataRow = Row();
        dataRow.Controls.Add(Link("Copy these details", () => Support.Copy(this, _details)));
        dataRow.Controls.Add(Link("Open data folder", () =>
        {
            var folder = Path.GetDirectoryName(store.FilePath)!;
            Directory.CreateDirectory(folder);
            Support.Open(folder);
        }));
        body.Controls.Add(dataRow);

        var footer = new Label
        {
            AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Color.DimGray, Margin = new Padding(3, 10, 3, 3),
            Font = new Font("Segoe UI", 8.5f),
            Text = $"© 2026 {AppInfo.Author}. {AppInfo.Name} is an independent project and is not affiliated with or endorsed by " +
                   "The National Bank of Ras Al Khaimah (RAKBANK).",
        };
        body.Controls.Add(footer);

        var close = new Button
        {
            Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(10, 2, 10, 2),
            Anchor = AnchorStyles.Right, Margin = new Padding(3, 4, 18, 14),
        };
        CancelButton = close;

        // header, body and Close button stacked in one self-sizing column
        var page = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Location = Point.Empty, Margin = new Padding(0), Padding = new Padding(0) };
        page.Controls.Add(Header());
        page.Controls.Add(body);
        page.Controls.Add(close);
        Controls.Add(page);
    }

    Control Header()
    {
        var p = new Panel { Dock = DockStyle.Fill, Height = 92, Margin = new Padding(0), BackColor = Navy };
        using var icon = new Icon(AppIcon.Get(), 64, 64);
        p.Controls.Add(new PictureBox { Image = icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(18, 14, 64, 64) });
        p.Controls.Add(new Label { Text = AppInfo.Name, ForeColor = Gold, Font = new Font("Segoe UI", 18f, FontStyle.Bold), AutoSize = true, Location = new Point(94, 14) });
        p.Controls.Add(new Label
        {
            Text = $"Version {Updates.Current}  ·  RAK Bank cheque printing for the UAE", ForeColor = Color.White,
            AutoSize = true, Location = new Point(97, 54),
        });
        return p;
    }

    static Label Heading(string t) => new()
    {
        Text = t, AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59),
        Margin = new Padding(3, 10, 3, 4),
    };

    static Label Text_(string t) => new() { Text = t, AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(3, 0, 3, 6) };

    static FlowLayoutPanel Row() => new() { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };

    static Button Button_(string t, EventHandler click, bool primary = false)
    {
        var b = new Button { Text = t, AutoSize = true, Padding = new Padding(8, 2, 8, 2), Margin = new Padding(3, 3, 10, 3) };
        if (primary) { b.BackColor = Color.FromArgb(0, 102, 204); b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat; }
        b.Click += click;
        return b;
    }

    static LinkLabel Link(string t, Action a)
    {
        var l = new LinkLabel { Text = t, AutoSize = true, Margin = new Padding(3, 8, 12, 3) };
        l.LinkClicked += (_, _) => a();
        return l;
    }
}

/// <summary>Opening email, WhatsApp and web links with the details support needs.</summary>
public static class Support
{
    public static string Details(DataStore store, BankLayout? book) =>
        $"{AppInfo.Name} {Updates.Current}\n" +
        $"Windows: {Environment.OSVersion.Version} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})\n" +
        (book == null ? "" : $"Cheque book: {book.Name} {book.Code}\n" +
                             $"Printer: {(string.IsNullOrEmpty(book.PrinterName) ? "(not chosen yet)" : book.PrinterName)}\n" +
                             $"Printer offset: X {book.OffsetXmm:0.0} mm, Y {book.OffsetYmm:0.0} mm\n") +
        $"Cheques in register: {store.Data.Cheques.Count}\n" +
        $"Data file: {store.FilePath}";

    static string Subject => $"{AppInfo.Name} {Updates.Current} - support";

    static string Body(string details) =>
        "Hello,\n\nPlease describe the problem or question here:\n\n\n\n" +
        "(If it is about alignment, attach a photo of a test print on plain paper. Please do not send photos of real cheques.)\n\n" +
        "---- details (added by RAK Special) ----\n" + details + "\n";

    public static string MailtoLink(string details) =>
        $"mailto:{AppInfo.SupportEmail}?subject={Uri.EscapeDataString(Subject)}&body={Uri.EscapeDataString(Body(details).Replace("\n", "\r\n"))}";

    public static string GmailLink(string details) =>
        $"https://mail.google.com/mail/?view=cm&fs=1&to={Uri.EscapeDataString(AppInfo.SupportEmail)}" +
        $"&su={Uri.EscapeDataString(Subject)}&body={Uri.EscapeDataString(Body(details))}";

    public static string WhatsAppMessage() => $"Hello, I need help with {AppInfo.Name} {Updates.Current}.";

    /// <summary>Opens the email program; if none is set up, offers Gmail in the browser instead.</summary>
    public static void Email(IWin32Window owner, string details)
    {
        try
        {
            Process.Start(new ProcessStartInfo(MailtoLink(details)) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            if (MessageBox.Show(owner, $"No email program is set up on this PC.\n\nOpen Gmail in the web browser instead?\n(Or write to {AppInfo.SupportEmail})",
                    "Email support", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Open(GmailLink(details));
        }
    }

    public static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("Could not open:\n" + target + "\n\n" + ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    public static void Copy(IWin32Window owner, string text)
    {
        try
        {
            Clipboard.SetText(text);
            MessageBox.Show(owner, "Copied:\n" + text, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(owner, "Could not copy:\n" + ex.Message, AppInfo.Name); }
    }
}
