using System.Globalization;
using System.Text;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>
/// Where the money went: totals by month and by payee, cheques awaiting clearing, bounced cheques
/// and post-dated cheques still to come. Everything is worked out from the cheque register.
/// </summary>
public class AnalyticsForm : Form
{
    readonly DataStore _store;
    readonly MainForm _main;
    readonly DateTime _today = DateTime.Today;

    readonly ComboBox _period = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Width = 115 };
    readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Width = 115 };
    readonly ComboBox _book = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    readonly FlowLayoutPanel _cards = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 0, 8, 6) };
    readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    readonly BarChart _monthChart = new() { Dock = DockStyle.Fill };
    readonly DataGridView _monthGrid = NewGrid();
    readonly TextBox _payeeSearch = new() { Width = 240, PlaceholderText = "Search payee" };
    readonly DataGridView _payeeGrid = NewGrid();
    readonly Label _payeeChartTitle = new() { Dock = DockStyle.Top, Height = 24, Padding = new Padding(4, 4, 0, 0), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    readonly BarChart _payeeChart = new() { Dock = DockStyle.Fill, EmptyText = "Select a payee to see their monthly payments." };
    readonly ComboBox _upcomingRange = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    readonly DataGridView _upcomingGrid = NewGrid();
    readonly Label _upcomingTotal = new() { AutoSize = true, Margin = new Padding(12, 7, 3, 3), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    readonly BarChart _catChart = new() { Dock = DockStyle.Fill, EmptyText = "No cheques in this period." };
    readonly DataGridView _catGrid = NewGrid();
    readonly DataGridView _chequeGrid = NewGrid();
    readonly CheckBox _showRemoved = new() { Text = "Show removed cheques", AutoSize = true, Margin = new Padding(16, 6, 3, 3) };
    readonly Label _chequeTotal = new() { AutoSize = true, Margin = new Padding(12, 7, 3, 3), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    const string AllCategories = "All categories";

    static readonly (string Text, AnalyticsPeriod Period)[] Periods =
    {
        ("This month", AnalyticsPeriod.ThisMonth), ("Last month", AnalyticsPeriod.LastMonth),
        ("Last 3 months", AnalyticsPeriod.Last3Months), ("Last 12 months", AnalyticsPeriod.Last12Months),
        ("This year", AnalyticsPeriod.ThisYear), ("Last year", AnalyticsPeriod.LastYear),
        ("All time", AnalyticsPeriod.AllTime), ("Custom dates", AnalyticsPeriod.Custom),
    };
    static readonly (string Text, int? Days)[] UpcomingRanges =
        { ("Next 30 days", 30), ("Next 90 days", 90), ("Next 12 months", 365), ("All post-dated", null) };

    readonly ToolTip _cardTip = new();
    readonly List<(SplitContainer Split, double Top)> _splits = new();

    List<ChequeRecord> _inPeriod = new();
    List<ChequeRecord> _periodAll = new();   // in period, including removed cheques
    Font? _removedFont;
    List<PayeeTotal> _payees = new();
    bool _loading = true;

    public AnalyticsForm(DataStore store, MainForm main)
    {
        _store = store; _main = main;
        Text = "Analytics - where the money went";
        Font = new Font("Segoe UI", 9.5f);
        Size = new Size(1180, 760);
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterParent;
        AppIcon.Apply(this);

        // ---- filters ----
        foreach (var p in Periods) _period.Items.Add(p.Text);
        _period.SelectedIndex = 3;   // last 12 months
        _book.Items.Add("All cheque books");
        foreach (var b in _store.Data.Banks) _book.Items.Add(b);
        _book.SelectedIndex = 0;
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 8, 8, 6) };
        Label L(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(8, 7, 3, 3) };
        _category.Items.Add(AllCategories);
        foreach (var c in PayeeCategories.All(_store.Data.Payees)) _category.Items.Add(c);
        _category.Items.Add(PayeeCategories.None);
        _category.SelectedIndex = 0;
        filters.Controls.AddRange(new Control[] { L("Period"), _period, L("From"), _from, L("To"), _to, L("Cheque book"), _book, L("Category"), _category });

        // ---- tabs ----
        _tabs.TabPages.Add(MonthlyTab());
        _tabs.TabPages.Add(PayeesTab());
        _tabs.TabPages.Add(CategoryTab());
        _tabs.TabPages.Add(UpcomingTab());
        _tabs.TabPages.Add(ChequesTab());

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var close = Btn("Close", (_, _) => Close());
        bottom.Controls.Add(close);
        bottom.Controls.Add(Btn("Export this tab to Excel (CSV)...", (_, _) => Export()));
        bottom.Controls.Add(new Label
        {
            AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 7, 20, 3),
            Text = "Totals count Printed and Cleared cheques by cheque date. Bounced, stopped and void are left out.",
        });
        CancelButton = close;

        Controls.Add(_tabs);
        Controls.Add(_cards);
        Controls.Add(filters);
        Controls.Add(bottom);

        _period.SelectedIndexChanged += (_, _) => { ApplyPeriod(); Reload(); };
        _from.ValueChanged += (_, _) => { if (!_loading) Reload(); };
        _to.ValueChanged += (_, _) => { if (!_loading) Reload(); };
        _book.SelectedIndexChanged += (_, _) => Reload();
        _category.SelectedIndexChanged += (_, _) => { if (!_loading) Reload(); };
        _showRemoved.CheckedChanged += (_, _) => FillCheques();
        _payeeSearch.TextChanged += (_, _) => FillPayees();
        _upcomingRange.SelectedIndexChanged += (_, _) => FillUpcoming();

        ApplyPeriod();
        _loading = false;
        Reload();
    }

    // ---------------- layout ----------------

    TabPage MonthlyTab()
    {
        Col(_monthGrid, "Month", 140);
        Col(_monthGrid, "Cheques", 80, "#,##0", right: true);
        Col(_monthGrid, "Total (AED)", 120, "#,##0.00", right: true);
        Col(_monthGrid, "Average (AED)", 120, "#,##0.00", right: true);
        Col(_monthGrid, "Change vs previous month", 140, right: true);
        var split = NewSplit(0.5);
        split.Panel1.Controls.Add(_monthChart);
        split.Panel1.Padding = new Padding(4);
        split.Panel2.Controls.Add(_monthGrid);
        var page = new TabPage("By month");
        page.Controls.Add(split);
        return page;
    }

    TabPage PayeesTab()
    {
        Col(_payeeGrid, "Payee", 240);
        Col(_payeeGrid, "Category", 100);
        Col(_payeeGrid, "Cheques", 70, "#,##0", right: true);
        Col(_payeeGrid, "Total (AED)", 110, "#,##0.00", right: true);
        Col(_payeeGrid, "Share", 70, "0.0%", right: true);
        Col(_payeeGrid, "Average (AED)", 105, "#,##0.00", right: true);
        Col(_payeeGrid, "Largest (AED)", 105, "#,##0.00", right: true);
        Col(_payeeGrid, "Last cheque", 95, "dd-MM-yyyy");
        Col(_payeeGrid, "Bounced", 70, "#,##0", right: true);
        _payeeGrid.SelectionChanged += (_, _) => FillPayeeChart();
        _payeeGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ShowPayeeCheques(); };

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 6, 4, 4) };
        top.Controls.Add(_payeeSearch);
        top.Controls.Add(Btn("Show this payee's cheques", (_, _) => ShowPayeeCheques()));
        top.Controls.Add(Btn("Set category...", (_, _) => SetPayeeCategory()));

        var split = NewSplit(0.55);
        split.Panel1.Controls.Add(_payeeGrid);
        split.Panel2.Controls.Add(_payeeChart);
        split.Panel2.Controls.Add(_payeeChartTitle);
        var page = new TabPage("By payee");
        page.Controls.Add(split);
        page.Controls.Add(top);
        return page;
    }

    TabPage CategoryTab()
    {
        Col(_catGrid, "Category", 180);
        Col(_catGrid, "Cheques", 80, "#,##0", right: true);
        Col(_catGrid, "Total (AED)", 120, "#,##0.00", right: true);
        Col(_catGrid, "Share", 80, "0.0%", right: true);
        Col(_catGrid, "Average (AED)", 120, "#,##0.00", right: true);
        var split = NewSplit(0.5);
        split.Panel1.Controls.Add(_catChart);
        split.Panel1.Padding = new Padding(4);
        split.Panel2.Controls.Add(_catGrid);
        var note = new Label
        {
            Dock = DockStyle.Bottom, Height = 26, ForeColor = Color.DimGray, Padding = new Padding(4, 6, 0, 0),
            Text = "Set each payee's category in Payees, or with 'Set category...' on the By payee tab.",
        };
        var page = new TabPage("By category");
        page.Controls.Add(split);
        page.Controls.Add(note);
        return page;
    }

    TabPage ChequesTab()
    {
        Col(_chequeGrid, "Cheque date", 95, "dd-MM-yyyy");
        Col(_chequeGrid, "Cheque No.", 85);
        Col(_chequeGrid, "Payee", 240);
        Col(_chequeGrid, "Category", 110);
        Col(_chequeGrid, "Amount (AED)", 110, "#,##0.00", right: true);
        Col(_chequeGrid, "Status", 80);
        Col(_chequeGrid, "In analytics", 80);
        _chequeGrid.MultiSelect = true;
        _chequeGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _chequeGrid.Rows[e.RowIndex].Tag is ChequeRecord { ExcludedFromAnalytics: true })
            {
                e.CellStyle!.ForeColor = Color.Gray;
                e.CellStyle.Font = _removedFont ??= new Font(_chequeGrid.Font, FontStyle.Strikeout);
            }
        };

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 6, 4, 4) };
        top.Controls.Add(Btn("Remove from analytics", (_, _) => SetRemoved(true)));
        top.Controls.Add(Btn("Put back", (_, _) => SetRemoved(false)));
        top.Controls.Add(_showRemoved);
        top.Controls.Add(_chequeTotal);
        var note = new Label
        {
            Dock = DockStyle.Bottom, Height = 26, ForeColor = Color.DimGray, Padding = new Padding(4, 6, 0, 0),
            Text = "Removed cheques (e.g. test prints) stay in the cheque register but are left out of every total and chart.",
        };
        var page = new TabPage("Cheques");
        page.Controls.Add(_chequeGrid);
        page.Controls.Add(top);
        page.Controls.Add(note);
        return page;
    }

    TabPage UpcomingTab()
    {
        Col(_upcomingGrid, "Cheque date", 95, "dd-MM-yyyy");
        Col(_upcomingGrid, "Days to go", 75, "#,##0", right: true);
        Col(_upcomingGrid, "Cheque No.", 85);
        Col(_upcomingGrid, "Payee", 260);
        Col(_upcomingGrid, "Amount (AED)", 110, "#,##0.00", right: true);
        Col(_upcomingGrid, "Cheque book", 140);
        Col(_upcomingGrid, "Notes", 160);
        foreach (var r in UpcomingRanges) _upcomingRange.Items.Add(r.Text);
        _upcomingRange.SelectedIndex = 0;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 6, 4, 4) };
        top.Controls.Add(_upcomingRange);
        top.Controls.Add(_upcomingTotal);
        var note = new Label
        {
            Dock = DockStyle.Bottom, Height = 26, ForeColor = Color.DimGray, Padding = new Padding(4, 6, 0, 0),
            Text = "Post-dated cheques you have given out that are not due yet (status Printed). Keep this much in the account by each date.",
        };
        var page = new TabPage("Upcoming post-dated cheques");
        page.Controls.Add(_upcomingGrid);
        page.Controls.Add(top);
        page.Controls.Add(note);
        return page;
    }

    /// <summary>Top/bottom split; the share for the top part is applied once the window has its size.</summary>
    SplitContainer NewSplit(double top)
    {
        var s = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        _splits.Add((s, top));
        return s;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        foreach (var (s, top) in _splits)
            s.SplitterDistance = Math.Clamp((int)(s.Height * top), s.Panel1MinSize, Math.Max(s.Panel1MinSize, s.Height - s.Panel2MinSize - s.SplitterWidth));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _cardTip.Dispose();
        base.Dispose(disposing);
    }

    static DataGridView NewGrid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        RowHeadersVisible = false, BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.None,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
    };

    static void Col(DataGridView g, string header, int weight, string? format = null, bool right = false)
    {
        var c = new DataGridViewTextBoxColumn { HeaderText = header, FillWeight = weight, SortMode = DataGridViewColumnSortMode.Automatic };
        if (format != null) c.DefaultCellStyle.Format = format;
        if (right) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        g.Columns.Add(c);
    }

    static Button Btn(string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true, Padding = new Padding(8, 2, 8, 2), Margin = new Padding(8, 3, 3, 3) };
        b.Click += click;
        return b;
    }

    // ---------------- data ----------------

    AnalyticsPeriod SelectedPeriod => Periods[Math.Max(0, _period.SelectedIndex)].Period;
    string? BookId => _book.SelectedItem is BankLayout b ? b.Id : null;

    void ApplyPeriod()
    {
        var custom = SelectedPeriod == AnalyticsPeriod.Custom;
        _from.Enabled = _to.Enabled = custom;
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
        var scope = _store.Data.Cheques.Where(InScope).ToList();
        _periodAll = Analytics.Filter(scope, from, to).ToList();
        _inPeriod = _periodAll;           // the Analytics functions leave out removed cheques themselves
        _payees = Analytics.ByPayee(_inPeriod);

        FillCards(Analytics.Summarize(_inPeriod, _today), Analytics.Upcoming(scope, _today, 30));
        FillMonths(from, to);
        FillPayees();
        FillCategories();
        FillUpcoming();
        FillCheques();
    }

    void FillCategories()
    {
        var cats = Analytics.ByCategory(_inPeriod, _store.Data.CategoryOf);
        _catChart.SetBars(cats.Select(c => new BarChart.Bar(c.Category, c.Total,
            $"{c.Category}\nAED {AmountInWords.Figures(c.Total)}\n{c.Count} cheques · {c.Share:0%} of total")));
        _catGrid.Rows.Clear();
        foreach (var c in cats) _catGrid.Rows.Add(c.Category, c.Count, c.Total, c.Share, c.Average);
    }

    void FillCheques()
    {
        var rows = _periodAll.Where(r => _showRemoved.Checked || !r.ExcludedFromAnalytics)
            .OrderByDescending(r => r.ChequeDate).ThenByDescending(r => r.PrintedAt).ToList();
        _chequeGrid.Rows.Clear();
        foreach (var r in rows)
        {
            var i = _chequeGrid.Rows.Add(r.ChequeDate.Date, r.ChequeNo, r.Payee, _store.Data.CategoryOf(r.Payee), r.Amount,
                r.Status.ToString(), r.ExcludedFromAnalytics ? "Removed" : "Yes");
            _chequeGrid.Rows[i].Tag = r;
        }
        var removed = _periodAll.Count(r => r.ExcludedFromAnalytics);
        _chequeTotal.Text = $"{rows.Count} cheques" + (removed > 0 ? $"  |  {removed} removed from analytics" : "");
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
        if (SelectedPayee is not { } name) return;
        using var f = new Form
        {
            Text = "Set category", Font = Font, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false,
            StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(380, 120), ShowIcon = false,
        };
        var combo = new ComboBox { Left = 12, Top = 36, Width = 356, DropDownStyle = ComboBoxStyle.DropDown };
        foreach (var c in PayeeCategories.All(_store.Data.Payees)) combo.Items.Add(c);
        var payee = _store.Data.FindPayee(name);
        combo.Text = payee?.Category ?? "";
        var ok = new Button { Text = "OK", Left = 212, Top = 78, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 293, Top = 78, Width = 75, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { new Label { Text = $"Category for {name}:", Left = 12, Top = 12, AutoSize = true }, combo, ok, cancel });
        f.AcceptButton = ok; f.CancelButton = cancel;
        if (f.ShowDialog(this) != DialogResult.OK) return;
        if (payee == null)
        {
            payee = new Payee { Name = name };
            _store.Data.Payees.Add(payee);
        }
        payee.Category = combo.Text.Trim();
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

    void FillCards(AnalyticsSummary s, List<ChequeRecord> next30)
    {
        _cards.SuspendLayout();
        _cards.Controls.Clear();
        var top = _payees.FirstOrDefault(p => p.Total > 0);
        var due = next30.Sum(r => r.Amount);
        AddCard("Total paid", Aed(s.Total), $"{s.Cheques:#,##0} cheques", Exact(s.Total));
        AddCard("Average cheque", Aed(s.Average), "Largest: " + Aed(s.Largest), Exact(s.Average));
        AddCard("Top payee", top?.Payee ?? "-", top == null ? "" : $"{Aed(top.Total)} · {top.Share:0%} of total", top?.Payee ?? "");
        AddCard("Awaiting clearing", Aed(s.UnclearedAmount), $"{s.Uncleared} due, not cleared", Exact(s.UnclearedAmount),
            s.Uncleared > 0 ? Color.FromArgb(180, 83, 9) : null);
        AddCard("Bounced / stopped", Aed(s.BouncedAmount + s.StoppedAmount), $"{s.Bounced} bounced, {s.Stopped} stopped",
            Exact(s.BouncedAmount + s.StoppedAmount), s.Bounced + s.Stopped > 0 ? Color.FromArgb(185, 28, 28) : null);
        AddCard("Due in next 30 days", Aed(due), $"{next30.Count} post-dated cheques", Exact(due));
        _cards.ResumeLayout();
    }

    /// <summary>Whole dirhams for the cards; the exact amount is in the tooltip and the tables.</summary>
    static string Aed(decimal v) => "AED " + Math.Round(v, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture);
    static string Exact(decimal v) => "AED " + AmountInWords.Figures(v);

    void AddCard(string title, string value, string sub, string tip, Color? accent = null)
    {
        var card = new TableLayoutPanel
        {
            Width = 176, Height = 76, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(10, 6, 6, 6),
            BackColor = Color.FromArgb(246, 247, 249), ColumnCount = 1, RowCount = 3,
        };
        card.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8.5f) });
        var big = new Label { Text = value, AutoSize = false, Width = 160, Height = 26, AutoEllipsis = true, Font = new Font("Segoe UI", 12.5f, FontStyle.Bold), ForeColor = accent ?? Color.FromArgb(30, 41, 59) };
        card.Controls.Add(big);
        card.Controls.Add(new Label { Text = sub, AutoSize = false, Width = 160, Height = 18, AutoEllipsis = true, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8.5f) });
        _cardTip.SetToolTip(big, tip);
        _cards.Controls.Add(card);
    }

    void FillMonths(DateTime? from, DateTime? to)
    {
        var months = Analytics.ByMonth(_inPeriod, from, to);
        var thisMonth = new DateTime(_today.Year, _today.Month, 1);
        _monthChart.SetBars(months.Select(m => new BarChart.Bar(m.ShortLabel, m.Total,
            $"{m.Label}\nAED {AmountInWords.Figures(m.Total)}\n{m.Count} cheques" + (m.Start > thisMonth ? "\n(post-dated)" : ""),
            Muted: m.Start > thisMonth)));

        _monthGrid.Rows.Clear();
        MonthTotal? prev = null;
        foreach (var m in months)
        {
            string change = prev == null || prev.Total == 0 ? "" : $"{(m.Total - prev.Total) / prev.Total:+0%;-0%;0%}";
            var i = _monthGrid.Rows.Add(m.Label + (m.Start > thisMonth ? "  (post-dated)" : ""), m.Count, m.Total, m.Average, change);
            _monthGrid.Rows[i].Tag = m.Start;
            prev = m;
        }
        // newest month on top in the table
        _monthGrid.Sort(new RowTagDescending());
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
            if (p.Bounced > 0) _payeeGrid.Rows[i].Cells[8].Style.ForeColor = Color.FromArgb(185, 28, 28);
            if (keep != null && Analytics.SamePayee(p.Payee, keep)) _payeeGrid.CurrentCell = _payeeGrid.Rows[i].Cells[0];
        }
        FillPayeeChart();
    }

    string? SelectedPayee => _payeeGrid.CurrentRow is { Index: >= 0 } r ? r.Cells[0].Value as string : null;

    void FillPayeeChart()
    {
        if (SelectedPayee is not { } name)
        {
            _payeeChartTitle.Text = "";
            _payeeChart.SetBars(Array.Empty<BarChart.Bar>());
            return;
        }
        var (from, to) = Range;
        var rows = _inPeriod.Where(r => Analytics.SamePayee(r.Payee, name)).ToList();
        var months = Analytics.ByMonth(rows, from, to);
        _payeeChartTitle.Text = $"Monthly payments to {name}";
        var thisMonth = new DateTime(_today.Year, _today.Month, 1);
        _payeeChart.SetBars(months.Select(m => new BarChart.Bar(m.ShortLabel, m.Total,
            $"{name}\n{m.Label}: AED {AmountInWords.Figures(m.Total)} ({m.Count} cheques)", Muted: m.Start > thisMonth)));
    }

    void ShowPayeeCheques()
    {
        if (SelectedPayee is not { } name) return;
        using var f = new RegisterForm(_store, _main, name);
        f.ShowDialog(this);
        Reload();   // statuses may have changed
    }

    void FillUpcoming()
    {
        var days = UpcomingRanges[Math.Max(0, _upcomingRange.SelectedIndex)].Days;
        var rows = Analytics.Upcoming(_store.Data.Cheques.Where(InScope), _today, days);
        _upcomingGrid.Rows.Clear();
        foreach (var r in rows)
            _upcomingGrid.Rows.Add(r.ChequeDate.Date, (r.ChequeDate.Date - _today).Days, r.ChequeNo, r.Payee, r.Amount, r.BankName, r.Notes);
        _upcomingTotal.Text = $"{rows.Count} cheques  |  Total: AED {AmountInWords.Figures(rows.Sum(r => r.Amount))}";
    }

    // ---------------- export ----------------

    void Export()
    {
        var grid = _tabs.SelectedIndex switch { 0 => _monthGrid, 1 => _payeeGrid, 2 => _catGrid, 3 => _upcomingGrid, _ => _chequeGrid };
        var name = _tabs.SelectedIndex switch { 0 => "by-month", 1 => "by-payee", 2 => "by-category", 3 => "upcoming-pdc", _ => "cheques" };
        using var dlg = new SaveFileDialog { Filter = "CSV for Excel (*.csv)|*.csv", FileName = $"RAKSpecial-{name}-{DateTime.Now:yyyyMMdd}.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var headers = grid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText);
            var rows = grid.Rows.Cast<DataGridViewRow>().Select(r => r.Cells.Cast<DataGridViewCell>().Select(CellText));
            File.WriteAllText(dlg.FileName, CsvExport.Table(headers, rows), new UTF8Encoding(true));
            MessageBox.Show(this, $"Exported {grid.Rows.Count} rows.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
