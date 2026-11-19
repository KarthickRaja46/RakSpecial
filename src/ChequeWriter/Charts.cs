using System.Drawing.Drawing2D;
using System.Globalization;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>
/// Chart colours. The categorical order is fixed and checked for colour-blind separation; a colour follows the
/// thing it stands for (a category keeps its colour whatever the filter). Status colours are only used for status.
/// </summary>
public static class ChartPalette
{
    public static readonly Color[] Series =
    {
        Hex("#2a78d6"), Hex("#eb6834"), Hex("#1baf7a"), Hex("#eda100"),
        Hex("#e87ba4"), Hex("#008300"), Hex("#6250d6"), Hex("#e34948"),
    };
    public static readonly Color Blue = Hex("#2a78d6");
    public static readonly Color BlueHover = Hex("#1c5cab");
    public static readonly Color BlueLight = Hex("#9ec5f4");      // post-dated months
    public static readonly Color Neutral = Hex("#898781");        // "More" / folded
    public static readonly Color NeutralLight = Hex("#c3c2b7");   // uncategorised
    public static readonly Color Grid = Hex("#ebebee");
    public static readonly Color Axis = Hex("#c3c2b7");
    public static readonly Color Wash = Hex("#f4f5f7");

    public static readonly Color Good = Hex("#0ca30c");
    public static readonly Color Warning = Hex("#fab219");
    public static readonly Color Serious = Hex("#ec835a");
    public static readonly Color Critical = Hex("#d03b3b");
    public static readonly Color PostDated = Hex("#6da7ec");

    static Color Hex(string h) => ColorTranslator.FromHtml(h);
}

/// <summary>Fonts the charts paint with, made once.</summary>
static class F
{
    public static readonly Font Ui9 = Theme.Ui(9f), Ui95 = Theme.Ui(9.5f), Bold85 = Theme.Semibold(8.5f),
        Bold9 = Theme.Semibold(9f), Bold95 = Theme.Semibold(9.5f),
        Big13 = Theme.Ui(13f, FontStyle.Bold), Big105 = Theme.Ui(10.5f, FontStyle.Bold);
}

/// <summary>Shared look for the charts: white surface, hover card, empty message.</summary>
public abstract class ChartBase : Control
{
    protected ChartBase()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
        Font = F.Ui9;
    }

    public string EmptyText { get; set; } = "No cheques in this period.";

    protected float S => DeviceDpi / 96f;
    protected int Px(float v) => (int)Math.Round(v * S);

    protected void DrawEmpty(Graphics g)
    {
        using var icon = Theme.Icons(22f);
        var r = ClientRectangle;
        TextRenderer.DrawText(g, "", icon, new Rectangle(0, r.Height / 2 - Px(44), r.Width, Px(36)), Theme.BorderStrong,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, EmptyText, F.Ui95, new Rectangle(Px(12), r.Height / 2 - Px(4), r.Width - Px(24), Px(40)), Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
    }

    /// <summary>The hover card: first line bold, the rest normal, kept inside the chart.</summary>
    protected void DrawTip(Graphics g, Point anchor, string text)
    {
        var lines = text.Split('\n');
        var bold = F.Bold95;
        var normal = F.Ui9;
        var sizes = lines.Select((l, i) => TextRenderer.MeasureText(l, i == 0 ? bold : normal)).ToList();
        int pad = Px(10), w = sizes.Max(s => s.Width) + pad * 2, h = sizes.Sum(s => s.Height) + pad * 2 - Px(2);
        var x = anchor.X + Px(14);
        if (x + w > Width - 2) x = anchor.X - w - Px(14);
        var y = Math.Clamp(anchor.Y - h / 2, 2, Math.Max(2, Height - h - 2));
        x = Math.Max(2, x);
        var box = new RectangleF(x, y, w, h);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var shadow = Theme.Round(new RectangleF(box.X + 1, box.Y + 2, box.Width, box.Height), Px(8)))
        using (var sb = new SolidBrush(Color.FromArgb(28, 0, 0, 0))) g.FillPath(sb, shadow);
        using (var path = Theme.Round(box, Px(8)))
        {
            g.FillPath(Brushes.White, path);
            using var pen = new Pen(Theme.BorderStrong);
            g.DrawPath(pen, path);
        }
        var ty = y + pad - Px(1);
        for (var i = 0; i < lines.Length; i++)
        {
            TextRenderer.DrawText(g, lines[i], i == 0 ? bold : normal, new Point(x + pad, ty), i == 0 ? Theme.Text : Theme.TextSoft);
            ty += sizes[i].Height;
        }
    }

    /// <summary>A bar with rounded data end (4 px) and a square end on the baseline.</summary>
    protected static GraphicsPath BarPath(RectangleF r, float radius, bool horizontal)
    {
        var p = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d < 1) { p.AddRectangle(r); return p; }
        if (!horizontal)
        {
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
        }
        else
        {
            p.AddLine(r.X, r.Y, r.Right - d / 2, r.Y);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddLine(r.Right - d / 2, r.Bottom, r.X, r.Bottom);
        }
        p.CloseFigure();
        return p;
    }

    public static string Short(decimal v) =>
        v >= 1_000_000 ? (v / 1_000_000m).ToString("0.##", CultureInfo.InvariantCulture) + "M"
        : v >= 1_000 ? (v / 1000m).ToString("0.#", CultureInfo.InvariantCulture) + "K"
        : Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
}

