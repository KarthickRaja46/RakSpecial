using System.ComponentModel;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>Saved payees: name (as printed), category, phone, email and notes. Edit directly in the grid.</summary>
public class PayeesForm : Form
{
    readonly DataStore _store;
    readonly BindingList<Payee> _list;
    readonly DataGridViewComboBoxColumn _category = new()
    {
        HeaderText = "Category", DataPropertyName = nameof(Payee.Category), FillWeight = 18,
        DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Flat,
    };
    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
        EditMode = DataGridViewEditMode.EditOnEnter,
    };

    public PayeesForm(DataStore store)
    {
        _store = store;
        Text = "Payees";
        Font = new Font("Segoe UI", 9.5f);
        Size = new Size(960, 560);
        StartPosition = FormStartPosition.CenterParent;
        AppIcon.Apply(this);

        _list = new BindingList<Payee>(store.Data.Payees.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new Payee { Name = p.Name, Category = p.Category, Phone = p.Phone, Email = p.Email, Notes = p.Notes })
            .ToList()) { AllowNew = true, AllowRemove = true };

        _category.Items.Add("");   // not set
        foreach (var c in PayeeCategories.All(store.Data.Payees)) _category.Items.Add(c);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Payee name (as printed on the cheque)", DataPropertyName = nameof(Payee.Name), FillWeight = 36 });
        _grid.Columns.Add(_category);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Phone", DataPropertyName = nameof(Payee.Phone), FillWeight = 14 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Email", DataPropertyName = nameof(Payee.Email), FillWeight = 18 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", DataPropertyName = nameof(Payee.Notes), FillWeight = 18 });
        _grid.DataError += (_, e) => e.ThrowException = false;   // a category not in the list: keep the grid usable
        _grid.DataSource = _list;

        var info = new Label
        {
            Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft,
            Text = "  Type in the last row to add a payee. Choose a category to see totals per category in Analytics. Select a row and press Delete to remove it.",
        };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Save", AutoSize = true };
        save.Click += (_, _) => SaveAndClose();
        var add = new Button { Text = "Add category...", AutoSize = true, Margin = new Padding(3, 3, 24, 3) };
        add.Click += (_, _) => AddCategory();
        bar.Controls.Add(cancel); bar.Controls.Add(save); bar.Controls.Add(add);
        CancelButton = cancel;

        Controls.Add(_grid);
        Controls.Add(info);
        Controls.Add(bar);
    }

    void AddCategory()
    {
        var name = Prompt.Show(this, "Add category", "New category (e.g. Insurance, Maintenance):", "")?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (!_category.Items.Cast<string>().Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
            _category.Items.Add(name);
        if (_grid.CurrentRow is { Index: >= 0 } row && row.Index < _list.Count)
        {
            _list[row.Index].Category = name;
            _grid.InvalidateRow(row.Index);
        }
    }

    void SaveAndClose()
    {
        _grid.EndEdit();
        var cleaned = _list.Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new Payee
            {
                Name = p.Name.Trim(), Category = (p.Category ?? "").Trim(), Phone = (p.Phone ?? "").Trim(),
                Email = (p.Email ?? "").Trim(), Notes = (p.Notes ?? "").Trim(),
            })
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .ToList();
        _store.Data.Payees = cleaned;
        try { _store.Save(); }
        catch (Exception ex) { MessageBox.Show(this, "Could not save:\n" + ex.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        DialogResult = DialogResult.OK;
        Close();
    }
}
