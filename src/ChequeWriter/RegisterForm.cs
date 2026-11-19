using System.Text;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>List of every printed cheque, with search, totals, status (cleared, bounced...) and CSV export.</summary>
public class RegisterForm : Form
{
    readonly DataStore _store;
    readonly MainForm _main;
    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, AutoGenerateColumns = false,
        RowHeadersVisible = false, BackgroundColor = SystemColors.Window,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
    };
    readonly TextBox _search = new() { Width = 220, PlaceholderText = "Search payee or cheque no." };
    readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Width = 120, ShowCheckBox = true, Checked = false };
    readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Width = 120, ShowCheckBox = true, Checked = false };
    readonly ComboBox _statusFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(8, 3, 3, 3) };
    readonly Label _total = new() { AutoSize = true, Margin = new Padding(12, 7, 3, 3), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };

    const string AllStatuses = "All statuses";
    const string AllButVoid = "All except void";

    List<ChequeRecord> _rows = new();
    Font? _voidFont;

    public RegisterForm(DataStore store, MainForm main, string search = "")
    {
        _store = store; _main = main;
        Text = "Cheque register";
        Font = new Font("Segoe UI", 9.5f);
        Size = new Size(1150, 660);
        StartPosition = FormStartPosition.CenterParent;
        AppIcon.Apply(this);

        AddCol("No.", nameof(ChequeRecord.ChequeNo), 70);
        AddCol("Cheque date", nameof(ChequeRecord.ChequeDate), 80, "dd-MM-yyyy");
        AddCol("Payee", nameof(ChequeRecord.Payee), 220);
        AddCol("Amount", nameof(ChequeRecord.Amount), 90, "#,##0.00", right: true);
        AddCol("Amount in words", nameof(ChequeRecord.AmountInWords), 280);
        AddCol("Status", nameof(ChequeRecord.Status), 70);
        AddCol("Printed", nameof(ChequeRecord.PrintedAt), 110, "dd-MM-yyyy HH:mm");
        AddCol("Notes", nameof(ChequeRecord.Notes), 140);
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
            switch (_rows[e.RowIndex].Status)
            {
                case ChequeStatus.Void:
                    e.CellStyle!.ForeColor = Color.Gray;
                    e.CellStyle.Font = _voidFont ??= new Font(Font, FontStyle.Strikeout);
                    break;
                case ChequeStatus.Bounced: e.CellStyle!.ForeColor = Color.FromArgb(185, 28, 28); break;
                case ChequeStatus.Stopped: e.CellStyle!.ForeColor = Color.FromArgb(180, 83, 9); break;
                case ChequeStatus.Cleared: e.CellStyle!.ForeColor = Color.FromArgb(21, 128, 61); break;
            }
        };
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Reprint(); };

        _statusFilter.Items.Add(AllStatuses);
        _statusFilter.Items.Add(AllButVoid);
        foreach (var s in ChequeStatusInfo.All) _statusFilter.Items.Add(s);
        _statusFilter.SelectedIndex = 0;
        _search.Text = search;

        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 8, 8, 4) };
        Label L(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(8, 7, 3, 3) };
        filters.Controls.AddRange(new Control[] { _search, L("From"), _from, L("To"), _to, _statusFilter, _total });

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        Button B(string t, EventHandler h) { var b = new Button { Text = t, AutoSize = true, Padding = new Padding(8, 3, 8, 3) }; b.Click += h; return b; }

        var statusMenu = new ContextMenuStrip();
        statusMenu.Items.Add("Printed (given out, not cleared yet)", null, (_, _) => SetStatus(ChequeStatus.Printed));
        statusMenu.Items.Add("Cleared (paid by the bank)", null, (_, _) => SetStatus(ChequeStatus.Cleared));
        statusMenu.Items.Add("Bounced (returned unpaid)...", null, (_, _) => SetStatus(ChequeStatus.Bounced));
        statusMenu.Items.Add("Stopped (payment stopped)...", null, (_, _) => SetStatus(ChequeStatus.Stopped));
        statusMenu.Items.Add("Void (spoilt or cancelled)", null, (_, _) => SetStatus(ChequeStatus.Void));
        statusMenu.Items.Add(new ToolStripSeparator());
        statusMenu.Items.Add("Undo void (back to Printed)", null, (_, _) => UndoVoid());
        Button? statusButton = null;
        statusButton = B("Set status  ▾", (_, _) => statusMenu.Show(statusButton!, new Point(0, statusButton!.Height)));

        bar.Controls.Add(B("Load into form to reprint", (_, _) => Reprint()));
        bar.Controls.Add(B("Edit cheque...", (_, _) => EditCheque()));
        bar.Controls.Add(statusButton);
        bar.Controls.Add(B("Undo void", (_, _) => UndoVoid()));
        bar.Controls.Add(B("Edit note...", (_, _) => EditNote()));
        bar.Controls.Add(B("Delete", (_, _) => DeleteRow()));
        bar.Controls.Add(B("Remove all void cheques...", (_, _) => RemoveAllVoid()));
        bar.Controls.Add(B("Export to Excel (CSV)...", (_, _) => Export()));
        var close = B("Close", (_, _) => Close());
        bar.Controls.Add(close);
        CancelButton = close;

        Controls.Add(_grid);
        Controls.Add(filters);
        Controls.Add(bar);

        _search.TextChanged += (_, _) => Reload();
        _from.ValueChanged += (_, _) => Reload();
        _to.ValueChanged += (_, _) => Reload();
        _statusFilter.SelectedIndexChanged += (_, _) => Reload();
        Reload();
    }

    void AddCol(string header, string prop, int weight, string? format = null, bool right = false)
    {
        var c = new DataGridViewTextBoxColumn { HeaderText = header, DataPropertyName = prop, FillWeight = weight };
        if (format != null) c.DefaultCellStyle.Format = format;
        if (right) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.Columns.Add(c);
    }

    void Reload()
    {
        var keep = Selected.Select(r => r.Id).ToHashSet();
        IEnumerable<ChequeRecord> q = _store.Data.Cheques;
        var s = _search.Text.Trim();
        if (s.Length > 0)
            q = q.Where(r => r.Payee.Contains(s, StringComparison.OrdinalIgnoreCase) || r.ChequeNo.Contains(s, StringComparison.OrdinalIgnoreCase));
        if (_from.Checked) q = q.Where(r => r.ChequeDate.Date >= _from.Value.Date);
        if (_to.Checked) q = q.Where(r => r.ChequeDate.Date <= _to.Value.Date);
        q = _statusFilter.SelectedItem switch
        {
            ChequeStatus st => q.Where(r => r.Status == st),
            AllButVoid => q.Where(r => r.Status != ChequeStatus.Void),
            _ => q,
        };
        _rows = q.OrderByDescending(r => r.PrintedAt).ToList();
        _grid.DataSource = null;
        _grid.DataSource = _rows;
        if (keep.Count > 0)
        {
            _grid.ClearSelection();
            for (var i = 0; i < _rows.Count; i++) if (keep.Contains(_rows[i].Id)) _grid.Rows[i].Selected = true;
        }

        var paid = _rows.Where(r => r.Status.CountsAsPaid()).Sum(r => r.Amount);
        var bounced = _rows.Where(r => r.Status == ChequeStatus.Bounced).ToList();
        _total.Text = $"{_rows.Count} cheques  |  Total paid: AED {AmountInWords.Figures(paid)}" +
                      (bounced.Count > 0 ? $"  |  Bounced: {bounced.Count} (AED {AmountInWords.Figures(bounced.Sum(r => r.Amount))})" : "");
    }

    ChequeRecord? Current => _grid.CurrentRow?.Index is int i && i >= 0 && i < _rows.Count ? _rows[i] : null;

    List<ChequeRecord> Selected => _grid.SelectedRows.Cast<DataGridViewRow>()
        .Where(r => r.Index >= 0 && r.Index < _rows.Count).Select(r => _rows[r.Index]).ToList();

    void Reprint()
    {
        if (Current is not { } r) return;
        _main.LoadFromRecord(r);
        Close();
    }

    void SetStatus(ChequeStatus status)
    {
        var rows = Selected;
        if (rows.Count == 0) return;
        if (status is ChequeStatus.Bounced or ChequeStatus.Stopped)
        {
            var what = status == ChequeStatus.Bounced ? "bounced" : "stopped";
            var reason = Prompt.Show(this, "Reason",
                rows.Count == 1 ? $"Why was cheque {rows[0].ChequeNo} {what}? (optional, saved in Notes)" : $"Why were these {rows.Count} cheques {what}? (optional, saved in Notes)",
                status == ChequeStatus.Bounced ? "Insufficient funds" : "");
            if (reason == null) return;
            if (reason.Trim().Length > 0)
                foreach (var r in rows)
                    r.Notes = string.IsNullOrWhiteSpace(r.Notes) ? reason.Trim() : $"{r.Notes}; {reason.Trim()}";
        }
        foreach (var r in rows) r.Status = status;
        Persist();
    }

    void EditNote()
    {
        if (Current is not { } r) return;
        var note = Prompt.Show(this, "Note", $"Note for cheque {r.ChequeNo}:", r.Notes);
        if (note == null) return;
        r.Notes = note;
        Persist();
    }

    /// <summary>Opens the selected cheque to fix its number, date, payee, amount, status or notes (or delete it).</summary>
    void EditCheque()
    {
        if (Current is not { } r)
        {
            MessageBox.Show(this, "Select a cheque in the list first.", "Edit cheque", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using (var f = new EditChequeDialog(_store, r)) f.ShowDialog(this);
        Reload();
    }

    void UndoVoid()
    {
        var voids = Selected.Where(r => r.Status == ChequeStatus.Void).ToList();
        if (voids.Count == 0)
        {
            MessageBox.Show(this, "Select one or more Void cheques first.\nTip: choose 'Void' in the status filter to see only those.",
                "Undo void", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this,
                voids.Count == 1 ? $"Mark cheque {voids[0].ChequeNo} ({voids[0].Payee}) as Printed again?" : $"Mark these {voids.Count} void cheques as Printed again?",
                "Undo void", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        ChequeRecords.UndoVoid(voids);
        Persist();
    }

    /// <summary>Deletes the selected cheques (one or more) after asking.</summary>
    void DeleteRow()
    {
        var rows = Selected;
        if (rows.Count == 0) return;
        var what = rows.Count == 1
            ? $"cheque {rows[0].ChequeNo} ({(rows[0].Payee.Length > 0 ? rows[0].Payee : "blank leaf")}, AED {AmountInWords.Figures(rows[0].Amount)})"
            : $"these {rows.Count} cheques (AED {AmountInWords.Figures(rows.Sum(r => r.Amount))} in all)";
        if (MessageBox.Show(this, $"Delete {what} from the register?\n\nThey are removed everywhere: register, analytics and totals. This cannot be undone.\nTip: Set status > Void keeps a record instead.",
                "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        foreach (var r in rows) _store.Data.Cheques.Remove(r);
        Persist();
    }

    /// <summary>Removes every Void cheque from the register (all cheque books), after showing how many.</summary>
    void RemoveAllVoid()
    {
        var voids = _store.Data.Cheques.Where(c => c.Status == ChequeStatus.Void).ToList();
        if (voids.Count == 0)
        {
            MessageBox.Show(this, "There are no void cheques in the register.", "Remove void cheques", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var list = string.Join("\n", voids.OrderBy(c => c.ChequeDate).Take(12).Select(c =>
            $"   No. {(c.ChequeNo.Length > 0 ? c.ChequeNo : "-")}   {c.ChequeDate:dd-MM-yyyy}   {(c.Payee.Length > 0 ? c.Payee : "blank leaf")}"));
        if (voids.Count > 12) list += $"\n   ... and {voids.Count - 12} more";
        if (MessageBox.Show(this,
                $"Remove all {voids.Count} void cheques from the register?\n\n{list}\n\nThis cannot be undone. Tip: make a backup first (File > Backup data).",
                "Remove void cheques", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        ChequeRecords.RemoveVoid(_store.Data.Cheques);
        Persist();
    }

    void Export()
    {
        using var dlg = new SaveFileDialog { Filter = "CSV for Excel (*.csv)|*.csv", FileName = $"Cheque-register-{DateTime.Now:yyyyMMdd}.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            // UTF-8 with BOM so Excel shows Arabic and special characters correctly
            File.WriteAllText(dlg.FileName, CsvExport.Register(_rows), new UTF8Encoding(true));
            MessageBox.Show(this, $"Exported {_rows.Count} cheques.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void Persist()
    {
        try { _store.Save(); }
        catch (Exception ex) { MessageBox.Show(this, "Could not save:\n" + ex.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        Reload();
    }
}