/// <summary>Column chart of AED amounts (one series). Lighter columns (e.g. post-dated months) are drawn in a lighter blue.</summary>
public class BarChart : ChartBase
{
    public record Bar(string Label, decimal Value, string Tip, bool Muted = false);

    List<Bar> _bars = new();
    int _hover = -1;
    Point _mouse;

    /// <summary>Draw a thin line at the average of the non-zero columns.</summary>
    public bool ShowAverage { get; set; }
    public event EventHandler<int>? BarClicked;

    public void SetBars(IEnumerable<Bar> bars)
    {
        _bars = bars.ToList();
        _hover = -1;
        Invalidate();
    }

    bool Empty => _bars.Count == 0 || _bars.All(b => b.Value == 0);

    Rectangle Plot(Graphics g, decimal top)
    {
        var labelW = Enumerable.Range(1, 4).Max(i => TextRenderer.MeasureText(Short(top * i / 4), Font).Width) + Px(10);
        return new Rectangle(labelW, Px(22), Math.Max(0, Width - labelW - Px(8)), Math.Max(0, Height - Px(22) - Px(26)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (Empty || Width < 80 || Height < 80) { DrawEmpty(g); return; }

        var top = NiceCeiling(_bars.Max(b => b.Value));
        var plot = Plot(g, top);
        float Y(decimal v) => plot.Bottom - (float)((double)(v / top) * plot.Height);

        // hairline grid, quiet axis labels
        var tickStep = plot.Height / 4 >= Px(18) ? 1 : plot.Height / 2 >= Px(18) ? 2 : 4;
        using (var grid = new Pen(ChartPalette.Grid))
            for (var i = tickStep; i <= 4; i += tickStep)
            {
                var v = top * i / 4;
                var y = (int)Y(v);
                g.DrawLine(grid, plot.Left, y, plot.Right, y);
                TextRenderer.DrawText(g, Short(v), Font, new Rectangle(0, y - Px(9), plot.Left - Px(6), Px(18)), Theme.Muted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        TextRenderer.DrawText(g, "0", Font, new Rectangle(0, plot.Bottom - Px(9), plot.Left - Px(6), Px(18)), Theme.Muted,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

        var slot = plot.Width / (float)_bars.Count;
        var bw = Math.Clamp(slot * 0.62f, 2f, Px(46));
        var labelW = TextRenderer.MeasureText("Mmm 00", Font).Width + Px(6);
        var every = Math.Max(1, (int)Math.Ceiling(labelW / slot));
        var maxIndex = _bars.FindIndex(b => b.Value == _bars.Max(x => x.Value));

        if (_hover >= 0)
            using (var wash = new SolidBrush(ChartPalette.Wash))
                g.FillRectangle(wash, plot.Left + slot * _hover, plot.Top, slot, plot.Height);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (var i = 0; i < _bars.Count; i++)
        {
            var b = _bars[i];
            var x = plot.Left + slot * i + (slot - bw) / 2;
            var h = plot.Bottom - Y(b.Value);
            if (b.Value > 0 && h < 2) h = 2;
            if (h > 0)
            {
                var color = i == _hover ? ChartPalette.BlueHover : b.Muted ? ChartPalette.BlueLight : ChartPalette.Blue;
                using var path = BarPath(new RectangleF(x, plot.Bottom - h, bw, h), Px(4), horizontal: false);
                using var brush = new SolidBrush(color);
                g.FillPath(brush, path);
            }
            // value only on the biggest column (and the hovered one shows in its card)
            if (i == maxIndex && b.Value > 0 && _hover != i)
                TextRenderer.DrawText(g, Short(b.Value), F.Bold85,
                    new Rectangle((int)(plot.Left + slot * i - Px(20)), (int)(plot.Bottom - h) - Px(18), (int)slot + Px(40), Px(16)), Theme.TextSoft,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom);
            if (i % every == 0 || i == _hover)
                TextRenderer.DrawText(g, b.Label, Font,
                    new Rectangle((int)(plot.Left + slot * i + slot / 2 - labelW), plot.Bottom + Px(5), labelW * 2, Px(18)),
                    i == _hover ? Theme.Text : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
        }
        g.SmoothingMode = SmoothingMode.None;
        using (var axis = new Pen(ChartPalette.Axis)) g.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

        if (ShowAverage)
        {
            var nonZero = _bars.Where(b => b.Value > 0).ToList();
            if (nonZero.Count >= 2)
            {
                var avg = nonZero.Average(b => b.Value);
                var y = (int)Y(avg);
                using var pen = new Pen(Theme.GoldDark, Math.Max(1f, S));
                g.DrawLine(pen, plot.Left, y, plot.Right, y);
                var text = "Average " + Short(avg);
                var tw = TextRenderer.MeasureText(text, F.Bold85).Width;
                var r = new Rectangle(plot.Right - tw - Px(6), y - Px(18), tw + Px(4), Px(16));
                using (var bg = new SolidBrush(Color.FromArgb(230, Theme.Card))) g.FillRectangle(bg, r);
                TextRenderer.DrawText(g, text, F.Bold85, r, Theme.GoldDark, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        if (_bars.Any(b => b.Muted))
            TextRenderer.DrawText(g, "Lighter columns: post-dated (not due yet)", Font, new Rectangle(plot.Left, 0, plot.Width, Px(18)),
                Theme.Muted, TextFormatFlags.Right | TextFormatFlags.Top);

        if (_hover >= 0) DrawTip(g, _mouse, _bars[_hover].Tip);
    }

    int HitTest(Point p)
    {
        if (Empty) return -1;
        using var g = CreateGraphics();
        var plot = Plot(g, NiceCeiling(_bars.Max(b => b.Value)));
        if (plot.Width <= 0 || p.X < plot.Left || p.X >= plot.Right || p.Y < plot.Top - Px(10) || p.Y > plot.Bottom + Px(24)) return -1;
        return Math.Min(_bars.Count - 1, (int)((p.X - plot.Left) / (plot.Width / (float)_bars.Count)));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mouse = e.Location;
        _hover = HitTest(e.Location);
        Cursor = _hover >= 0 && BarClicked != null ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var i = HitTest(e.Location);
        if (i >= 0) BarClicked?.Invoke(this, i);
    }

    /// <summary>Rounds the top of the scale up to 1, 2, 2.5 or 5 × a power of ten.</summary>
    static decimal NiceCeiling(decimal max)
    {
        if (max <= 0) return 1;
        var p = Math.Pow(10, Math.Floor(Math.Log10((double)max)));
        foreach (var m in new[] { 1, 2, 2.5, 5, 10 })
            if (m * p >= (double)max) return (decimal)(m * p);
        return (decimal)(10 * p);
    }
}

/// <summary>Ranked horizontal bars (e.g. top payees): name, bar, amount. Click a row to open it.</summary>
public class HBarChart : ChartBase
{
    public record Item(string Label, decimal Value, string ValueText, string Tip);

    List<Item> _items = new();
    int _hover = -1;
    Point _mouse;

    public event EventHandler<int>? ItemClicked;

    public void SetItems(IEnumerable<Item> items) { _items = items.ToList(); _hover = -1; Invalidate(); }

    int RowH => Px(34);
    int VisibleRows => Math.Max(0, Math.Min(_items.Count, (Height - Px(4)) / RowH));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (_items.Count == 0 || _items.All(i => i.Value == 0)) { DrawEmpty(g); return; }

        var max = _items.Max(i => i.Value);
        var labelW = (int)(Width * 0.36f);
        var valueW = _items.Take(VisibleRows).Select(i => TextRenderer.MeasureText(i.ValueText, F.Bold9).Width).DefaultIfEmpty(0).Max() + Px(8);
        var barLeft = labelW + Px(8);
        var barMax = Math.Max(10, Width - barLeft - valueW - Px(4));
        var thick = Px(14);

        for (var i = 0; i < VisibleRows; i++)
        {
            var it = _items[i];
            var y = i * RowH + Px(2);
            if (i == _hover)
                using (var wash = new SolidBrush(ChartPalette.Wash))
                using (var path = Theme.Round(new RectangleF(0, y, Width - 1, RowH - 2), Px(6)))
                    g.FillPath(wash, path);
            TextRenderer.DrawText(g, it.Label, F.Ui95, new Rectangle(Px(6), y, labelW - Px(6), RowH - 2), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            var w = max == 0 ? 0 : (float)((double)(it.Value / max) * barMax);
            if (it.Value > 0 && w < 2) w = 2;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = BarPath(new RectangleF(barLeft, y + (RowH - thick) / 2f - 1, w, thick), Px(4), horizontal: true))
            using (var brush = new SolidBrush(i == _hover ? ChartPalette.BlueHover : ChartPalette.Blue))
                g.FillPath(brush, path);
            g.SmoothingMode = SmoothingMode.None;
            TextRenderer.DrawText(g, it.ValueText, F.Bold9, new Rectangle(barLeft + (int)w + Px(6), y, valueW + Px(4), RowH - 2), Theme.TextSoft,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
        if (VisibleRows < _items.Count)
            TextRenderer.DrawText(g, $"+ {_items.Count - VisibleRows} more", Font, new Rectangle(0, VisibleRows * RowH, Width - Px(6), Height - VisibleRows * RowH),
                Theme.Muted, TextFormatFlags.Right | TextFormatFlags.Top);
        if (_hover >= 0) DrawTip(g, _mouse, _items[_hover].Tip);
    }

    int HitTest(Point p)
    {
        var i = (p.Y - Px(2)) / RowH;
        return p.Y >= Px(2) && i >= 0 && i < VisibleRows ? i : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mouse = e.Location;
        _hover = HitTest(e.Location);
        Cursor = _hover >= 0 && ItemClicked != null ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var i = HitTest(e.Location);
        if (i >= 0) ItemClicked?.Invoke(this, i);
    }
}

/// <summary>Share of the total as a donut, with a labelled legend (name, share, amount) beside it.</summary>
public class DonutChart : ChartBase
{
    public record Slice(string Label, decimal Value, Color Color, string Tip);

    List<Slice> _slices = new();
    int _hover = -1;
    Point _mouse;

    public string CenterCaption { get; set; } = "total";

    public void SetSlices(IEnumerable<Slice> slices) { _slices = slices.Where(s => s.Value > 0).ToList(); _hover = -1; Invalidate(); }

    decimal Total => _slices.Sum(s => s.Value);

    (RectangleF Ring, Rectangle Legend) Geometry()
    {
        var d = Math.Min(Height - Px(12), (int)(Width * 0.44f));
        d = Math.Max(Px(60), d);
        var ring = new RectangleF(Px(6), (Height - d) / 2f, d, d);
        var legend = new Rectangle((int)ring.Right + Px(18), Px(4), Math.Max(0, Width - (int)ring.Right - Px(22)), Height - Px(8));
        return (ring, legend);
    }

    int LegendRowH => _slices.Count == 0 ? Px(26) : Math.Clamp((Height - Px(8)) / _slices.Count, Px(20), Px(28));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (_slices.Count == 0) { DrawEmpty(g); return; }

        var (ring, legend) = Geometry();
        var total = Total;
        var thick = ring.Width * 0.24f;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float start = -90;
        for (var i = 0; i < _slices.Count; i++)
        {
            var sweep = (float)((double)(_slices[i].Value / total) * 360.0);
            var faded = _hover >= 0 && _hover != i;
            var color = faded ? Color.FromArgb(70, _slices[i].Color) : _slices[i].Color;
            var r = i == _hover ? RectangleF.Inflate(ring, Px(3), Px(3)) : ring;
            var t = i == _hover ? thick + Px(3) : thick;
            using (var pen = new Pen(color, t))
                g.DrawArc(pen, r.X + t / 2, r.Y + t / 2, r.Width - t, r.Height - t, start, Math.Max(0.5f, sweep));
            start += sweep;
        }
        // 2 px surface gap between slices
        if (_slices.Count > 1)
        {
            start = -90;
            var c = new PointF(ring.X + ring.Width / 2, ring.Y + ring.Height / 2);
            using var gap = new Pen(BackColor, Px(2));
            foreach (var s in _slices)
            {
                var a = start * Math.PI / 180;
                g.DrawLine(gap, c.X + (float)Math.Cos(a) * (ring.Width / 2 - thick - Px(4)), c.Y + (float)Math.Sin(a) * (ring.Width / 2 - thick - Px(4)),
                                c.X + (float)Math.Cos(a) * (ring.Width / 2 + Px(4)), c.Y + (float)Math.Sin(a) * (ring.Width / 2 + Px(4)));
                start += (float)((double)(s.Value / total) * 360.0);
            }
        }
        g.SmoothingMode = SmoothingMode.None;

        // centre: the total
        var inner = RectangleF.Inflate(ring, -thick, -thick);
        var shown = _hover >= 0 ? _slices[_hover].Value : total;
        var centre = "AED " + Short(shown);
        var big = new[] { F.Big13, F.Big105, F.Bold9 }.FirstOrDefault(f => TextRenderer.MeasureText(centre, f).Width <= inner.Width - Px(6)) ?? F.Bold85;
        if (TextRenderer.MeasureText(centre, big).Width > inner.Width - Px(6)) centre = Short(shown);
        TextRenderer.DrawText(g, centre, big, new Rectangle((int)inner.X, (int)(inner.Y + inner.Height / 2 - big.Height), (int)inner.Width, big.Height + Px(2)),
            Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom);
        TextRenderer.DrawText(g, _hover >= 0 ? $"{_slices[_hover].Value / total:0%}" : inner.Width > Px(80) ? CenterCaption : "", Font,
            new Rectangle((int)inner.X, (int)(inner.Y + inner.Height / 2 + Px(2)), (int)inner.Width, Px(18)), Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);

        // legend: every slice named, with its share and amount
        var rows = Math.Max(1, legend.Height / LegendRowH);
        var y = legend.Y + Math.Max(0, (legend.Height - Math.Min(rows, _slices.Count) * LegendRowH) / 2);
        for (var i = 0; i < _slices.Count && i < rows; i++)
        {
            var s = _slices[i];
            var row = new Rectangle(legend.X, y, legend.Width, LegendRowH);
            if (i == _hover)
                using (var wash = new SolidBrush(ChartPalette.Wash))
                using (var path = Theme.Round(new RectangleF(row.X - Px(6), row.Y, row.Width + Px(6), row.Height - 2), Px(6)))
                    g.FillPath(wash, path);
            using (var b = new SolidBrush(s.Color))
            using (var sw = Theme.Round(new RectangleF(row.X, row.Y + row.Height / 2f - Px(6), Px(11), Px(11)), Px(3)))
            { g.SmoothingMode = SmoothingMode.AntiAlias; g.FillPath(b, sw); g.SmoothingMode = SmoothingMode.None; }
            var amount = "AED " + Short(s.Value);
            var share = $"{s.Value / total:0%}";
            var amountW = TextRenderer.MeasureText("AED 000.0K", F.Ui9).Width;
            var shareW = TextRenderer.MeasureText("100%", F.Bold9).Width + Px(6);
            TextRenderer.DrawText(g, s.Label, F.Ui95, new Rectangle(row.X + Px(18), row.Y, Math.Max(0, row.Width - Px(18) - amountW - shareW), row.Height - 2),
                Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, share, F.Bold9, new Rectangle(row.Right - amountW - shareW, row.Y, shareW, row.Height - 2),
                Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            TextRenderer.DrawText(g, amount, F.Ui9, new Rectangle(row.Right - amountW, row.Y, amountW, row.Height - 2),
                Theme.TextSoft, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            y += LegendRowH;
        }
        if (_hover >= 0) DrawTip(g, _mouse, _slices[_hover].Tip);
    }

    int HitTest(Point p)
    {
        if (_slices.Count == 0) return -1;
        var (ring, legend) = Geometry();
        if (legend.Contains(p))
        {
            var rows = Math.Max(1, legend.Height / LegendRowH);
            var top = legend.Y + Math.Max(0, (legend.Height - Math.Min(rows, _slices.Count) * LegendRowH) / 2);
            var i = (p.Y - top) / LegendRowH;
            return p.Y >= top && i >= 0 && i < Math.Min(rows, _slices.Count) ? i : -1;
        }
        var c = new PointF(ring.X + ring.Width / 2, ring.Y + ring.Height / 2);
        double dx = p.X - c.X, dy = p.Y - c.Y, dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > ring.Width / 2 + Px(4) || dist < ring.Width / 2 - ring.Width * 0.24f - Px(4)) return -1;
        var angle = (Math.Atan2(dy, dx) * 180 / Math.PI + 90 + 360) % 360;
        double acc = 0;
        for (var i = 0; i < _slices.Count; i++)
        {
            acc += (double)(_slices[i].Value / Total) * 360.0;
            if (angle <= acc) return i;
        }
        return _slices.Count - 1;
    }

    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); _mouse = e.Location; _hover = HitTest(e.Location); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }
}

/// <summary>Where the cheques stand: one row per status with an icon, the name, count, amount and a share bar.</summary>
public class StatusChart : ChartBase
{
    List<StatusSlice> _rows = new();
    int _hover = -1;

    public event EventHandler<ChequeState>? StateClicked;

    public void SetRows(IEnumerable<StatusSlice> rows) { _rows = rows.ToList(); Invalidate(); }

    public static (string Name, string Glyph, Color Color) Look(ChequeState s) => s switch
    {
        ChequeState.Cleared => ("Cleared", "", ChartPalette.Good),
        ChequeState.AwaitingClearing => ("Awaiting clearing", "", ChartPalette.Warning),
        ChequeState.PostDated => ("Post-dated", "", ChartPalette.PostDated),
        ChequeState.Bounced => ("Bounced", "", ChartPalette.Critical),
        ChequeState.Stopped => ("Stopped", "", ChartPalette.Serious),
        _ => ("Void", "", ChartPalette.Neutral),
    };

    int RowH => Math.Max(Px(22), Math.Min(Px(44), (Height - Px(4)) / Math.Max(1, _rows.Count)));
    bool Bars => RowH >= Px(30);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (_rows.Sum(r => r.Count) == 0) { DrawEmpty(g); return; }
        var max = Math.Max(1, _rows.Max(r => r.Count));
        using var icons = Theme.Icons(11f);
        var countW = TextRenderer.MeasureText("000 cheques", F.Ui9).Width;
        var amountW = TextRenderer.MeasureText("AED 0,000,000", F.Bold95).Width;
        for (var i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            var (name, glyph, color) = Look(r.State);
            var y = i * RowH + Px(2);
            var row = new Rectangle(0, y, Width, RowH - 2);
            if (i == _hover && r.Count > 0)
                using (var wash = new SolidBrush(ChartPalette.Wash))
                using (var path = Theme.Round(new RectangleF(row.X, row.Y, row.Width - 1, row.Height), Px(6)))
                    g.FillPath(wash, path);
            var faded = r.Count == 0;
            var th = Bars ? RowH - Px(8) : RowH - 2;
            TextRenderer.DrawText(g, glyph, icons, new Rectangle(Px(6), y, Px(22), th), faded ? Theme.BorderStrong : color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            TextRenderer.DrawText(g, name, F.Ui95, new Rectangle(Px(32), y, Width - Px(32) - countW - amountW, th),
                faded ? Theme.Muted : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, $"{r.Count} {(r.Count == 1 ? "cheque" : "cheques")}", F.Ui9,
                new Rectangle(Width - countW - amountW - Px(8), y, countW, th), Theme.TextSoft, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            TextRenderer.DrawText(g, r.Count == 0 ? "-" : "AED " + r.Amount.ToString("#,##0", CultureInfo.InvariantCulture), F.Bold95,
                new Rectangle(Width - amountW - Px(6), y, amountW, th), faded ? Theme.Muted : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            // share bar under the name
            if (!Bars) continue;
            var trackW = Width - Px(32) - Px(6);
            var track = new RectangleF(Px(32), y + RowH - Px(9), trackW, Px(4));
            using (var tb = new SolidBrush(ChartPalette.Grid)) g.FillRectangle(tb, track);
            if (r.Count > 0)
                using (var fb = new SolidBrush(color)) g.FillRectangle(fb, track.X, track.Y, Math.Max(Px(3), trackW * r.Count / (float)max), track.Height);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var i = (e.Y - Px(2)) / RowH;
        var h = i >= 0 && i < _rows.Count ? i : -1;
        Cursor = h >= 0 && _rows[h].Count > 0 && StateClicked != null ? Cursors.Hand : Cursors.Default;
        if (h != _hover) { _hover = h; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_hover >= 0 && _rows[_hover].Count > 0) StateClicked?.Invoke(this, _rows[_hover].State);
    }
}

/// <summary>The "Good to know" list: one finding per line, warnings with an amber icon. Scrolls when long.</summary>
public class InsightList : ScrollableControl
{
    List<Insight> _items = new();

    public InsightList()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
        Font = F.Ui95;
        AutoScroll = true;
    }

    float S => DeviceDpi / 96f;
    int Px(float v) => (int)Math.Round(v * S);

    public void SetItems(IEnumerable<Insight> items)
    {
        _items = items.ToList();
        UpdateScroll();
        Invalidate();
    }

    int TextWidth => Math.Max(40, ClientSize.Width - Px(34));

    int ItemHeight(Insight i) =>
        TextRenderer.MeasureText(i.Text, Font, new Size(TextWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + Px(12);

    void UpdateScroll() => AutoScrollMinSize = new Size(0, _items.Sum(ItemHeight) + Px(4));

    protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateScroll(); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        using var icons = Theme.Icons(11f);
        var y = AutoScrollPosition.Y + Px(2);
        foreach (var it in _items)
        {
            var h = ItemHeight(it);
            var warn = it.Kind == InsightKind.Warning;
            TextRenderer.DrawText(g, warn ? "" : "", icons, new Rectangle(Px(2), y + Px(2), Px(24), Px(20)),
                warn ? Theme.Warning : ChartPalette.Blue, TextFormatFlags.Left | TextFormatFlags.Top);
            TextRenderer.DrawText(g, it.Text, Font, new Rectangle(Px(30), y, TextWidth, h), Theme.Text,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Top);
            y += h;
            if (it != _items[^1])
                using (var pen = new Pen(ChartPalette.Grid)) g.DrawLine(pen, Px(30), y - Px(6), ClientSize.Width - Px(4), y - Px(6));
        }
    }
}
