using System.Drawing.Drawing2D;

namespace ChequeWriter;

public enum ButtonKind { Primary, Secondary, Ghost, OnNavy, Danger }

/// <summary>Rounded flat button with an optional icon (<see cref="Theme.Glyph"/>) and a shortcut hint on the right.</summary>
public class RoundButton : Button
{
    bool _hover, _down;

    public ButtonKind Kind { get; set; } = ButtonKind.Secondary;
    public string Glyph { get; set; } = "";
    public string Hint { get; set; } = "";
    public int Radius { get; set; } = 8;

    public RoundButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = Theme.Semibold(10f);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font);
        var w = text.Width + Padding.Horizontal + 24;
        if (Glyph.Length > 0) w += (int)(Font.Height * 1.4f);
        if (Hint.Length > 0) w += TextRenderer.MeasureText(Hint, Font).Width + 12;
        return new Size(w, Math.Max(text.Height + 14, Font.Height * 2 + 4));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var (fill, border, fore) = Kind switch
        {
            ButtonKind.Primary => (_down ? Theme.GoldDark : _hover ? Theme.GoldHover : Theme.Gold, Color.Empty, Theme.Navy),
            ButtonKind.Ghost => (_down ? Theme.Border : _hover ? Theme.Canvas : Color.Empty, Color.Empty, Theme.Muted),
            ButtonKind.OnNavy => (_down ? Theme.Navy3 : _hover ? Theme.Navy2 : Color.Empty, Color.Empty, Color.White),
            ButtonKind.Danger => (_down ? Theme.DangerSoft2 : _hover ? Theme.DangerSoft : Theme.Card, Theme.DangerBorder, Theme.Danger),
            _ => (_down ? Theme.Border : _hover ? Theme.Canvas : Theme.Card, _hover ? Theme.BorderStrong : Theme.Border, Theme.Text),
        };
        if (!Enabled) { fore = Theme.Muted; if (Kind == ButtonKind.Primary) fill = Theme.Border; }

        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Theme.Round(r, Radius * DeviceDpi / 96f))
        {
            if (!fill.IsEmpty) using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            if (!border.IsEmpty) using (var p = new Pen(border)) g.DrawPath(p, path);
        }
        if (Focused && ShowFocusCues)
        {
            using var ring = Theme.Round(new RectangleF(2, 2, Width - 5, Height - 5), Math.Max(2, Radius - 2) * DeviceDpi / 96f);
            using var pen = new Pen(Kind == ButtonKind.Primary ? Theme.Navy : Theme.Gold, 1.6f) { DashStyle = DashStyle.Dot };
            g.DrawPath(pen, ring);
        }

        // icon + text, centred; hint at the right edge
        var textSize = TextRenderer.MeasureText(Text, Font);
        using var iconFont = Theme.Icons(Font.SizeInPoints * 1.1f);
        var iconW = Glyph.Length > 0 ? (int)(Font.Height * 1.4f) : 0;
        var hintW = Hint.Length > 0 ? TextRenderer.MeasureText(Hint, Font).Width + 12 : 0;
        var contentW = iconW + textSize.Width;
        var x = Math.Max(Padding.Left + 8, (Width - hintW - contentW) / 2 + (Hint.Length > 0 ? hintW / 4 : 0));
        if (Glyph.Length > 0)
            TextRenderer.DrawText(g, Glyph, iconFont, new Rectangle(x, 0, iconW, Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(x + iconW, 0, Width - x - iconW - hintW, Height), fore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.HidePrefix);
        if (Hint.Length > 0)
            TextRenderer.DrawText(g, Hint, Font, new Rectangle(Width - hintW - 6, 0, hintW, Height),
                Kind == ButtonKind.Primary ? Color.FromArgb(150, Theme.Navy) : Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
    }
}

/// <summary>White panel with rounded corners and a thin border. Optional hover highlight and click.</summary>
public class Card : Panel
{
    bool _hover;

    public int Radius { get; set; } = 12;
    public Color Fill { get; set; } = Theme.Card;
    public Color BorderColor { get; set; } = Theme.Border;
    public bool Hoverable { get; set; }

