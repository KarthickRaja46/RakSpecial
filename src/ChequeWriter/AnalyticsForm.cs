using System.Globalization;
using System.Text;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>
/// Where the money went: an overview dashboard (monthly trend, top payees, categories, cheque status and
/// plain-language findings), plus detail views by month, payee, category, post-dated cheques and the cheque list.
/// Everything is worked out from the cheque register.
/// </summary>
public class AnalyticsForm : Form
{
    readonly DataStore _store;
    readonly MainForm _main;
    readonly DateTime _today = DateTime.Today;

    // ---- filters ----
    readonly SegmentedControl _period = new() { Light = true, BaseHeight = 34, Font = Theme.Semibold(9.5f) };
    readonly SegmentedControl _book = new() { Light = true, BaseHeight = 34, Font = Theme.Semibold(9.5f) };
    readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Font = Theme.Ui(10f) };
    readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Font = Theme.Ui(10f) };
    readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Font = Theme.Ui(10f) };
    readonly Label _rangeLabel = new() { AutoSize = true, ForeColor = Theme.TextSoft, Font = Theme.Ui(9.5f) };
    Control _fromFrame = null!, _toFrame = null!;

    // ---- figures and views ----
    readonly StatTile[] _tiles =
    {
        new("Total paid"), new("Average cheque"), new("Top payee"),
        new("Awaiting clearing"), new("Bounced / stopped"), new("Due in next 30 days"),
    };
    readonly SegmentedControl _views = new() { Light = true, BaseHeight = 38, Font = Theme.Semibold(10f) };
    readonly Panel _host = new() { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
    readonly List<Control> _pages = new();
    readonly Label _note = new() { Dock = DockStyle.Bottom, ForeColor = Theme.TextSoft, Font = Theme.Ui(9f), BackColor = Theme.Canvas, TextAlign = ContentAlignment.MiddleLeft };

    // overview
    readonly BarChart _ovMonth = new() { ShowAverage = true };
    readonly InsightList _insights = new();
    readonly HBarChart _ovPayees = new() { EmptyText = "No payments in this period." };
    readonly DonutChart _ovCats = new() { CenterCaption = "total paid", EmptyText = "No payments in this period." };
    readonly StatusChart _ovStatus = new();

    // by month
    readonly BarChart _monthChart = new() { ShowAverage = true };
    readonly DataGridView _monthGrid = NewGrid();

    // by payee
    readonly TextBox _payeeSearch = new() { PlaceholderText = "Search payee", Font = Theme.Ui(10f) };
    readonly DataGridView _payeeGrid = NewGrid();
    readonly Label _payeeChartTitle = new();
    readonly BarChart _payeeChart = new() { EmptyText = "Select a payee to see their monthly payments." };

    // by category
    readonly DonutChart _catChart = new() { CenterCaption = "total paid", EmptyText = "No payments in this period." };
    readonly DataGridView _catGrid = NewGrid();

    // post-dated
    readonly SegmentedControl _upcomingRange = new() { Light = true, BaseHeight = 34, Font = Theme.Semibold(9.5f) };
    readonly Label _upcomingTotal = new() { AutoSize = true, Font = Theme.Semibold(10f), ForeColor = Theme.Text };
    readonly BarChart _upcomingChart = new() { EmptyText = "No post-dated cheques waiting." };
    readonly DataGridView _upcomingGrid = NewGrid();

    // cheques
    readonly DataGridView _chequeGrid = NewGrid();
    readonly ToggleSwitch _showRemoved = new() { Text = "Show removed cheques" };
    readonly Label _chequeTotal = new() { AutoSize = true, Font = Theme.Semibold(10f), ForeColor = Theme.Text };

    const string AllCategories = "All categories";

    static readonly (string Text, AnalyticsPeriod Period)[] Periods =
    {
        ("This month", AnalyticsPeriod.ThisMonth), ("Last month", AnalyticsPeriod.LastMonth),
        ("Last 3 months", AnalyticsPeriod.Last3Months), ("Last 12 months", AnalyticsPeriod.Last12Months),
        ("This year", AnalyticsPeriod.ThisYear), ("Last year", AnalyticsPeriod.LastYear),
        ("All time", AnalyticsPeriod.AllTime), ("Custom", AnalyticsPeriod.Custom),
    };
    static readonly (string Text, int? Days)[] UpcomingRanges =
        { ("Next 30 days", 30), ("Next 90 days", 90), ("Next 12 months", 365), ("All post-dated", null) };
    static readonly string[] ViewNames = { "Overview", "By month", "By payee", "By category", "Post-dated", "Cheques" };
    static readonly string[] ViewNotes =
    {
        "Totals count Printed and Cleared cheques by cheque date; bounced, stopped and void are left out. Click a month, a payee or a status to see the details.",
        "Totals count Printed and Cleared cheques by cheque date. Bounced, stopped and void cheques are left out.",
        "Double-click a payee to see their cheques in the register. 'Set category' groups payees on the By category view.",
        "Set each payee's category in Payees, or with 'Set category' on the By payee view.",
        "Post-dated cheques you have given out that are not due yet. Keep this much in the account by each date.",
        "Removed cheques (e.g. test prints) stay in the cheque register but are left out of every total and chart.",
    };

    List<ChequeRecord> _inPeriod = new();
    List<ChequeRecord> _scope = new();
    List<PayeeTotal> _payees = new();
    List<MonthTotal> _months = new();
    List<string> _categoryOrder = new();
    Font? _removedFont;
    bool _loading = true;

    public AnalyticsForm(DataStore store, MainForm main)
    {
        _store = store; _main = main;
        Text = "Analytics - where the money went";
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Theme.Canvas;
        AppIcon.Apply(this);
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        MinimumSize = new Size(Math.Min(S(1000), screen.Width), Math.Min(S(680), screen.Height));
        Size = new Size(Math.Min(S(1340), screen.Width), Math.Min(S(900), screen.Height));
        StartPosition = FormStartPosition.CenterParent;
        if (screen.Height < S(900) || screen.Width < S(1340)) WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        _categoryOrder = PayeeCategories.All(_store.Data.Payees);

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Padding = new Padding(S(11), S(12), S(11), 0) };
        body.Controls.Add(_host);
        body.Controls.Add(BuildViewTabs());
        body.Controls.Add(BuildTiles());
        _note.Height = S(28);
        _note.Padding = new Padding(S(18), 0, S(18), 0);

        Controls.Add(body);
        Controls.Add(BuildFilters());
        Controls.Add(BuildHeader());
        Controls.Add(_note);

        _pages.Add(OverviewPage());
        _pages.Add(MonthPage());
        _pages.Add(PayeePage());
        _pages.Add(CategoryPage());
        _pages.Add(UpcomingPage());
        _pages.Add(ChequesPage());
        foreach (var p in _pages) { p.Dock = DockStyle.Fill; p.Visible = false; _host.Controls.Add(p); }

        _period.SelectedIndexChanged += (_, _) => { ApplyPeriod(); if (!_loading) Reload(); };
        _from.ValueChanged += (_, _) => { if (!_loading) Reload(); };
        _to.ValueChanged += (_, _) => { if (!_loading) Reload(); };
        _book.SelectedIndexChanged += (_, _) => { if (!_loading) Reload(); };
        _category.SelectedIndexChanged += (_, _) => { if (!_loading) Reload(); };
        _views.SelectedIndexChanged += (_, _) => ShowView(_views.SelectedIndex);
        _showRemoved.CheckedChanged += (_, _) => FillCheques();
        _payeeSearch.TextChanged += (_, _) => FillPayees();
        _upcomingRange.SelectedIndexChanged += (_, _) => FillUpcoming();

        ApplyPeriod();
        ShowView(0);
        _loading = false;
        Reload();
    }

    int S(int px) => (int)Math.Round(px * DeviceDpi / 96f);

    // ---------------- layout ----------------

    Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = S(56), BackColor = Theme.Navy, Padding = new Padding(S(20), 0, S(12), 0) };
        var titles = new TableLayoutPanel { Dock = DockStyle.Left, AutoSize = true, ColumnCount = 1, BackColor = Theme.Navy, Padding = new Padding(0, S(6), 0, 0) };
        titles.Controls.Add(new Label { Text = "Analytics", AutoSize = true, ForeColor = Theme.Gold, Font = Theme.Ui(14f, FontStyle.Bold), Margin = new Padding(0) });
        titles.Controls.Add(new Label { Text = "Where the money went, from your cheque register", AutoSize = true, ForeColor = Theme.OnNavyMuted, Font = Theme.Ui(9f), Margin = new Padding(S(2), 0, 0, 0) });

        var nav = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Navy, Padding = new Padding(0, S(9), 0, 0) };
        RoundButton Nav(string text, string glyph, EventHandler click)
        {
            var b = new RoundButton { Text = text, Glyph = glyph, Kind = ButtonKind.OnNavy, Font = Theme.Ui(9.5f), Height = S(38), Margin = new Padding(S(2), 0, 0, 0), TabStop = false };
            b.Width = b.GetPreferredSize(Size.Empty).Width;
            b.Click += click;
            return b;
        }
        nav.Controls.Add(Nav("Export to Excel", Theme.Glyph.Page, (_, _) => Export()));
        nav.Controls.Add(Nav("Cheque register", Theme.Glyph.Register, (_, _) => OpenRegister("")));
        header.Controls.Add(titles);
        header.Controls.Add(nav);
        return header;
    }

    Control BuildFilters()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = S(100), BackColor = Theme.Card, Padding = new Padding(S(20), S(10), S(20), S(6)) };
        Label Caption(string t) => new()
        {
            Text = t.ToUpperInvariant(), AutoSize = false, Width = S(108), Height = S(34), TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.TextSoft, Font = Theme.Semibold(8.5f), Margin = new Padding(0, 0, S(4), 0),
        };
        FlowLayoutPanel Row() => new() { Dock = DockStyle.Top, Height = S(42), WrapContents = false, BackColor = Theme.Card, Margin = new Padding(0) };

        _period.SetItems(Periods.Select(p => p.Text), 3);
        _period.Margin = new Padding(0, 0, S(12), 0);
        _fromFrame = new InputFrame(_from) { Width = S(140), Height = S(34), Margin = new Padding(0, 0, S(8), 0) };
        _toFrame = new InputFrame(_to) { Width = S(140), Height = S(34), Margin = new Padding(0) };
        var row1 = Row();
        row1.Controls.AddRange(new[] { Caption("Period"), _period, _fromFrame, _toFrame });

        _book.SetItems(new[] { "All cheque books" }.Concat(_store.Data.Banks.Select(b => b.Name)), 0);
        _book.Margin = new Padding(0, 0, S(24), 0);
        _category.Items.Add(AllCategories);
        foreach (var c in _categoryOrder) _category.Items.Add(c);
        _category.Items.Add(PayeeCategories.None);
        _category.SelectedIndex = 0;
        var catFrame = new InputFrame(_category) { Width = S(190), Height = S(34), Margin = new Padding(0, 0, S(20), 0) };
        var catCaption = Caption("Category");
        catCaption.Width = S(76);
        _rangeLabel.Margin = new Padding(0, S(8), 0, 0);
        var row2 = Row();
        row2.Controls.AddRange(new Control[] { Caption("Cheque book"), _book, catCaption, catFrame, _rangeLabel });

        bar.Controls.Add(row2);
        bar.Controls.Add(row1);
        var line = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border };
        var wrap = new Panel { Dock = DockStyle.Top, Height = bar.Height + 1 };
        wrap.Controls.Add(bar);
        wrap.Controls.Add(line);
        bar.Dock = DockStyle.Fill;
        return wrap;
    }

    Control BuildTiles()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Top, Height = S(96), ColumnCount = _tiles.Length, BackColor = Theme.Canvas, Padding = new Padding(0, 0, 0, S(8)) };
        int[] goTo = { 1, 5, 2, 5, 5, 4 };
        for (var i = 0; i < _tiles.Length; i++)
        {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _tiles.Length));
            var tile = _tiles[i];
            tile.Dock = DockStyle.Fill;
            tile.Margin = new Padding(S(7), 0, S(7), 0);
            tile.Padding = new Padding(S(14), S(10), S(10), S(8));
            var view = goTo[i];
            var index = i;
            tile.Click += (_, _) =>
            {
                _views.SelectedIndex = view;
                if (index == 2 && _payees.FirstOrDefault(p => p.Total > 0) is { } top) SelectPayee(top.Payee);
            };
            t.Controls.Add(tile, i, 0);
        }
        return t;
    }

    Control BuildViewTabs()
    {
        var p = new Panel { Dock = DockStyle.Top, Height = S(50), BackColor = Theme.Canvas, Padding = new Padding(S(7), S(4), 0, S(8)) };
        _views.SetItems(ViewNames, 0);
        _views.Location = new Point(S(7), S(4));
        p.Controls.Add(_views);
        return p;
    }

    /// <summary>A white card with a title (and optional subtitle and header control) around one chart or table.</summary>
    Card ChartCard(Label title, string? sub, Control content, Control? right = null, int pad = 16)
    {
        var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(pad), S(12), S(pad), S(10)), Margin = new Padding(S(7)) };
        var head = new Panel { Dock = DockStyle.Top, Height = sub == null ? S(32) : S(48), BackColor = Theme.Card };
        title.AutoSize = false;
        title.Dock = DockStyle.Top;
        title.Height = S(24);
        title.Font = Theme.Semibold(11.5f);
        title.ForeColor = Theme.Text;
        title.BackColor = Theme.Card;
        title.AutoEllipsis = true;
        if (sub != null)
            head.Controls.Add(new Label { Text = sub, Dock = DockStyle.Top, Height = S(20), Font = Theme.Ui(9f), ForeColor = Theme.Muted, BackColor = Theme.Card, AutoEllipsis = true });
        head.Controls.Add(title);
        if (right != null) { right.Dock = DockStyle.Right; head.Controls.Add(right); right.BringToFront(); }
        content.Dock = DockStyle.Fill;
        card.Controls.Add(content);
        card.Controls.Add(head);
        if (pad < 16 && content is DataGridView) head.Padding = new Padding(S(16 - pad), 0, 0, 0);
        return card;
    }

    Card ChartCard(string title, string? sub, Control content, Control? right = null, int pad = 16) =>
        ChartCard(new Label { Text = title }, sub, content, right, pad);

    static TableLayoutPanel Grid(int cols, int rows, float[] colPct, float[] rowPct)
    {
        var t = new TableLayoutPanel { ColumnCount = cols, RowCount = rows, BackColor = Theme.Canvas, Margin = new Padding(0) };
        foreach (var c in colPct) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, c));
        foreach (var r in rowPct) t.RowStyles.Add(new RowStyle(SizeType.Percent, r));
        return t;
    }

    FlowLayoutPanel Toolbar() => new()
    {
        Dock = DockStyle.Top, Height = S(50), WrapContents = false, BackColor = Theme.Canvas, Padding = new Padding(S(7), S(2), 0, S(6)),
    };

    RoundButton Button(string text, string glyph, EventHandler click, ButtonKind kind = ButtonKind.Secondary)
    {
        var b = new RoundButton { Text = text, Glyph = glyph, Kind = kind, Font = Theme.Semibold(9.5f), Height = S(36), Margin = new Padding(0, 0, S(8), 0) };
        b.Width = b.GetPreferredSize(Size.Empty).Width;
        b.Click += click;
        return b;
    }

    Control OverviewPage()
    {
        var t = Grid(3, 2, new[] { 34f, 33f, 33f }, new[] { 47f, 53f });
        t.Controls.Add(ChartCard("Paid by month", "Printed and cleared cheques, by cheque date. Click a month for its details.", _ovMonth), 0, 0);
        t.SetColumnSpan(t.GetControlFromPosition(0, 0)!, 2);
        t.Controls.Add(ChartCard("Good to know", null, _insights), 2, 0);
        t.Controls.Add(ChartCard("Top payees", null, _ovPayees), 0, 1);
        t.Controls.Add(ChartCard("By category", null, _ovCats), 1, 1);
        t.Controls.Add(ChartCard("Where your cheques stand", null, _ovStatus), 2, 1);

        _ovMonth.BarClicked += (_, i) => { if (i < _months.Count) { _views.SelectedIndex = 1; SelectMonth(_months[i].Start); } };
        _ovPayees.ItemClicked += (_, i) =>
        {
            var top = _payees.Where(p => p.Total > 0).ToList();
            if (i < top.Count) { _views.SelectedIndex = 2; SelectPayee(top[i].Payee); }
        };
        _ovStatus.StateClicked += (_, _) => _views.SelectedIndex = 5;
        return t;
    }

    Control MonthPage()
    {
        Col(_monthGrid, "Month", 140);
        Col(_monthGrid, "Cheques", 80, "#,##0", right: true);
        Col(_monthGrid, "Total (AED)", 120, "#,##0.00", right: true);
        Col(_monthGrid, "Average (AED)", 120, "#,##0.00", right: true);
        Col(_monthGrid, "Change vs previous month", 150, right: true);
        _monthGrid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex == 4 && e.Value is string s && s.Length > 0)
                e.CellStyle!.ForeColor = s.StartsWith('+') ? Theme.Danger : s.StartsWith('-') ? Color.FromArgb(0, 99, 0) : Theme.TextSoft;
        };
        var t = Grid(1, 2, new[] { 100f }, new[] { 52f, 48f });
        t.Controls.Add(ChartCard("Paid by month", "The gold line is the monthly average", _monthChart), 0, 0);
        t.Controls.Add(ChartCard("Month by month", null, _monthGrid, pad: 6), 0, 1);
        _monthChart.BarClicked += (_, i) => { if (i < _months.Count) SelectMonth(_months[i].Start); };
        return t;
    }

    Control PayeePage()
    {
        Col(_payeeGrid, "Payee", 300);
        Col(_payeeGrid, "Category", 110);
        Col(_payeeGrid, "Cheques", 80, "#,##0", right: true);
        Col(_payeeGrid, "Total (AED)", 120, "#,##0.00", right: true);
        Col(_payeeGrid, "Share", 70, "0.0%", right: true);
        Col(_payeeGrid, "Average", 100, "#,##0.00", right: true);
        Col(_payeeGrid, "Largest", 100, "#,##0.00", right: true);
        Col(_payeeGrid, "Last cheque", 105, "dd-MM-yyyy");
        Col(_payeeGrid, "Bounced", 80, "#,##0", right: true);
        _payeeGrid.SelectionChanged += (_, _) => FillPayeeChart();
        _payeeGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ShowPayeeCheques(); };

        var bar = Toolbar();
        bar.Controls.Add(new InputFrame(_payeeSearch) { Width = S(260), Height = S(36), Margin = new Padding(0, 0, S(10), 0) });
        bar.Controls.Add(Button("Show cheques", Theme.Glyph.Register, (_, _) => ShowPayeeCheques()));
        bar.Controls.Add(Button("Set category", Theme.Glyph.People, (_, _) => SetPayeeCategory()));

        var t = Grid(2, 1, new[] { 64f, 36f }, new[] { 100f });
        t.Controls.Add(ChartCard("Payees", null, _payeeGrid, pad: 6), 0, 0);
        _payeeChartTitle.Text = "Monthly payments";
        t.Controls.Add(ChartCard(_payeeChartTitle, "Click a payee in the list", _payeeChart), 1, 0);

        var page = new Panel { BackColor = Theme.Canvas };
        t.Dock = DockStyle.Fill;
        page.Controls.Add(t);
        page.Controls.Add(bar);
        return page;
    }

    Control CategoryPage()
    {
        Col(_catGrid, "Category", 180);
        Col(_catGrid, "Cheques", 80, "#,##0", right: true);
        Col(_catGrid, "Total (AED)", 120, "#,##0.00", right: true);
        Col(_catGrid, "Share", 80, "0.0%", right: true);
        Col(_catGrid, "Average (AED)", 120, "#,##0.00", right: true);
        var t = Grid(2, 1, new[] { 48f, 52f }, new[] { 100f });
        t.Controls.Add(ChartCard("Share by category", "Hover a slice or a name for the amount", _catChart), 0, 0);
        t.Controls.Add(ChartCard("Categories", null, _catGrid, pad: 6), 1, 0);
        return t;
    }

    Control UpcomingPage()
    {
        Col(_upcomingGrid, "Cheque date", 95, "dd-MM-yyyy");
        Col(_upcomingGrid, "Days to go", 75, "#,##0", right: true);
        Col(_upcomingGrid, "Cheque No.", 85);
        Col(_upcomingGrid, "Payee", 240);
        Col(_upcomingGrid, "Amount (AED)", 110, "#,##0.00", right: true);
        Col(_upcomingGrid, "Cheque book", 130);
        Col(_upcomingGrid, "Notes", 160);
        _upcomingRange.SetItems(UpcomingRanges.Select(r => r.Text), 0);
        _upcomingRange.Margin = new Padding(0, 0, S(16), 0);
        _upcomingTotal.Margin = new Padding(0, S(8), 0, 0);

        var bar = Toolbar();
        bar.Controls.Add(_upcomingRange);
        bar.Controls.Add(_upcomingTotal);
        var t = Grid(1, 2, new[] { 100f }, new[] { 50f, 50f });
        t.Controls.Add(ChartCard("Falling due per month", "Money that must be in the account each month", _upcomingChart), 0, 0);
        t.Controls.Add(ChartCard("Post-dated cheques", null, _upcomingGrid, pad: 6), 0, 1);
        var page = new Panel { BackColor = Theme.Canvas };
        t.Dock = DockStyle.Fill;
        page.Controls.Add(t);
        page.Controls.Add(bar);
        return page;
    }

    Control ChequesPage()
    {
        Col(_chequeGrid, "Cheque date", 95, "dd-MM-yyyy");
        Col(_chequeGrid, "Cheque No.", 85);
        Col(_chequeGrid, "Payee", 240);
        Col(_chequeGrid, "Category", 110);
        Col(_chequeGrid, "Amount (AED)", 110, "#,##0.00", right: true);
        Col(_chequeGrid, "Status", 90);
        Col(_chequeGrid, "In analytics", 80);
        _chequeGrid.MultiSelect = true;
        _chequeGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || _chequeGrid.Rows[e.RowIndex].Tag is not ChequeRecord r) return;
            if (r.ExcludedFromAnalytics)
            {
                e.CellStyle!.ForeColor = Color.Gray;
                e.CellStyle.Font = _removedFont ??= new Font(_chequeGrid.DefaultCellStyle.Font ?? Font, FontStyle.Strikeout);
            }
            else if (e.ColumnIndex == 5)
                e.CellStyle!.ForeColor = r.Status switch
                {
                    ChequeStatus.Cleared => Color.FromArgb(0, 99, 0),
                    ChequeStatus.Bounced => Theme.Danger,
                    ChequeStatus.Stopped => Theme.Warning,
                    ChequeStatus.Void => Theme.Muted,
                    _ => Theme.Text,
                };
        };

        _showRemoved.Width = S(210);
        _showRemoved.Height = S(36);
        _showRemoved.Margin = new Padding(S(8), 0, S(12), 0);
        _chequeTotal.Margin = new Padding(0, S(8), 0, 0);
        var bar = Toolbar();
        bar.Controls.Add(Button("Remove from analytics", Theme.Glyph.Void, (_, _) => SetRemoved(true), ButtonKind.Danger));
        bar.Controls.Add(Button("Put back", Theme.Glyph.Undo, (_, _) => SetRemoved(false)));
        bar.Controls.Add(_showRemoved);
        bar.Controls.Add(_chequeTotal);
        var card = ChartCard("Cheques in this period", "Select one or more cheques (Ctrl+click), then Remove or Put back", _chequeGrid, pad: 6);
        card.Margin = new Padding(S(7));
        var t = Grid(1, 1, new[] { 100f }, new[] { 100f });
        t.Controls.Add(card, 0, 0);
        var page = new Panel { BackColor = Theme.Canvas };
        t.Dock = DockStyle.Fill;
        page.Controls.Add(t);
        page.Controls.Add(bar);
        return page;
    }

    void ShowView(int i)
    {
        if (i < 0 || i >= _pages.Count) return;
        for (var k = 0; k < _pages.Count; k++) _pages[k].Visible = k == i;
        _note.Text = ViewNotes[i];
        // open By payee on the first (biggest) payee, so its chart is never empty
        if (i == 2 && SelectedPayee == null && _payeeGrid.Rows.Count > 0)
        {
            _payeeGrid.CurrentCell = _payeeGrid.Rows[0].Cells[0];
            FillPayeeChart();
        }
    }

    static DataGridView NewGrid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        g.HandleCreated += (_, _) => Theme.StyleGrid(g);
        Theme.StyleGrid(g);
        return g;
    }

    static void Col(DataGridView g, string header, int weight, string? format = null, bool right = false)
    {
        var c = new DataGridViewTextBoxColumn { HeaderText = header, FillWeight = weight, SortMode = DataGridViewColumnSortMode.Automatic };
        if (format != null) c.DefaultCellStyle.Format = format;
        if (right)
        {
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        g.Columns.Add(c);
    }

    // ---------------- data ----------------

    AnalyticsPeriod SelectedPeriod => Periods[Math.Max(0, _period.SelectedIndex)].Period;
    string? BookId => _book.SelectedIndex > 0 ? _store.Data.Banks[_book.SelectedIndex - 1].Id : null;

    void ApplyPeriod()
    {
        var custom = SelectedPeriod == AnalyticsPeriod.Custom;
        _fromFrame.Visible = _toFrame.Visible = custom;
        if (custom) return;
        var (from, to) = Analytics.Range(SelectedPeriod, _today);
        var wasLoading = _loading;
        _loading = true;
        var all = _store.Data.Cheques;
        _from.Value = from ?? (all.Count > 0 ? all.Min(r => r.ChequeDate).Date : _today);
        _to.Value = to ?? (all.Count > 0 ? all.Max(r => r.ChequeDate).Date : _today);
        _loading = wasLoading;
    }

    (DateTime? From, DateTime? To) Range =>
        SelectedPeriod == AnalyticsPeriod.AllTime ? (null, null) : (_from.Value.Date, _to.Value.Date);

    string? SelectedCategory => _category.SelectedItem is string c && c != AllCategories ? c : null;

    /// <summary>Cheque book and category filters (not the period).</summary>
    bool InScope(ChequeRecord r) =>
        (BookId == null || r.BankId == BookId) &&
        (SelectedCategory == null || string.Equals(_store.Data.CategoryOf(r.Payee), SelectedCategory, StringComparison.OrdinalIgnoreCase));

    void Reload()
    {
        var (from, to) = Range;
        _scope = _store.Data.Cheques.Where(InScope).ToList();
        _inPeriod = Analytics.Filter(_scope, from, to).ToList();   // the Analytics functions leave out removed cheques themselves
        _payees = Analytics.ByPayee(_inPeriod);

        var counted = _inPeriod.Count(r => !r.ExcludedFromAnalytics);
        _rangeLabel.Text = (from == null ? "All dates" : $"{from:dd MMM yyyy}  to  {to:dd MMM yyyy}") +
                           $"   ·   {counted:#,##0} {(counted == 1 ? "cheque" : "cheques")}";

        FillTiles(from, to);
        FillMonths(from, to);
        FillOverview();
        FillPayees();
        FillCategories();
        FillUpcoming();
        FillCheques();
    }

    void FillTiles(DateTime? from, DateTime? to)
    {
        var s = Analytics.Summarize(_inPeriod, _today);
        var next30 = Analytics.Upcoming(_scope, _today, 30);
        var top = _payees.FirstOrDefault(p => p.Total > 0);

        var compare = "";
        if (from != null && to != null)
        {
            var (pf, pt) = Analytics.PreviousRange(from.Value, to.Value);
            var before = Analytics.Filter(Analytics.Counted(_scope), pf, pt).Where(r => r.Status.CountsAsPaid()).Sum(r => r.Amount);
            if (Analytics.Change(s.Total, before) is { } ch)
                compare = $"  ·  {(ch >= 0 ? "▲" : "▼")} {Math.Abs(ch):0%} vs before";
        }
        _tiles[0].Set(Aed(s.Total), $"{s.Cheques:#,##0} {(s.Cheques == 1 ? "cheque" : "cheques")}{compare}");
        _tiles[1].Set(Aed(s.Average), "Largest: " + Aed(s.Largest));
        _tiles[2].Set(top?.Payee ?? "-", top == null ? "No payments yet" : $"{Aed(top.Total)}  ·  {top.Share:0%} of total");
        _tiles[3].Set(Aed(s.UnclearedAmount), s.Uncleared == 0 ? "Nothing waiting" : $"{s.Uncleared} due, not marked Cleared",
            s.Uncleared > 0 ? Theme.Warning : null);
        _tiles[4].Set(Aed(s.BouncedAmount + s.StoppedAmount), $"{s.Bounced} bounced, {s.Stopped} stopped",
            s.Bounced + s.Stopped > 0 ? Theme.Danger : null);
        _tiles[5].Set(Aed(next30.Sum(r => r.Amount)), next30.Count == 0 ? "No post-dated cheques due" : $"{next30.Count} post-dated {(next30.Count == 1 ? "cheque" : "cheques")}");
    }

    void FillOverview()
    {
        var (from, to) = Range;
        _insights.SetItems(Analytics.Insights(_scope, from, to, _today));
        _ovMonth.SetBars(MonthBars(_months));
        _ovPayees.SetItems(_payees.Where(p => p.Total > 0).Take(12).Select(p => new HBarChart.Item(
            p.Payee, p.Total, "AED " + ChartBase.Short(p.Total),
            $"{p.Payee}\nAED {AmountInWords.Figures(p.Total)}  ·  {p.Share:0%} of total\n{p.Count} {(p.Count == 1 ? "cheque" : "cheques")}, last on {p.LastDate:dd MMM yyyy}")));
        _ovCats.SetSlices(CategorySlices());
        _ovStatus.SetRows(Analytics.ByStatus(_inPeriod, _today));
    }

    IEnumerable<BarChart.Bar> MonthBars(IEnumerable<MonthTotal> months)
    {
        var thisMonth = new DateTime(_today.Year, _today.Month, 1);
        return months.Select(m => new BarChart.Bar(m.ShortLabel, m.Total,
            $"{m.Label}\nAED {AmountInWords.Figures(m.Total)}\n{m.Count} {(m.Count == 1 ? "cheque" : "cheques")}" + (m.Start > thisMonth ? "  ·  post-dated" : ""),
            Muted: m.Start > thisMonth));
    }

    /// <summary>A category keeps its colour whatever the filters: colour by its place in the category list, not by size.</summary>
    Color CategoryColor(string category)
    {
        if (string.Equals(category, PayeeCategories.None, StringComparison.OrdinalIgnoreCase)) return ChartPalette.NeutralLight;
        var i = _categoryOrder.FindIndex(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i < ChartPalette.Series.Length ? ChartPalette.Series[i] : ChartPalette.Neutral;
    }

    List<DonutChart.Slice> CategorySlices()
    {
        var cats = Analytics.ByCategory(_inPeriod, _store.Data.CategoryOf);
        var slices = new List<DonutChart.Slice>();
        decimal more = 0; var moreCount = 0;
        foreach (var c in cats)
        {
            var color = CategoryColor(c.Category);
            if (color == ChartPalette.Neutral) { more += c.Total; moreCount += c.Count; continue; }   // past 8 colours: fold
            slices.Add(new DonutChart.Slice(c.Category, c.Total, color,
                $"{c.Category}\nAED {AmountInWords.Figures(c.Total)}  ·  {c.Share:0%}\n{c.Count} {(c.Count == 1 ? "cheque" : "cheques")}"));
        }
        if (more > 0)
            slices.Add(new DonutChart.Slice("More categories", more, ChartPalette.Neutral, $"More categories\nAED {AmountInWords.Figures(more)}\n{moreCount} cheques"));
        return slices;
    }

    void FillCategories()
    {
        var cats = Analytics.ByCategory(_inPeriod, _store.Data.CategoryOf);
        _catChart.SetSlices(CategorySlices());
        _catGrid.Rows.Clear();
        foreach (var c in cats) _catGrid.Rows.Add(c.Category, c.Count, c.Total, c.Share, c.Average);
    }

    void FillCheques()
    {
        var rows = _inPeriod.Where(r => _showRemoved.Checked || !r.ExcludedFromAnalytics)
            .OrderByDescending(r => r.ChequeDate).ThenByDescending(r => r.PrintedAt).ToList();
        _chequeGrid.Rows.Clear();
        foreach (var r in rows)
        {
            var i = _chequeGrid.Rows.Add(r.ChequeDate.Date, r.ChequeNo, r.Payee, _store.Data.CategoryOf(r.Payee), r.Amount,
                r.Status.ToString(), r.ExcludedFromAnalytics ? "Removed" : "Yes");
            _chequeGrid.Rows[i].Tag = r;
        }
        var removed = _inPeriod.Count(r => r.ExcludedFromAnalytics);
        _chequeTotal.Text = $"{rows.Count} {(rows.Count == 1 ? "cheque" : "cheques")}" + (removed > 0 ? $"   ·   {removed} removed from analytics" : "");
    }

    /// <summary>Removes the selected cheques from analytics, or puts them back. The register keeps them.</summary>
    void SetRemoved(bool removed)
    {
        var picked = _chequeGrid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<ChequeRecord>().ToList();
        if (picked.Count == 0)
        {
            MessageBox.Show(this, "Select one or more cheques in the list first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (removed && MessageBox.Show(this,
                $"Remove {picked.Count} cheque{(picked.Count == 1 ? "" : "s")} from analytics?\n\nThey stay in the cheque register, and you can put them back here at any time ('Show removed cheques').",
                Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
        foreach (var r in picked) r.ExcludedFromAnalytics = removed;
        Persist();
    }

    /// <summary>Sets the category of the selected payee (adds the payee to the payee list if needed).</summary>
    void SetPayeeCategory()
    {
        if (SelectedPayee is not { } name)
        {
            MessageBox.Show(this, "Select a payee in the list first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var f = new Form
        {
            Text = "Set category", Font = Font, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false,
            StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(380, 120), ShowIcon = false,
        };
        var combo = new ComboBox { Left = 12, Top = 36, Width = 356, DropDownStyle = ComboBoxStyle.DropDown };
        foreach (var c in PayeeCategories.All(_store.Data.Payees)) combo.Items.Add(c);
        var payee = _store.Data.FindPayee(name);
        combo.Text = payee?.Category ?? "";
        var ok = new System.Windows.Forms.Button { Text = "OK", Left = 212, Top = 78, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new System.Windows.Forms.Button { Text = "Cancel", Left = 293, Top = 78, Width = 75, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { new Label { Text = $"Category for {name}:", Left = 12, Top = 12, AutoSize = true }, combo, ok, cancel });
        f.AcceptButton = ok; f.CancelButton = cancel;
        if (f.ShowDialog(this) != DialogResult.OK) return;
        if (payee == null)
        {
            payee = new Payee { Name = name };
            _store.Data.Payees.Add(payee);
        }
        payee.Category = combo.Text.Trim();
        _categoryOrder = PayeeCategories.All(_store.Data.Payees);
        if (payee.Category.Length > 0 && !_category.Items.Cast<object>().Any(i => string.Equals(i as string, payee.Category, StringComparison.OrdinalIgnoreCase)))
            _category.Items.Insert(_category.Items.Count - 1, payee.Category);
        Persist();
    }

    void Persist()
    {
        try { _store.Save(); }
        catch (Exception ex) { MessageBox.Show(this, "Could not save:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        Reload();
    }

    /// <summary>Whole dirhams for the tiles; the exact amounts are in the charts' hover cards and the tables.</summary>
    static string Aed(decimal v) => "AED " + Math.Round(v, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture);

    void FillMonths(DateTime? from, DateTime? to)
    {
        _months = Analytics.ByMonth(_inPeriod, from, to);
        var thisMonth = new DateTime(_today.Year, _today.Month, 1);
        _monthChart.SetBars(MonthBars(_months));

        _monthGrid.Rows.Clear();
        MonthTotal? prev = null;
        foreach (var m in _months)
        {
            string change = prev == null || prev.Total == 0 ? "" : $"{(m.Total - prev.Total) / prev.Total:+0%;-0%;0%}";
            var i = _monthGrid.Rows.Add(m.Label + (m.Start > thisMonth ? "  (post-dated)" : ""), m.Count, m.Total, m.Average, change);
            _monthGrid.Rows[i].Tag = m.Start;
            prev = m;
        }
        _monthGrid.Sort(new RowTagDescending());   // newest month on top in the table
    }

    void SelectMonth(DateTime start)
    {
        foreach (DataGridViewRow r in _monthGrid.Rows)
            if (r.Tag is DateTime d && d == start)
            {
                _monthGrid.CurrentCell = r.Cells[0];
                r.Selected = true;
                break;
            }
    }

    sealed class RowTagDescending : System.Collections.IComparer
    {
        public int Compare(object? x, object? y) =>
            Comparer<DateTime>.Default.Compare((DateTime)((DataGridViewRow)y!).Tag!, (DateTime)((DataGridViewRow)x!).Tag!);
    }

    void FillPayees()
    {
        var s = _payeeSearch.Text.Trim();
        var keep = SelectedPayee;
        _payeeGrid.Rows.Clear();
        foreach (var p in _payees.Where(p => s.Length == 0 || p.Payee.Contains(s, StringComparison.OrdinalIgnoreCase)))
        {
            var i = _payeeGrid.Rows.Add(p.Payee, _store.Data.CategoryOf(p.Payee), p.Count, p.Total, p.Share, p.Average, p.Largest, p.LastDate, p.Bounced);
            if (p.Bounced > 0) _payeeGrid.Rows[i].Cells[8].Style.ForeColor = Theme.Danger;
            if (keep != null && Analytics.SamePayee(p.Payee, keep)) _payeeGrid.CurrentCell = _payeeGrid.Rows[i].Cells[0];
        }
        FillPayeeChart();
    }

    void SelectPayee(string name)
    {
        if (_payeeSearch.Text.Length > 0) _payeeSearch.Text = "";
        foreach (DataGridViewRow r in _payeeGrid.Rows)
            if (r.Cells[0].Value is string n && Analytics.SamePayee(n, name))
            {
                _payeeGrid.CurrentCell = r.Cells[0];
                r.Selected = true;
                break;
            }
        FillPayeeChart();
    }

    string? SelectedPayee => _payeeGrid.CurrentRow is { Index: >= 0 } r ? r.Cells[0].Value as string : null;

    void FillPayeeChart()
    {
        if (SelectedPayee is not { } name)
        {
            _payeeChartTitle.Text = "Monthly payments";
            _payeeChart.SetBars(Array.Empty<BarChart.Bar>());
            return;
        }
        var (from, to) = Range;
        var rows = _inPeriod.Where(r => Analytics.SamePayee(r.Payee, name)).ToList();
        _payeeChartTitle.Text = $"Monthly payments to {name}";
        _payeeChart.SetBars(MonthBars(Analytics.ByMonth(rows, from, to)));
    }

    void ShowPayeeCheques()
    {
        if (SelectedPayee is not { } name) return;
        OpenRegister(name);
    }

    void OpenRegister(string search)
    {
        using (var f = new RegisterForm(_store, _main, search)) f.ShowDialog(this);
        Reload();   // statuses may have changed
    }

    void FillUpcoming()
    {
        var days = UpcomingRanges[Math.Max(0, _upcomingRange.SelectedIndex)].Days;
        var rows = Analytics.Upcoming(_scope, _today, days);
        _upcomingGrid.Rows.Clear();
        foreach (var r in rows)
            _upcomingGrid.Rows.Add(r.ChequeDate.Date, (r.ChequeDate.Date - _today).Days, r.ChequeNo, r.Payee, r.Amount, r.BankName, r.Notes);
        _upcomingTotal.Text = $"{rows.Count} {(rows.Count == 1 ? "cheque" : "cheques")}   ·   Total AED {AmountInWords.Figures(rows.Sum(r => r.Amount))}";
        _upcomingChart.SetBars(Analytics.ByMonth(rows).Select(m => new BarChart.Bar(m.ShortLabel, m.Total,
            $"{m.Label}\nAED {AmountInWords.Figures(m.Total)} falls due\n{m.Count} {(m.Count == 1 ? "cheque" : "cheques")}")));
    }

    // ---------------- export ----------------

    void Export()
    {
        var view = Math.Max(0, _views.SelectedIndex);
        var grid = view switch { 2 => _payeeGrid, 3 => _catGrid, 4 => _upcomingGrid, 5 => _chequeGrid, _ => _monthGrid };
        var name = view switch { 2 => "by-payee", 3 => "by-category", 4 => "upcoming-pdc", 5 => "cheques", _ => "by-month" };
        using var dlg = new SaveFileDialog { Filter = "CSV for Excel (*.csv)|*.csv", FileName = $"RAKSpecial-{name}-{DateTime.Now:yyyyMMdd}.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var headers = grid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText);
            var rows = grid.Rows.Cast<DataGridViewRow>().Select(r => r.Cells.Cast<DataGridViewCell>().Select(CellText));
            File.WriteAllText(dlg.FileName, CsvExport.Table(headers, rows), new UTF8Encoding(true));
            MessageBox.Show(this, $"Exported {grid.Rows.Count} rows ({ViewNames[view == 0 ? 1 : view]}).", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    /// <summary>Plain numbers for Excel (no thousands separators), dates as dd-MM-yyyy.</summary>
    static string CellText(DataGridViewCell c) => c.Value switch
    {
        decimal d when c.OwningColumn.DefaultCellStyle.Format.Contains('%') => (d * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%",
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        int n => n.ToString(CultureInfo.InvariantCulture),
        DateTime t => t.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
        null => "",
        var v => v.ToString() ?? "",
    };
}
