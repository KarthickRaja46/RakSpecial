using System.Drawing.Drawing2D;
using System.Globalization;

namespace ChequeWriter;

/// <summary>Simple column chart of AED amounts with a hover tooltip. Muted bars (e.g. future months) are drawn lighter.</summary>
public class BarChart : Control
{
    public record Bar(string Label, decimal Value, string Tip, bool Muted = false);

    static readonly Color Ink = Color.FromArgb(0, 102, 204);
    static readonly Color InkHover = Color.FromArgb(0, 74, 153);
    static readonly Color InkMuted = Color.FromArgb(150, 190, 235);
    static readonly Color Grid = Color.FromArgb(232, 232, 232);

    List<Bar> _bars = new();
    int _hover = -1;
    readonly ToolTip _tip = new() { InitialDelay = 0, ReshowDelay = 0 };

    public string EmptyText { get; set; } = "No cheques in this period.";

    public BarChart()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = SystemColors.Window;
        Font = new Font("Segoe UI", 8.5f);
    }

    public void SetBars(IEnumerable<Bar> bars)
    {
        _bars = bars.ToList();
        _hover = -1;
        _tip.Hide(this);
        Invalidate();
    }

    Rectangle Plot => new(64, 14, Math.Max(0, Width - 64 - 14), Math.Max(0, Height - 14 - 30));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        var plot = Plot;
        if (_bars.Count == 0 || _bars.All(b => b.Value == 0) || plot.Width < 30 || plot.Height < 30)
        {
            TextRenderer.DrawText(g, EmptyText, Font, ClientRectangle, Color.Gray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var top = NiceCeiling(_bars.Max(b => b.Value));
        using (var pen = new Pen(Grid))
        {
            for (var i = 0; i <= 4; i++)
            {
                var v = top * i / 4;
                var y = plot.Bottom - (int)Math.Round((double)(v / top) * plot.Height);
                g.DrawLine(pen, plot.Left, y, plot.Right, y);
                TextRenderer.DrawText(g, Short(v), Font, new Rectangle(0, y - 9, plot.Left - 8, 18), Color.Gray,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        var slot = plot.Width / (float)_bars.Count;
        var bw = Math.Clamp(slot * 0.68f, 2f, 56f);
        var labelW = TextRenderer.MeasureText("Mmm 00", Font).Width + 6;
        var every = Math.Max(1, (int)Math.Ceiling(labelW / slot));
        var showValues = bw >= 40;

        for (var i = 0; i < _bars.Count; i++)
        {
            var b = _bars[i];
            var x = plot.Left + slot * i + (slot - bw) / 2;
            var h = (float)((double)(b.Value / top) * plot.Height);
            if (b.Value > 0 && h < 1) h = 1;
            using (var brush = new SolidBrush(i == _hover ? InkHover : b.Muted ? InkMuted : Ink))
                g.FillRectangle(brush, x, plot.Bottom - h, bw, h);

            if (showValues && b.Value > 0)
                TextRenderer.DrawText(g, Short(b.Value), Font,
                    new Rectangle((int)(plot.Left + slot * i), (int)(plot.Bottom - h) - 18, (int)slot, 16), Color.DimGray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom);
            if (i % every == 0)
                TextRenderer.DrawText(g, b.Label, Font,
                    new Rectangle((int)(plot.Left + slot * i + slot / 2 - labelW), plot.Bottom + 4, labelW * 2, 18), Color.DimGray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var plot = Plot;
        var i = -1;
        if (_bars.Count > 0 && plot.Width > 0 && e.X >= plot.Left && e.X < plot.Right && e.Y >= plot.Top && e.Y <= plot.Bottom + 20)
            i = Math.Min(_bars.Count - 1, (int)((e.X - plot.Left) / (plot.Width / (float)_bars.Count)));
        if (i == _hover) return;
        _hover = i;
        Invalidate();
        if (i < 0) _tip.Hide(this);
        else _tip.Show(_bars[i].Tip, this, e.X + 14, e.Y + 14);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        _tip.Hide(this);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
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

    static string Short(decimal v) =>
        v >= 1_000_000 ? (v / 1_000_000m).ToString("0.##", CultureInfo.InvariantCulture) + "M"
        : v >= 1_000 ? (v / 1000m).ToString("0.#", CultureInfo.InvariantCulture) + "K"
        : Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
}
