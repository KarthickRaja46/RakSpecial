using System.Drawing.Printing;
using System.Globalization;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>
/// Post-dated cheques (rent, instalments): enter the series once, check the list, then print
/// the cheques one after another. Each printed cheque goes into the register.
/// </summary>
public class PdcForm : Form
{
    readonly DataStore _store;
    readonly BankLayout _bank;

    readonly ComboBox _payee = new() { Width = 360, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    readonly TextBox _amount = new() { Width = 140, TextAlign = HorizontalAlignment.Right };
    readonly TextBox _firstNo = new() { Width = 140 };
    readonly DateTimePicker _firstDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Width = 140 };
    readonly NumericUpDown _count = new() { Minimum = 1, Maximum = PdcSchedule.MaxCheques, Value = 12, Width = 70 };
    readonly NumericUpDown _every = new() { Minimum = 1, Maximum = 12, Value = 1, Width = 70 };
    readonly ComboBox _weekend = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox _acPayee = new() { Text = "A/C Payee Only", AutoSize = true, Checked = true };
    readonly CheckBox _bearer = new() { Text = "Cross out \"or Bearer\"", AutoSize = true, Checked = true };
    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
    };
    readonly Label _summary = new() { Dock = DockStyle.Bottom, Height = 26, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

    IReadOnlyList<PdcItem> _items = Array.Empty<PdcItem>();
    readonly HashSet<int> _printed = new();

    public PdcForm(DataStore store, BankLayout bank, string payee, decimal amount, string firstNo, DateTime firstDate)
    {
        _store = store; _bank = bank;
        Text = $"PDC series - {bank.Name}";
        Font = new Font("Segoe UI", 9.75f);
        Size = new Size(860, 640);
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.CenterParent;
        AppIcon.Apply(this);

        foreach (var p in store.Data.Payees.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)) _payee.Items.Add(p.Name);
        _payee.Text = payee;
        _amount.Text = amount > 0 ? AmountInWords.Figures(amount) : "";
        _firstNo.Text = firstNo.Length > 0 ? firstNo : bank.NextChequeNo;
        _firstDate.Value = firstDate;
        _weekend.Items.AddRange(new object[] { "Keep the date", "Saturday/Sunday -> next Monday", "Saturday/Sunday -> Friday before" });
        _weekend.SelectedIndex = 0;

        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(10, 8, 10, 4) };
        Label L(string t) => new() { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 8, 3) };
        form.Controls.Add(L("Pay to"), 0, 0); form.Controls.Add(_payee, 1, 0); form.SetColumnSpan(_payee, 3);
        form.Controls.Add(L("Amount each (AED)"), 0, 1); form.Controls.Add(_amount, 1, 1);
        var checks = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        checks.Controls.Add(_acPayee); checks.Controls.Add(_bearer);
        form.Controls.Add(checks, 2, 1); form.SetColumnSpan(checks, 2);
        form.Controls.Add(L("First cheque No."), 0, 2); form.Controls.Add(_firstNo, 1, 2);
        form.Controls.Add(L("First date"), 2, 2); form.Controls.Add(_firstDate, 3, 2);
        form.Controls.Add(L("Number of cheques"), 0, 3); form.Controls.Add(_count, 1, 3);
        var every = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        every.Controls.Add(L("Every")); every.Controls.Add(_every); every.Controls.Add(L("month(s)"));
        form.Controls.Add(every, 2, 3); form.SetColumnSpan(every, 2);
        form.Controls.Add(L("Weekend dates"), 0, 4); form.Controls.Add(_weekend, 1, 4); form.SetColumnSpan(_weekend, 3);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        Button B(string t, EventHandler h, bool primary = false)
        {
            var b = new Button { Text = t, AutoSize = true, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 0, 8, 0) };
            if (primary) { b.BackColor = Color.FromArgb(0, 102, 204); b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat; }
            b.Click += h; return b;
        }
        bar.Controls.Add(B("Print all, one by one", (_, _) => PrintAll(), true));
        bar.Controls.Add(B("Preview all", (_, _) => PreviewAll()));
        bar.Controls.Add(B("Close", (_, _) => Close()));

        var listBox = new GroupBox { Text = "Cheques in this series", Dock = DockStyle.Fill, Padding = new Padding(8) };
        listBox.Controls.Add(_grid);

        Controls.Add(listBox);
        Controls.Add(_summary);
        Controls.Add(bar);
        Controls.Add(form);
        listBox.BringToFront();

        _grid.Columns.Add("n", "#"); _grid.Columns.Add("no", "Cheque No."); _grid.Columns.Add("date", "Date");
        _grid.Columns.Add("day", "Day"); _grid.Columns.Add("amt", "Amount (AED)"); _grid.Columns.Add("st", "Status");
        _grid.Columns[0].FillWeight = 25; _grid.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        foreach (Control c in new Control[] { _payee, _amount, _firstNo, _firstDate, _count, _every, _weekend })
            c.TextChanged += (_, _) => Rebuild();
        _count.ValueChanged += (_, _) => Rebuild();
        _every.ValueChanged += (_, _) => Rebuild();
        _firstDate.ValueChanged += (_, _) => Rebuild();
        _weekend.SelectedIndexChanged += (_, _) => Rebuild();
        _amount.Leave += (_, _) => { if (TryAmount(out var a)) _amount.Text = AmountInWords.Figures(a); };
        Rebuild();
    }

    bool TryAmount(out decimal amount)
    {
        var t = _amount.Text.Trim().Replace("AED", "", StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    void Rebuild()
    {
        TryAmount(out var amount);
        _printed.Clear();
        try
        {
            _items = PdcSchedule.Build(_firstDate.Value.Date, (int)_count.Value, (int)_every.Value,
                _firstNo.Text, Math.Max(0, amount), (WeekendRule)Math.Max(0, _weekend.SelectedIndex));
        }
        catch (ArgumentOutOfRangeException) { _items = Array.Empty<PdcItem>(); }

        _grid.Rows.Clear();
        foreach (var it in _items)
        {
            var row = _grid.Rows.Add(it.Index, it.ChequeNo, it.Date.ToString("dd-MM-yyyy"), it.Date.ToString("ddd"),
                AmountInWords.Figures(it.Amount), "");
            if (PdcSchedule.IsWeekend(it.Date)) _grid.Rows[row].DefaultCellStyle.ForeColor = Color.Firebrick;
        }
        var total = _items.Sum(i => i.Amount);
        var weekend = _items.Count(i => PdcSchedule.IsWeekend(i.Date));
        _summary.Text = _items.Count == 0 ? "" :
            $"{_items.Count} cheques, {_items[0].Date:dd-MM-yyyy} to {_items[^1].Date:dd-MM-yyyy}, total AED {AmountInWords.Figures(total)}" +
            (weekend > 0 ? $"   |   {weekend} on a weekend (shown in red)" : "");
    }

    ChequeData DataFor(PdcItem it) => new()
    {
        ChequeNo = it.ChequeNo, Date = it.Date, Payee = _payee.Text, Amount = it.Amount,
        AcPayeeOnly = _acPayee.Checked, StrikeBearer = _bearer.Checked,
    };

    bool Validate(out string problem)
    {
        problem = "";
        if (_payee.Text.Trim().Length == 0) problem = "Please enter who the cheques are paid to.";
        else if (!TryAmount(out var a) || a <= 0) problem = "Please enter an amount greater than zero.";
        else if (decimal.Round(a, 2) != a) problem = "Amount can have at most 2 decimals (fils).";
        else if (a > AmountInWords.MaxAmount) problem = "Amount is too large.";
        else if (_items.Count == 0) problem = "The series is empty.";
        else if (ChequeRenderer.WrapWords(_bank, ChequeText.Build(DataFor(_items[0]), _bank, _store.Data.Settings).Words).Overflow)
            problem = "Amount in words does not fit on the cheque lines.";
        return problem.Length == 0;
    }

    void PreviewAll()
    {
        if (!Validate(out var problem)) { MessageBox.Show(this, problem, "PDC series", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        using var doc = ChequePrinter.CreateSeries(_bank, _items.Select(DataFor).ToList(), _store.Data.Settings, false, "RAK Special PDC preview");
        using var dlg = new PrintPreviewDialog
        {
            Document = doc, Width = 1100, Height = 800, UseAntiAlias = true, Icon = AppIcon.Get(),
            Text = $"Preview - {_items.Count} PDC cheques (use the page buttons; the cheque picture is never printed)",
        };
        dlg.ShowDialog(this);
    }

    void PrintAll()
    {
        if (!Validate(out var problem)) { MessageBox.Show(this, problem, "PDC series", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        var nos = _items.Where(i => i.ChequeNo.Length > 0).Select(i => i.ChequeNo).ToHashSet();
        var dups = _store.Data.Cheques.Where(c => c.Status.UsesNumber() && c.BankId == _bank.Id && nos.Contains(c.ChequeNo))
                         .Select(c => c.ChequeNo).Distinct().ToList();
        if (dups.Count > 0 && MessageBox.Show(this,
                $"These cheque numbers were already printed: {string.Join(", ", dups.Take(10))}{(dups.Count > 10 ? " ..." : "")}\n\nPrint the series anyway?",
                "Duplicate cheque numbers", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        var amount = _items[0].Amount;
        if (MessageBox.Show(this,
                $"Pay to: {_payee.Text.Trim()}\nAmount each: AED {AmountInWords.Figures(amount)}\n" +
                $"{_items.Count} cheques from {_items[0].Date:dd-MM-yyyy} to {_items[^1].Date:dd-MM-yyyy}\n" +
                $"Cheque book: {_bank.Name} {_bank.Code}\n\nThe program will ask you to put in each cheque before it prints. Start?",
                "Print PDC series", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;

        // the cheque book's printer (chosen in the main window)
        if (!Printers.Ready(this, Printers.For(_bank), realCheque: true)) return;

        var done = 0;
        foreach (var it in _items)
        {
            if (_printed.Contains(it.Index)) continue;
            var data = DataFor(it);
            if (MessageBox.Show(this,
                    $"Cheque {it.Index} of {_items.Count}\n\nCheque No.: {it.ChequeNo}\nDate: {it.Date:dd-MM-yyyy}\n\nPut this cheque in the printer and click OK.\n(Cancel stops here; cheques already printed stay in the register.)",
                    "Next cheque", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) break;

            using var doc = ChequePrinter.Create(_bank, data, _store.Data.Settings, testPrint: false);
            try { doc.Print(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Printing failed:\n" + ex.Message, "Print", MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;
            }

            var text = ChequeText.Build(data, _bank, _store.Data.Settings);
            _store.Data.Cheques.Add(new ChequeRecord
            {
                ChequeNo = it.ChequeNo, BankId = _bank.Id, BankName = _bank.Name, ChequeDate = it.Date,
                Payee = data.Payee.Trim(), Amount = it.Amount, AmountInWords = text.Words, AcPayeeOnly = data.AcPayeeOnly,
                Notes = $"PDC {it.Index}/{_items.Count}",
            });
            _store.RememberPayee(data.Payee);
            if (it.ChequeNo.Length > 0) _bank.NextChequeNo = DataStore.NextNumber(it.ChequeNo);
            _printed.Add(it.Index);
            _grid.Rows[it.Index - 1].Cells["st"].Value = "Printed";
            done++;
            TrySave();
        }
        MessageBox.Show(this, $"{done} of {_items.Count} cheques printed and saved in the register.", "PDC series",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    void TrySave()
    {
        try { _store.Save(); }
        catch (Exception ex) { MessageBox.Show(this, "Could not save data:\n" + ex.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