    public Card()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (!Hoverable) return;
        // the whole card reacts, also when the mouse is over its labels
        e.Control!.MouseEnter += (_, _) => SetHover(true);
        e.Control.MouseLeave += (_, _) => SetHover(ClientRectangle.Contains(PointToClient(MousePosition)));
        e.Control.Click += (_, _) => OnClick(EventArgs.Empty);
        e.Control.Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { SetHover(true); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { SetHover(ClientRectangle.Contains(PointToClient(MousePosition))); base.OnMouseLeave(e); }

    void SetHover(bool on)
    {
        if (!Hoverable || _hover == on) return;
        _hover = on;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Canvas);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Radius * DeviceDpi / 96f);
        using (var b = new SolidBrush(Fill)) g.FillPath(b, path);
        using (var p = new Pen(_hover ? Theme.Gold : BorderColor, _hover ? 1.6f : 1f)) g.DrawPath(p, path);
    }
}

/// <summary>
/// Rounded frame around one input (TextBox, ComboBox or DateTimePicker), so all inputs look the same.
/// The input's own border is hidden; the frame turns gold while the input has the focus.
/// </summary>
public class InputFrame : Card
{
    readonly Control _input;
    readonly Panel _clip = new() { BackColor = Theme.Card };
    bool _focus;

    public InputFrame(Control input)
    {
        _input = input;
        Radius = 8;
        BorderColor = Theme.BorderStrong;
        Cursor = Cursors.IBeam;
        input.Dock = DockStyle.None;
        if (input is TextBox t) t.BorderStyle = BorderStyle.None;
        input.BackColor = Theme.Card;
        input.ForeColor = Theme.Text;
        _clip.Controls.Add(input);
        Controls.Add(_clip);
        input.Enter += (_, _) => { _focus = true; Invalidate(); };
        input.Leave += (_, _) => { _focus = false; Invalidate(); };
        _clip.Click += (_, _) => input.Focus();
        Click += (_, _) => input.Focus();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_input == null) return;
        var s = DeviceDpi / 96f;
        var left = (int)(10 * s);
        if (_input is TextBox)
        {
            _clip.SetBounds(left, (Height - _input.Height) / 2, Width - left - (int)(8 * s), _input.Height);
            _input.SetBounds(0, 0, _clip.Width, _input.Height);
        }
        else
        {
            // show the combo / date box without its own 2 px border
            var h = _input.Height - 4;
            _clip.SetBounds(left - 2, (Height - h) / 2, Width - left - (int)(4 * s), h);
            _input.SetBounds(-2, -2, _clip.Width + 4, _input.Height);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        BorderColor = _focus ? Theme.Gold : Theme.BorderStrong;
        base.OnPaintBackground(e);
        if (!_focus) return;
        using var path = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 3), Radius * DeviceDpi / 96f);
        using var pen = new Pen(Theme.Gold, 1.6f);
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>On/off switch drawn as a pill, with its label on the right. Works like a CheckBox.</summary>
public class ToggleSwitch : CheckBox
{
    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        AutoSize = false;
        Cursor = Cursors.Hand;
        Font = Theme.Ui(10f);
    }

    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var s = DeviceDpi / 96f;
        float tw = 38 * s, th = 22 * s, y = (Height - th) / 2;
        var track = new RectangleF(1, y, tw, th);
        using (var path = Theme.Round(track, th / 2))
        using (var b = new SolidBrush(Checked ? Theme.Gold : Theme.BorderStrong))
            g.FillPath(b, path);
        var k = th - 6 * s;
        var kx = Checked ? track.Right - k - 3 * s : track.Left + 3 * s;
        using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(shadow, kx, y + 3 * s + 1, k, k);
        g.FillEllipse(Brushes.White, kx, y + 3 * s, k, k);
        if (Focused && ShowFocusCues)
        {
            using var ring = Theme.Round(new RectangleF(track.X - 2, track.Y - 2, track.Width + 4, track.Height + 4), th / 2 + 2);
            using var pen = new Pen(Theme.Gold, 1.5f) { DashStyle = DashStyle.Dot };
            g.DrawPath(pen, ring);
        }
        TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(tw + 10 * s), 0, Width - (int)(tw + 10 * s), Height),
            Enabled ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>Pill-shaped segmented selector (e.g. RAK Business | RAK Old) for a dark header.</summary>
