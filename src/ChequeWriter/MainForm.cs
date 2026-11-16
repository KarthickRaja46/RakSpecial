using System.Diagnostics;
using System.Drawing.Printing;
using System.Globalization;
using ChequeWriter.Core;

namespace ChequeWriter;

public class MainForm : Form
{
    readonly DataStore _store;
    BankLayout Bank => _store.Data.LastBank!;
    bool _fillingBooks;

    readonly SegmentedControl _books = new();
    readonly Label _bookInfo = new() { AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Ui(9f) };
    readonly TextBox _chequeNo = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Ui(11f) };
    readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Dock = DockStyle.Fill, Font = Theme.Ui(11f) };
    readonly ComboBox _payee = new() { Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Font = Theme.Ui(11f), AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    readonly TextBox _amount = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Right, Font = Theme.Ui(17f, FontStyle.Bold), PlaceholderText = "0.00" };
    readonly Card _wordsBox = new() { Dock = DockStyle.Fill, Fill = Theme.GoldSoft, BorderColor = Color.FromArgb(236, 222, 192), Radius = 10, Padding = new Padding(12, 8, 12, 8) };
    readonly Label _words = new() { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Theme.Text, Font = Theme.Ui(10f), BackColor = Theme.GoldSoft, AutoEllipsis = true };
    readonly ToggleSwitch _acPayee = new() { Text = "A/C Payee only (crossing)", Checked = true, Dock = DockStyle.Top };
    readonly ToggleSwitch _bearer = new() { Text = "Cross out \"or Bearer\"", Checked = true, Dock = DockStyle.Top };
    readonly ChequePreview _preview = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(246, 247, 249) };
    readonly ToolTip _tips = new();
    readonly StatTile _statMonth = new("This month");
    readonly StatTile _statUncleared = new("Awaiting clearing");
    readonly StatTile _statNext = new("Next post-dated cheque");
    readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted };

    public MainForm(DataStore store)
    {
        _store = store;
        Text = AppInfo.Name;
        AppIcon.Apply(this);
        Theme.TitleBar(this);
        Font = new Font("Segoe UI", 10f);
        MinimumSize = new Size(S(1060), S(720));
        Size = new Size(S(1280), S(820));
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        BuildLayout();
        BuildMenu();
        LoadBank();

        _date.ValueChanged += (_, _) => UpdatePreview();
        _payee.TextChanged += (_, _) => UpdatePreview();
        _amount.TextChanged += (_, _) => UpdatePreview();
        _acPayee.CheckedChanged += (_, _) => UpdatePreview();
        _bearer.CheckedChanged += (_, _) => UpdatePreview();
        KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.P) { e.SuppressKeyPress = true; PrintCheque(); } };
        Shown += async (_, _) => await CheckForUpdates(manual: false);
    }

    // ---------------- update check ----------------

    /// <summary>
    /// Automatic (every start): silent on errors; a newer version opens the "Update available" window,
    /// unless the user skipped that version. Manual (Help menu): always asks GitHub and reports the result.
    /// </summary>
    async Task CheckForUpdates(bool manual)
    {
        var s = _store.Data.Settings;
        if (!Updates.Configured)
        {
            if (manual) MessageBox.Show(this, "The update check is not set up in this copy of RAK Special.", "Updates",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!manual && !s.CheckForUpdates) return;

        ReleaseInfo? latest;
        try { latest = await Updates.GetLatestAsync(); }
        catch (Exception ex)
        {
            if (manual) MessageBox.Show(this, "Could not reach GitHub to check for updates:\n" + ex.Message, "Updates",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (IsDisposed) return;
        s.LastUpdateCheck = DateTime.Now;
        Save();

        var newer = latest != null && UpdateCheck.IsNewer(latest.Version, Updates.Current);
        if (!newer)
        {
            if (manual) MessageBox.Show(this, $"You have the latest version ({Updates.Current}).", "Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!manual && latest!.Version.ToString() == s.SkippedVersion) return;
        ShowUpdateDialog(latest!);
    }

    void ShowUpdateDialog(ReleaseInfo r)
    {
        foreach (var old in Controls.OfType<UpdateBar>().ToList()) old.Dispose();
        UpdateChoice choice;
        string setup;
        using (var d = new UpdateDialog(r))
        {
            d.ShowDialog(this);
            choice = d.Choice;
            setup = d.SetupFile;
        }
        switch (choice)
        {
            case UpdateChoice.Install:
                Save();
                Updates.InstallAndExit(setup);
                break;
            case UpdateChoice.Skip:
                _store.Data.Settings.SkippedVersion = r.Version.ToString();
                Save();
                break;
            default:
                ShowUpdateBar(r);   // a reminder for this session
                break;
        }
    }

    void ShowUpdateBar(ReleaseInfo r)
    {
        foreach (var old in Controls.OfType<UpdateBar>().ToList()) old.Dispose();
        var bar = new UpdateBar(r, () => ShowUpdateDialog(r), () => { _store.Data.Settings.SkippedVersion = r.Version.ToString(); Save(); });
        Controls.Add(bar);
        bar.SendToBack();
        _header.SendToBack();          // order from the top: menu, header, update bar
        MainMenuStrip?.SendToBack();
    }

    // ---------------- UI ----------------

    void BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("&Backup data...", null, (_, _) => Backup());
        file.DropDownItems.Add("&Restore data from backup...", null, (_, _) => Restore());
        file.DropDownItems.Add("Open &data folder", null, (_, _) => OpenDataFolder());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("E&xit", null, (_, _) => Close());

        var tools = new ToolStripMenuItem("&Tools");
        tools.DropDownItems.Add("P&DC series (post-dated cheques)...", null, (_, _) => ShowPdc());
        tools.DropDownItems.Add(new ToolStripSeparator());
        tools.DropDownItems.Add("Cheque &register", null, (_, _) => ShowRegister());
        tools.DropDownItems.Add(new ToolStripMenuItem("&Analytics (totals by month and payee)", null, (_, _) => ShowAnalytics(), Keys.Control | Keys.T));
        tools.DropDownItems.Add("&Payees", null, (_, _) => ShowPayees());
        tools.DropDownItems.Add(new ToolStripSeparator());
        tools.DropDownItems.Add("&Layout editor (field positions)", null, (_, _) => ShowLayoutEditor());
        tools.DropDownItems.Add("&Settings (wording && formats)", null, (_, _) => ShowSettings());

        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add(new ToolStripMenuItem("Help && &support...", null, async (_, _) => await ShowSupport(), Keys.F1));
        help.DropDownItems.Add("&How to align with your printer", null, (_, _) => ShowAlignHelp());
        help.DropDownItems.Add(new ToolStripSeparator());
        help.DropDownItems.Add("&Email support", null, (_, _) => Support.Email(this, Support.Details(_store, Bank)));
        var whatsapp = new ToolStripMenuItem("&WhatsApp support");
        foreach (var p in AppInfo.Phones)
            whatsapp.DropDownItems.Add($"{p.Country}:  {p.Display}", null, (_, _) => Support.Open(p.WhatsAppLink(Support.WhatsAppMessage())));
        help.DropDownItems.Add(whatsapp);
        help.DropDownItems.Add("&Report a problem on GitHub", null, (_, _) => Support.Open(AppInfo.NewIssuePage));
        help.DropDownItems.Add(new ToolStripSeparator());
        help.DropDownItems.Add("Check for &updates...", null, async (_, _) => await CheckForUpdates(manual: true));
        help.DropDownItems.Add("&About RAK Special", null, async (_, _) => await ShowSupport());

        menu.Items.AddRange(new ToolStripItem[] { file, tools, help });
        menu.Renderer = new Theme.MenuRenderer();
        menu.BackColor = Theme.Navy;
        menu.Padding = new Padding(S(10), S(2), 0, S(2));
        foreach (ToolStripMenuItem top in menu.Items) top.ForeColor = Color.White;
        MainMenuStrip = menu;
        Controls.Add(menu);
        menu.SendToBack();   // docked first, so it sits at the very top
    }

    int S(int px) => (int)Math.Round(px * DeviceDpi / 96f);

    Panel _header = null!;

    void BuildLayout()
    {
        BackColor = Theme.Canvas;

        // ---------- header: logo, cheque book selector, quick actions ----------
        _header = new Panel { Dock = DockStyle.Top, Height = S(68), BackColor = Theme.Navy, Padding = new Padding(S(18), 0, S(12), 0) };

        var brand = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false, BackColor = Theme.Navy, Padding = new Padding(0, S(14), 0, 0) };
        using (var ico = new Icon(AppIcon.Get(), 40, 40))
            brand.Controls.Add(new PictureBox { Image = ico.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(S(40), S(40)), Margin = new Padding(0, 0, S(10), 0) });
        var titles = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = new Padding(0), BackColor = Theme.Navy };
        titles.Controls.Add(new Label { Text = AppInfo.Name, AutoSize = true, ForeColor = Theme.Gold, Font = Theme.Ui(14f, FontStyle.Bold), Margin = new Padding(0) });
        titles.Controls.Add(new Label { Text = "Cheque printing", AutoSize = true, ForeColor = Theme.OnNavyMuted, Font = Theme.Ui(8.5f), Margin = new Padding(S(2), 0, 0, 0) });
        brand.Controls.Add(titles);

        var bookArea = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Theme.Navy, Padding = new Padding(S(36), S(16), 0, 0) };
        _books.Margin = new Padding(0);
        _bookInfo.ForeColor = Theme.OnNavyMuted;
        _bookInfo.Margin = new Padding(S(12), S(9), 0, 0);
        bookArea.Controls.Add(_books);
        bookArea.Controls.Add(_bookInfo);

        var nav = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Navy, Padding = new Padding(0, S(14), 0, 0) };
        RoundButton NavButton(string text, string glyph, EventHandler click, string tip)
        {
            var b = new RoundButton { Text = text, Glyph = glyph, Kind = ButtonKind.OnNavy, Font = Theme.Ui(9.5f), Height = S(38), Margin = new Padding(S(2), 0, 0, 0), TabStop = false };
            b.Width = b.GetPreferredSize(Size.Empty).Width;
            b.Click += click;
            _tips.SetToolTip(b, tip);
            return b;
        }
        nav.Controls.Add(NavButton("Register", Theme.Glyph.Register, (_, _) => ShowRegister(), "Cheque register: every printed cheque, status and export"));
        nav.Controls.Add(NavButton("Analytics", Theme.Glyph.Analytics, (_, _) => ShowAnalytics(), "Totals by month and payee (Ctrl+T)"));
        nav.Controls.Add(NavButton("PDC series", Theme.Glyph.Calendar, (_, _) => ShowPdc(), "Print a series of post-dated cheques"));
        nav.Controls.Add(NavButton("Payees", Theme.Glyph.People, (_, _) => ShowPayees(), "Your payee list"));
        nav.Controls.Add(NavButton("Help", Theme.Glyph.Help, async (_, _) => await ShowSupport(), "Help & support (F1)"));

        _header.Controls.Add(bookArea);
        _header.Controls.Add(nav);
        _header.Controls.Add(brand);

        _books.SelectedIndexChanged += (_, _) =>
        {
            if (_fillingBooks || _books.SelectedIndex < 0) return;
            var b = _store.Data.Banks[_books.SelectedIndex];
            if (b.Id == Bank.Id) return;
            _store.Data.Settings.LastBankId = b.Id;
            Save();
            LoadBank();
        };

        // ---------- body ----------
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Padding = new Padding(S(18)) };

        // left: the cheque form
        var formCard = new Card { Dock = DockStyle.Left, Width = S(410), Padding = new Padding(S(22), S(18), S(22), S(18)) };
        var f = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Card, Margin = new Padding(0) };
        f.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        f.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        Label Caption(string t) => new()
        {
            Text = t.ToUpperInvariant(), AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Semibold(8f),
            Margin = new Padding(0, S(10), 0, S(4)),
        };
        void Row(Control c, int span = 2, int height = 0, int col = 0)
        {
            c.Margin = new Padding(0, c.Margin.Top, col == 0 && span == 1 ? S(6) : 0, c.Margin.Bottom);
            if (col == 1) c.Margin = new Padding(S(6), c.Margin.Top, 0, c.Margin.Bottom);
            f.RowCount++;
            f.RowStyles.Add(height > 0 ? new RowStyle(SizeType.Absolute, height) : new RowStyle(SizeType.AutoSize));
            f.Controls.Add(c, col, f.RowCount - 1);
            if (span == 2) f.SetColumnSpan(c, 2);
        }
        void Pair(Control left, Control right, int height = 0)
        {
            f.RowCount++;
            f.RowStyles.Add(height > 0 ? new RowStyle(SizeType.Absolute, height) : new RowStyle(SizeType.AutoSize));
            left.Margin = new Padding(0, left.Margin.Top, S(6), left.Margin.Bottom);
            right.Margin = new Padding(S(6), right.Margin.Top, 0, right.Margin.Bottom);
            f.Controls.Add(left, 0, f.RowCount - 1);
            f.Controls.Add(right, 1, f.RowCount - 1);
        }

        InputFrame Framed(Control c, int h) => new(c) { Dock = DockStyle.Fill, Height = S(h) };

        Row(new Label { Text = "New cheque", AutoSize = true, Font = Theme.Ui(15f, FontStyle.Bold), ForeColor = Theme.Text, Margin = new Padding(0) });
        Row(new Label { Text = "The preview updates as you type.", AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Ui(9.5f), Margin = new Padding(0, S(2), 0, S(4)) });
        Pair(Caption("Cheque No."), Caption("Date"));
        Pair(Framed(_chequeNo, 42), Framed(_date, 42), S(42));
        Row(Caption("Pay to"));
        Row(Framed(_payee, 42), height: S(42));
        Row(Caption("Amount (AED)"));
        Row(Framed(_amount, 56), height: S(56));

        var wordsCaption = new Label { Text = "AMOUNT IN WORDS", Dock = DockStyle.Top, Height = S(20), ForeColor = Theme.GoldDark, Font = Theme.Semibold(8f), BackColor = Theme.GoldSoft };
        _wordsBox.Controls.Add(_words);
        _wordsBox.Controls.Add(wordsCaption);
        _wordsBox.Margin = new Padding(0, S(12), 0, S(6));
        Row(_wordsBox, height: S(96));

        var toggles = new Panel { Dock = DockStyle.Fill, Height = S(70), BackColor = Theme.Card, Margin = new Padding(0, S(4), 0, 0) };
        _acPayee.Height = _bearer.Height = S(32);
        toggles.Controls.Add(_bearer);
        toggles.Controls.Add(_acPayee);
        Row(toggles, height: S(74));

        f.RowCount++;
        f.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // pushes the buttons to the bottom

        var print = new RoundButton { Text = "Print cheque", Glyph = Theme.Glyph.Print, Hint = "Ctrl+P", Kind = ButtonKind.Primary, Dock = DockStyle.Fill, Font = Theme.Semibold(11.5f), Radius = 10 };
        print.Click += (_, _) => PrintCheque();
        Row(print, height: S(52));
        var preview = new RoundButton { Text = "Preview", Glyph = Theme.Glyph.Preview, Dock = DockStyle.Fill, Margin = new Padding(0, S(8), 0, 0) };
        preview.Click += (_, _) => Preview();
        var test = new RoundButton { Text = "Test print", Glyph = Theme.Glyph.Page, Dock = DockStyle.Fill, Margin = new Padding(0, S(8), 0, 0) };
        test.Click += (_, _) => TestPrint();
        _tips.SetToolTip(test, "Print on plain paper with the cheque picture, to check alignment");
        Pair(preview, test, S(50));
        var clear = new RoundButton { Text = "Clear form", Glyph = Theme.Glyph.Clear, Kind = ButtonKind.Ghost, Dock = DockStyle.Fill, Font = Theme.Ui(9.5f), Margin = new Padding(0, S(6), 0, 0) };
        clear.Click += (_, _) => ClearEntry();
        Row(clear, height: S(42));
        formCard.Controls.Add(f);

        // right: live preview and figures
        var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Padding = new Padding(S(18), 0, 0, 0) };
        var previewCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(18), S(14), S(18), S(14)) };
        var previewHead = new Panel { Dock = DockStyle.Top, Height = S(34), BackColor = Theme.Card };
        previewHead.Controls.Add(new Label
        {
            Text = "The cheque picture is shown only on screen and on test prints. Real cheques get just your text.",
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Muted, Font = Theme.Ui(8.5f), AutoEllipsis = true,
        });
        previewHead.Controls.Add(new Label { Text = "Live preview", Dock = DockStyle.Left, AutoSize = true, Font = Theme.Ui(12f, FontStyle.Bold), ForeColor = Theme.Text, Padding = new Padding(0, S(4), S(16), 0) });
        previewCard.Controls.Add(_preview);
        previewCard.Controls.Add(previewHead);

        var stats = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = S(96), ColumnCount = 3, BackColor = Theme.Canvas, Padding = new Padding(0, S(14), 0, 0) };
        for (var i = 0; i < 3; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        foreach (var (tile, col) in new[] { (_statMonth, 0), (_statUncleared, 1), (_statNext, 2) })
        {
            tile.Dock = DockStyle.Fill;
            tile.Margin = new Padding(col == 0 ? 0 : S(7), 0, col == 2 ? 0 : S(7), 0);
            tile.Click += (_, _) => ShowAnalytics();
            _tips.SetToolTip(tile, "Open analytics");
            stats.Controls.Add(tile, col, 0);
        }

        right.Controls.Add(previewCard);
        right.Controls.Add(stats);

        body.Controls.Add(right);
        body.Controls.Add(formCard);

        // ---------- status bar ----------
        var status = new StatusStrip { SizingGrip = false, BackColor = Color.White, Renderer = new Theme.MenuRenderer(), Padding = new Padding(S(16), 0, S(16), 0) };
        status.Items.Add(_status);
        status.Items.Add(new ToolStripStatusLabel($"{AppInfo.Name} {Updates.Current}") { ForeColor = Theme.Muted });

        _amount.Leave += (_, _) => { if (TryAmount(out var a)) _amount.Text = AmountInWords.Figures(a); };
        AcceptButton = null;

        Controls.Add(body);
        Controls.Add(_header);
        Controls.Add(status);
    }

    void LoadBank()
    {
        _fillingBooks = true;
        _books.SetItems(_store.Data.Banks.Select(b => b.Name), _store.Data.Banks.IndexOf(Bank));
        _fillingBooks = false;

        Text = $"{AppInfo.Name} - {Bank.Name}";
        _bookInfo.Text = (Bank.Code.Length > 0 ? Bank.Code + "  ·  " : "") + $"{Bank.ChequeWidthMm:0.#} × {Bank.ChequeHeightMm:0.#} mm";
        _chequeNo.Text = Bank.NextChequeNo;
        RefreshPayeeList();
        UpdatePreview();
    }

    void RefreshPayeeList()
    {
        var current = _payee.Text;
        _payee.BeginUpdate();
        _payee.Items.Clear();
        foreach (var p in _store.Data.Payees.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            _payee.Items.Add(p.Name);
        _payee.EndUpdate();
        _payee.Text = current;
    }

    // ---------------- data from the form ----------------

    bool TryAmount(out decimal amount)
    {
        var t = _amount.Text.Trim().Replace("AED", "", StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    ChequeData CurrentData()
    {
        TryAmount(out var amount);
        return new ChequeData
        {
            ChequeNo = _chequeNo.Text.Trim(),
            Date = _date.Value.Date,
            Payee = _payee.Text,
            Amount = Math.Max(0, amount),
            AcPayeeOnly = _acPayee.Checked,
            StrikeBearer = _bearer.Checked,
        };
    }

    void UpdatePreview()
    {
        var data = CurrentData();
        var badNumber = _amount.Text.Trim().Length > 0 && !TryAmount(out _);

        string words;
        var tooLarge = false;
        try { words = ChequeText.Build(data, Bank, _store.Data.Settings).Words; }
        catch (ArgumentOutOfRangeException ex) { words = ex.Message; tooLarge = true; }
        var wrap = ChequeRenderer.WrapWords(Bank, words);

        // the gold box shows the words, or what is wrong with the amount
        (_words.Text, _words.ForeColor) =
            badNumber ? ("Type the amount as a number, like 1250.50", Theme.Danger)
            : data.Amount <= 0 ? ("The amount in words appears here.", Theme.Muted)
            : tooLarge ? (words, Theme.Danger)
            : wrap.Overflow ? (words + "\n\nToo long for the two lines on the cheque: make the font smaller in the layout editor.", Theme.Danger)
            : (words, Theme.Text);

        _preview.SetContent(Bank, data, _store.Data.Settings);
        var n = _store.Data.Cheques.Count;
        _status.ForeColor = wrap.Overflow ? Theme.Danger : Theme.Muted;
        _status.Text = wrap.Overflow
            ? "Amount in words is too long for the two lines on the cheque. Make the font smaller in the layout editor."
            : $"{n:#,##0} {(n == 1 ? "cheque" : "cheques")} in the register   ·   Cheque book: {Bank.Name}";
        RefreshStats();
    }

    /// <summary>The three figures under the preview (all cheque books).</summary>
    void RefreshStats()
    {
        var today = DateTime.Today;
        var all = _store.Data.Cheques;
        var (from, to) = Analytics.Range(AnalyticsPeriod.ThisMonth, today);
        var month = Analytics.Summarize(Analytics.Filter(all, from, to), today);
        _statMonth.Set($"AED {AmountInWords.Figures(month.Total)}", $"{month.Cheques} {(month.Cheques == 1 ? "cheque" : "cheques")} in {today:MMMM}");

        var open = Analytics.Summarize(all, today);
        _statUncleared.Set($"AED {AmountInWords.Figures(open.UnclearedAmount)}",
            open.Uncleared == 0 ? "Nothing waiting" : $"{open.Uncleared} due, not marked Cleared",
            open.Uncleared > 0 ? Theme.Warning : null);

        var next = Analytics.Upcoming(all, today).FirstOrDefault();
        if (next == null) _statNext.Set("None", "No post-dated cheques waiting");
        else _statNext.Set($"AED {AmountInWords.Figures(next.Amount)}",
            $"{next.ChequeDate:dd MMM yyyy}  ·  {next.Payee}");
    }

    /// <summary>Checks the entry before a real print. Returns false (and tells the user) when something is wrong.</summary>
    bool ValidateCheque(ChequeData data)
    {
        string? problem = null;
        if (data.Payee.Trim().Length == 0) problem = "Please enter who the cheque is paid to.";
        else if (!TryAmount(out var a) || a <= 0) problem = "Please enter an amount greater than zero.";
        else if (decimal.Round(a, 2) != a) problem = "Amount can have at most 2 decimals (fils).";
        else if (a > AmountInWords.MaxAmount) problem = "Amount is too large.";
        else if (ChequeRenderer.WrapWords(Bank, ChequeText.Build(data, Bank, _store.Data.Settings).Words).Overflow)
            problem = "Amount in words does not fit on the cheque lines.";
        if (problem != null)
        {
            MessageBox.Show(this, problem, "Check the cheque", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (data.ChequeNo.Length == 0 &&
            MessageBox.Show(this, "No cheque number entered. Print anyway?", "Cheque number",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;

        var dup = _store.Data.Cheques.FirstOrDefault(c => c.Status.UsesNumber() && data.ChequeNo.Length > 0 &&
                                                         c.BankId == Bank.Id && c.ChequeNo == data.ChequeNo);
        if (dup != null && MessageBox.Show(this,
                $"Cheque No. {dup.ChequeNo} was already printed on {dup.PrintedAt:dd-MM-yyyy} for {dup.Payee} ({AmountInWords.Figures(dup.Amount)}).\n\nPrint again with the same number?",
                "Duplicate cheque number", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return false;
        return true;
    }

    // ---------------- actions ----------------

    void PrintCheque()
    {
        var data = CurrentData();
        if (!ValidateCheque(data)) return;
        var text = ChequeText.Build(data, Bank, _store.Data.Settings);

        if (_store.Data.Settings.ConfirmBeforePrint && MessageBox.Show(this,
                $"Cheque No.: {data.ChequeNo}\nDate: {text.Date}\nPay to: {data.Payee.Trim()}\nAmount: AED {AmountInWords.Figures(data.Amount)}\n{text.Words}\n\nPut the cheque in the printer and click OK.",
                "Print cheque", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;

        using var doc = ChequePrinter.Create(Bank, data, _store.Data.Settings, testPrint: false);
        using var dlg = new PrintDialog { Document = doc, UseEXDialog = true, AllowSomePages = false, AllowSelection = false };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        Bank.PrinterName = doc.PrinterSettings.PrinterName;

        try { doc.Print(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Printing failed:\n" + ex.Message, "Print", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _store.Data.Cheques.Add(new ChequeRecord
        {
            ChequeNo = data.ChequeNo, BankId = Bank.Id, BankName = Bank.Name, ChequeDate = data.Date,
            Payee = data.Payee.Trim(), Amount = data.Amount, AmountInWords = text.Words, AcPayeeOnly = data.AcPayeeOnly,
        });
        _store.RememberPayee(data.Payee);
        if (data.ChequeNo.Length > 0) Bank.NextChequeNo = DataStore.NextNumber(data.ChequeNo);
        Save();

        _chequeNo.Text = Bank.NextChequeNo;
        _payee.Text = ""; _amount.Text = "";
        RefreshPayeeList();
        UpdatePreview();
        _payee.Focus();
    }

    void Preview()
    {
        using var doc = ChequePrinter.Create(Bank, CurrentData(), _store.Data.Settings, testPrint: false);
        using var dlg = new PrintPreviewDialog
        {
            Document = doc, Width = 1100, Height = 800, UseAntiAlias = true, Icon = AppIcon.Get(),
            Text = $"Preview - {Bank.Name} (the cheque picture is only shown, never printed)",
        };
        dlg.ShowDialog(this);
    }

    void ShowPdc()
    {
        TryAmount(out var amount);
        using var f = new PdcForm(_store, Bank, _payee.Text, amount, _chequeNo.Text.Trim(), _date.Value.Date);
        f.ShowDialog(this);
        _chequeNo.Text = Bank.NextChequeNo;
        RefreshPayeeList();
        UpdatePreview();
    }

    void TestPrint()
    {
        var data = CurrentData();
        if (data.Payee.Trim().Length == 0) data.Payee = "Sample Payee Trading LLC";
        if (data.Amount <= 0) data.Amount = 1354.50m;
        using var doc = ChequePrinter.Create(Bank, data, _store.Data.Settings, testPrint: true);
        using var dlg = new PrintDialog { Document = doc, UseEXDialog = true };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { doc.Print(); }
        catch (Exception ex) { MessageBox.Show(this, "Printing failed:\n" + ex.Message, "Print", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void ClearEntry()
    {
        _payee.Text = ""; _amount.Text = ""; _date.Value = DateTime.Today;
        _acPayee.Checked = true; _bearer.Checked = true;
        _chequeNo.Text = Bank.NextChequeNo;
        _payee.Focus();
    }

    /// <summary>Called from the register: fills the form with an earlier cheque so it can be printed again.</summary>
    public void LoadFromRecord(ChequeRecord r)
    {
        _chequeNo.Text = r.ChequeNo; _date.Value = r.ChequeDate; _payee.Text = r.Payee;
        _amount.Text = AmountInWords.Figures(r.Amount); _acPayee.Checked = r.AcPayeeOnly;
        UpdatePreview();
    }

    void ShowRegister()
    {
        using var f = new RegisterForm(_store, this);
        f.ShowDialog(this);
        UpdatePreview();
    }

    async Task ShowSupport()
    {
        DialogResult r;
        using (var f = new SupportForm(_store, Bank)) r = f.ShowDialog(this);
        if (r == DialogResult.Retry) await CheckForUpdates(manual: true);   // "Check for updates" button
    }

    void ShowAnalytics()
    {
        using var f = new AnalyticsForm(_store, this);
        f.ShowDialog(this);
        UpdatePreview();   // statuses may have changed from its register view
    }

    void ShowPayees()
    {
        using var f = new PayeesForm(_store);
        f.ShowDialog(this);
        RefreshPayeeList();
    }

    void ShowLayoutEditor()
    {
        using var f = new LayoutEditorForm(_store, Bank, CurrentData());
        if (f.ShowDialog(this) == DialogResult.OK) Save();
        LoadBank();
    }

    void ShowSettings()
    {
        var copy = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
            System.Text.Json.JsonSerializer.Serialize(_store.Data.Settings))!;
        using var f = new PropertyDialog("Settings - wording and formats", copy);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        copy.LastBankId = _store.Data.Settings.LastBankId;
        _store.Data.Settings = copy;
        Save();
        UpdatePreview();
    }

    void ShowAlignHelp() => MessageBox.Show(this,
        "1. Click 'Test print' and print on plain paper. It prints the cheque outline and all fields.\n" +
        "2. Hold the printout over a real cheque against a window.\n" +
        "3. If everything is shifted the same way, open Tools > Layout editor > Cheque settings and change\n" +
        "   Offset X (right +, left -) and Offset Y (down +, up -) in mm.\n" +
        "4. If only one field is wrong, drag that field in the layout editor (or use the arrow keys).\n" +
        "5. Feed the cheque the same way every time (against the paper guide).",
        "Aligning with your printer", MessageBoxButtons.OK, MessageBoxIcon.Information);

    void Save()
    {
        try { _store.Save(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save data:\n" + ex.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void Backup()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "RAK Special backup (*.json)|*.json",
            FileName = $"RAKSpecial-backup-{DateTime.Now:yyyyMMdd-HHmm}.json",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _store.BackupTo(dlg.FileName);
            MessageBox.Show(this, "Backup saved.", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Backup failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void Restore()
    {
        using var dlg = new OpenFileDialog { Filter = "RAK Special backup (*.json)|*.json" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (MessageBox.Show(this, "Replace ALL current data (register, payees, layout) with this backup?\nThe current data is kept as a .bak file.",
                "Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            _store.RestoreFrom(dlg.FileName);
            LoadBank();
            MessageBox.Show(this, "Data restored.", "Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Restore failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void OpenDataFolder()
    {
        var folder = Path.GetDirectoryName(_store.FilePath)!;
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }
}
