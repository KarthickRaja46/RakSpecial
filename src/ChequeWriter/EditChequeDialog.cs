using System.Globalization;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>
/// Changes a cheque that is already in the register (number, date, payee, amount, crossing, status, notes),
/// or deletes it. Only the register changes: the paper cheque is not touched.
/// DialogResult.OK = saved, DialogResult.Abort = deleted, Cancel = nothing changed.
/// </summary>
public class EditChequeDialog : Form
{
    readonly DataStore _store;
    readonly ChequeRecord _r;

    readonly TextBox _no = new() { Font = Theme.Ui(11f) };
    readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Font = Theme.Ui(11f) };
    readonly ComboBox _payee = new() { FlatStyle = FlatStyle.Flat, Font = Theme.Ui(11f), AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    readonly TextBox _amount = new() { Font = Theme.Ui(14f, FontStyle.Bold), TextAlign = HorizontalAlignment.Right };
    readonly ComboBox _status = new() { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Font = Theme.Ui(11f) };
    readonly TextBox _notes = new() { Font = Theme.Ui(10.5f) };
    readonly ToggleSwitch _acPayee = new() { Text = "A/C Payee only (crossing)" };
    readonly Label _words = new() { AutoSize = false, Dock = DockStyle.Fill, Font = Theme.Ui(10f), BackColor = Theme.GoldSoft, ForeColor = Theme.Text };

    static readonly (ChequeStatus Status, string Text)[] Statuses =
    {
        (ChequeStatus.Printed, "Printed (given out, not cleared yet)"),
        (ChequeStatus.Cleared, "Cleared (paid by the bank)"),
        (ChequeStatus.Bounced, "Bounced (returned unpaid)"),
        (ChequeStatus.Stopped, "Stopped (payment stopped)"),
        (ChequeStatus.Void, "Void (spoilt or cancelled)"),
    };

    public EditChequeDialog(DataStore store, ChequeRecord record)
    {
        _store = store; _r = record;
        Text = $"Edit cheque {(record.ChequeNo.Length > 0 ? "No. " + record.ChequeNo : "")}".Trim();
        Font = new Font("Segoe UI", 10f);
        BackColor = Theme.Card;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AppIcon.Apply(this);
        ClientSize = new Size(S(520), S(640));

        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Card, Padding = new Padding(S(24), S(18), S(24), 0) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Label Caption(string text) => new()
        {
            Text = text.ToUpperInvariant(), AutoSize = true, ForeColor = Theme.TextSoft, Font = Theme.Semibold(8.5f), Margin = new Padding(0, S(10), 0, S(4)),
        };
        InputFrame Framed(Control c, int h) => new(c) { Dock = DockStyle.Fill, Height = S(h), Margin = new Padding(0) };
        void Add(Control c, int col, int span = 1, int height = 0)
        {
            if (col == 0)
            {
                t.RowCount++;
                t.RowStyles.Add(height > 0 ? new RowStyle(SizeType.Absolute, height) : new RowStyle(SizeType.AutoSize));
            }
            if (span == 1) c.Margin = new Padding(col == 0 ? 0 : S(6), c.Margin.Top, col == 0 ? S(6) : 0, c.Margin.Bottom);
            t.Controls.Add(c, col, t.RowCount - 1);
            if (span == 2) t.SetColumnSpan(c, 2);
        }

        var heading = new Label
        {
            Text = "Fix a mistake in the register", AutoSize = true, Font = Theme.Ui(14f, FontStyle.Bold), ForeColor = Theme.Text, Margin = new Padding(0),
        };
        Add(heading, 0, 2);
        Add(new Label
        {
            Text = "This changes the register and analytics only. The paper cheque is not changed.",
            AutoSize = true, MaximumSize = new Size(S(470), 0), ForeColor = Theme.TextSoft, Font = Theme.Ui(9.5f), Margin = new Padding(0, S(2), 0, S(4)),
        }, 0, 2);

        Add(Caption("Cheque No."), 0); Add(Caption("Cheque date"), 1);
        Add(Framed(_no, 40), 0, height: S(40)); Add(Framed(_date, 40), 1);
        Add(Caption("Pay to"), 0, 2);
        Add(Framed(_payee, 40), 0, 2, S(40));
        Add(Caption("Amount (AED)"), 0, 2);
        Add(Framed(_amount, 48), 0, 2, S(48));

        var wordsBox = new Card { Dock = DockStyle.Fill, Fill = Theme.GoldSoft, BorderColor = Color.FromArgb(236, 222, 192), Radius = 10, Padding = new Padding(S(12), S(8), S(12), S(8)), Margin = new Padding(0, S(10), 0, 0) };
        wordsBox.Controls.Add(_words);
        Add(wordsBox, 0, 2, S(74));

        _acPayee.Height = S(34);
        _acPayee.Dock = DockStyle.Fill;
        _acPayee.Margin = new Padding(0, S(8), 0, 0);
        Add(_acPayee, 0, 2, S(44));

        Add(Caption("Status"), 0, 2);
        Add(Framed(_status, 40), 0, 2, S(40));
        Add(Caption("Notes"), 0, 2);
        Add(Framed(_notes, 40), 0, 2, S(40));

        // buttons: Delete on the left, Cancel / Save on the right
        var bar = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = S(74), ColumnCount = 4, BackColor = Theme.Card, Padding = new Padding(S(24), S(16), S(24), S(18)) };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        RoundButton Btn(string text, string glyph, ButtonKind kind, EventHandler click)
        {
            var b = new RoundButton { Text = text, Glyph = glyph, Kind = kind, Font = Theme.Semibold(10f), Height = S(40), Dock = DockStyle.Fill, Margin = new Padding(S(8), 0, 0, 0) };
            b.Width = Math.Max(S(104), b.GetPreferredSize(Size.Empty).Width);
            b.Click += click;
            return b;
        }
        var delete = Btn("Delete", Theme.Glyph.Void, ButtonKind.Danger, (_, _) => DeleteCheque());
        delete.Margin = new Padding(0);
        var cancel = Btn("Cancel", "", ButtonKind.Secondary, (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        var save = Btn("Save changes", "", ButtonKind.Primary, (_, _) => SaveCheque());
        bar.Controls.Add(delete, 0, 0);
        bar.Controls.Add(cancel, 2, 0);
        bar.Controls.Add(save, 3, 0);
        var line = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border };

        Controls.Add(t);
        Controls.Add(line);
        Controls.Add(bar);
        CancelButton = cancel;

        // fill in the cheque
        foreach (var p in _store.Data.Payees.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)) _payee.Items.Add(p.Name);
        foreach (var s in Statuses) _status.Items.Add(s.Text);
        _no.Text = record.ChequeNo;
        _date.Value = record.ChequeDate.Date < _date.MinDate ? DateTime.Today : record.ChequeDate.Date;
        _payee.Text = record.Payee;
        _amount.Text = record.Amount > 0 ? AmountInWords.Figures(record.Amount) : "";
        _acPayee.Checked = record.AcPayeeOnly;
        _status.SelectedIndex = Math.Max(0, Array.FindIndex(Statuses, s => s.Status == record.Status));
        _notes.Text = record.Notes;
        _amount.TextChanged += (_, _) => ShowWords();
        _amount.Leave += (_, _) => { if (TryAmount(out var a) && a > 0) _amount.Text = AmountInWords.Figures(a); };
        ShowWords();

        // fit the window to its content
        Load += (_, _) =>
        {
            t.PerformLayout();
            var needed = t.GetPreferredSize(new Size(ClientSize.Width, 0)).Height + bar.Height + 1 + S(24);
            ClientSize = new Size(ClientSize.Width, Math.Min(needed, Screen.FromControl(this).WorkingArea.Height - S(60)));
        };
        Shown += (_, _) => { _amount.Focus(); _amount.SelectAll(); };   // most fixes are to the amount
    }

    int S(int px) => (int)Math.Round(px * DeviceDpi / 96f);

    BankLayout Bank => _store.Data.Banks.FirstOrDefault(b => b.Id == _r.BankId) ?? _store.Data.LastBank!;

    bool TryAmount(out decimal amount) => ChequeRecords.TryParseAmount(_amount.Text, out amount);

    string Words(decimal amount) => amount <= 0 ? "" :
        ChequeText.Build(new ChequeData { Amount = amount, Date = _date.Value.Date, Payee = _payee.Text }, Bank, _store.Data.Settings).Words;

    void ShowWords()
    {
        if (_amount.Text.Trim().Length == 0) { _words.Text = "The amount in words appears here."; _words.ForeColor = Theme.Muted; return; }
        if (!TryAmount(out var a)) { _words.Text = "Type the amount as a number, like 1250.50"; _words.ForeColor = Theme.Danger; return; }
        try { _words.Text = a <= 0 ? "Zero" : Words(a); _words.ForeColor = Theme.Text; }
        catch (ArgumentOutOfRangeException ex) { _words.Text = ex.Message; _words.ForeColor = Theme.Danger; }
    }

    void SaveCheque()
    {
        var status = Statuses[Math.Max(0, _status.SelectedIndex)].Status;
        var payee = _payee.Text.Trim();
        var no = _no.Text.Trim();
        string? problem = null;
        decimal amount = 0;
        if (_amount.Text.Trim().Length > 0 && !TryAmount(out amount)) problem = "Type the amount as a number, like 1250.50.";
        else if (amount < 0) problem = "The amount cannot be negative.";
        else if (decimal.Round(amount, 2) != amount) problem = "The amount can have at most 2 decimals (fils).";
        else if (amount > AmountInWords.MaxAmount) problem = "The amount is too large.";
        else if (status != ChequeStatus.Void && payee.Length == 0) problem = "Please enter who the cheque is paid to.";
        else if (status != ChequeStatus.Void && amount <= 0) problem = "Please enter an amount greater than zero (or set the status to Void).";
        if (problem != null)
        {
            MessageBox.Show(this, problem, "Check the cheque", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var dup = _store.Data.Cheques.FirstOrDefault(c => c != _r && no.Length > 0 && c.BankId == _r.BankId && c.ChequeNo == no &&
                                                          c.Status.UsesNumber() && status.UsesNumber());
        if (dup != null && MessageBox.Show(this,
                $"Cheque No. {no} is already in the register for {dup.Payee} (AED {AmountInWords.Figures(dup.Amount)}).\n\nSave anyway with the same number?",
                "Duplicate cheque number", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        _r.ChequeNo = no;
        _r.ChequeDate = _date.Value.Date;
        _r.Payee = payee;
        _r.Amount = amount;
        _r.AmountInWords = Words(amount);
        _r.AcPayeeOnly = _acPayee.Checked;
        _r.Status = status;
        _r.Notes = _notes.Text.Trim();
        _store.RememberPayee(payee);
        if (!Persist()) return;
        DialogResult = DialogResult.OK;
        Close();
    }

    void DeleteCheque()
    {
        if (MessageBox.Show(this,
                $"Delete cheque {(_r.ChequeNo.Length > 0 ? "No. " + _r.ChequeNo : "")} ({(_r.Payee.Length > 0 ? _r.Payee : "blank leaf")}, AED {AmountInWords.Figures(_r.Amount)}) from the register?\n\n" +
                "It is removed everywhere: register, analytics and totals. This cannot be undone.\n" +
                "To keep a record instead, set the status to Void.",
                "Delete cheque", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _store.Data.Cheques.Remove(_r);
        if (!Persist()) return;
        DialogResult = DialogResult.Abort;
        Close();
    }

    bool Persist()
    {
        try { _store.Save(); return true; }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save:\n" + ex.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}