public class SegmentedControl : Control
{
    readonly List<string> _items = new();
    int _selected = -1, _hover = -1;

    public event EventHandler? SelectedIndexChanged;

    public SegmentedControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Font = Theme.Semibold(10f);
        BackColor = Theme.Navy;
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value == _selected || value < -1 || value >= _items.Count) return;
            _selected = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Replaces the items without raising <see cref="SelectedIndexChanged"/>.</summary>
    public void SetItems(IEnumerable<string> items, int selected)
    {
        _items.Clear();
        _items.AddRange(items);
        _selected = Math.Clamp(selected, -1, _items.Count - 1);
        Size = GetPreferredSize(Size.Empty);
        Invalidate();
    }

    float S => DeviceDpi / 96f;
    int SegmentWidth(int i) => TextRenderer.MeasureText(_items[i], Font).Width + (int)(28 * S);

    public override Size GetPreferredSize(Size proposedSize) =>
        new(Enumerable.Range(0, _items.Count).Sum(SegmentWidth) + (int)(8 * S), (int)(36 * S));

    int HitTest(int x)
    {
        var left = (int)(4 * S);
        for (var i = 0; i < _items.Count; i++)
        {
            var w = SegmentWidth(i);
            if (x >= left && x < left + w) return i;
            left += w;
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var h = HitTest(e.X);
        if (h != _hover) { _hover = h; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        Focus();
        var h = HitTest(e.X);
        if (h >= 0) SelectedIndex = h;
        base.OnMouseClick(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left && _selected > 0) SelectedIndex = _selected - 1;
        if (e.KeyCode == Keys.Right && _selected < _items.Count - 1) SelectedIndex = _selected + 1;
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Navy);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var track = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Height / 2f))
        {
            using var b = new SolidBrush(Theme.Navy2);
            g.FillPath(b, track);
            using var p = new Pen(Focused && ShowFocusCues ? Theme.Gold : Theme.Navy3);
            g.DrawPath(p, track);
        }
        var left = 4 * S;
        for (var i = 0; i < _items.Count; i++)
        {
            var w = SegmentWidth(i);
            var seg = new RectangleF(left, 4 * S, w, Height - 8 * S);
            if (i == _selected || i == _hover)
            {
                using var path = Theme.Round(seg, seg.Height / 2);
                using var b = new SolidBrush(i == _selected ? Theme.Gold : Theme.Navy3);
                g.FillPath(b, path);
            }
            TextRenderer.DrawText(g, _items[i], Font, Rectangle.Round(seg), i == _selected ? Theme.Navy : Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            left += w;
        }
    }
}

/// <summary>Small clickable figure: caption, big value, one line underneath.</summary>
public class StatTile : Card
{
    readonly Label _caption = new() { AutoSize = false, Dock = DockStyle.Top, Font = Theme.Semibold(9f), ForeColor = Theme.TextSoft };
    readonly Label _value = new() { AutoSize = false, Dock = DockStyle.Top, Font = Theme.Ui(15f, FontStyle.Bold), ForeColor = Theme.Text, AutoEllipsis = true };
    readonly Label _sub = new() { AutoSize = false, Dock = DockStyle.Fill, Font = Theme.Ui(9.5f), ForeColor = Theme.TextSoft, AutoEllipsis = true };

    public StatTile(string caption)
    {
        Hoverable = true;
        Cursor = Cursors.Hand;
        _caption.Text = caption.ToUpperInvariant();
        _caption.Height = _caption.Font.Height + 2;
        _value.Height = _value.Font.Height + 2;
        Controls.Add(_sub);
        Controls.Add(_value);
        Controls.Add(_caption);
        foreach (Control c in Controls) c.BackColor = Theme.Card;
    }

    public void Set(string value, string sub, Color? accent = null)
    {
        _value.Text = value;
        _value.ForeColor = accent ?? Theme.Text;
        _sub.Text = sub;
    }
}
