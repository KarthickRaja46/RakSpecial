using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ChequeWriter;

/// <summary>Brand colours (from the logo), fonts and window styling shared by every window.</summary>
public static class Theme
{
    // brand
    public static readonly Color Navy = Color.FromArgb(11, 13, 18);
    public static readonly Color Navy2 = Color.FromArgb(28, 33, 44);      // hover / raised on navy
    public static readonly Color Navy3 = Color.FromArgb(44, 51, 66);
    public static readonly Color Gold = Color.FromArgb(212, 175, 106);
    public static readonly Color GoldHover = Color.FromArgb(196, 157, 85);
    public static readonly Color GoldDark = Color.FromArgb(140, 104, 40);
    public static readonly Color GoldSoft = Color.FromArgb(251, 246, 236);

    // surfaces and text
    public static readonly Color Canvas = Color.FromArgb(240, 242, 245);
    public static readonly Color Card = Color.White;
    public static readonly Color Border = Color.FromArgb(225, 228, 233);
    public static readonly Color BorderStrong = Color.FromArgb(203, 208, 216);
    public static readonly Color Text = Color.FromArgb(17, 24, 39);
    public static readonly Color Muted = Color.FromArgb(107, 114, 128);
    public static readonly Color OnNavyMuted = Color.FromArgb(160, 166, 178);
    public static readonly Color Danger = Color.FromArgb(185, 28, 28);
    public static readonly Color DangerSoft = Color.FromArgb(254, 242, 242);
    public static readonly Color DangerSoft2 = Color.FromArgb(254, 226, 226);
    public static readonly Color DangerBorder = Color.FromArgb(240, 180, 180);
    public static readonly Color TextSoft = Color.FromArgb(75, 85, 99);       // secondary text that must stay readable
    public static readonly Color Warning = Color.FromArgb(180, 83, 9);

    public static Font Ui(float size, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);

    /// <summary>Clean table look: no grid boxes, roomy rows, soft gold selection.</summary>
    public static void StyleGrid(DataGridView g)
    {
        var s = g.DeviceDpi / 96f;
        g.BackgroundColor = Card;
        g.BorderStyle = BorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.GridColor = Color.FromArgb(236, 238, 241);
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = (int)(36 * s);
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(248, 249, 250), ForeColor = TextSoft, Font = Semibold(9f),
            SelectionBackColor = Color.FromArgb(248, 249, 250), SelectionForeColor = TextSoft,
            Padding = new Padding((int)(8 * s), 0, (int)(8 * s), 0),
        };
        g.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Card, ForeColor = Text, Font = Ui(9.5f),
            SelectionBackColor = GoldSoft, SelectionForeColor = Text,
            Padding = new Padding((int)(8 * s), 0, (int)(8 * s), 0),
        };
        g.RowTemplate.Height = (int)(32 * s);
        g.RowHeadersVisible = false;
    }
    public static Font Semibold(float size) => new("Segoe UI Semibold", size);

    /// <summary>Windows 11 "Segoe Fluent Icons", or "Segoe MDL2 Assets" on Windows 10.</summary>
    public static readonly string IconFontName =
        FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    public static Font Icons(float size) => new(IconFontName, size);

    public static class Glyph
    {
        public const string Void = "", Undo = "";
        public const string Print = "", Preview = "", Page = "", Clear = "",
            Register = "", Analytics = "", Calendar = "", People = "", Help = "";
    }

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ---------------- title bar (Windows 11) ----------------

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    const int DWMWA_BORDER_COLOR = 34, DWMWA_CAPTION_COLOR = 35, DWMWA_TEXT_COLOR = 36;

    static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    /// <summary>Navy title bar with white text on Windows 11. Older Windows ignores it.</summary>
    public static void TitleBar(Form f)
    {
        void Apply()
        {
            try
            {
                int caption = ColorRef(Navy), text = ColorRef(Color.White), border = ColorRef(Navy);
                DwmSetWindowAttribute(f.Handle, DWMWA_CAPTION_COLOR, ref caption, 4);
                DwmSetWindowAttribute(f.Handle, DWMWA_TEXT_COLOR, ref text, 4);
                DwmSetWindowAttribute(f.Handle, DWMWA_BORDER_COLOR, ref border, 4);
            }
            catch { /* not available: keep the normal title bar */ }
        }
        if (f.IsHandleCreated) Apply();
        f.HandleCreated += (_, _) => Apply();
    }

    // ---------------- menus ----------------

    /// <summary>Navy menu bar with white items; light drop-down menus with a gold highlight.</summary>
    public sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        public MenuRenderer() : base(new Colors()) { RoundedEdges = false; }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.IsOnDropDown) { base.OnRenderMenuItemBackground(e); return; }
            if (e.Item.Selected || e.Item.Pressed)
            {
                using var b = new SolidBrush(Navy3);
                e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.ToolStrip is MenuStrip && !e.Item.IsOnDropDown) e.TextColor = Color.White;
            else if (e.Item.IsOnDropDown) e.TextColor = e.Item.Enabled ? Text : Muted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Muted;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown) base.OnRenderToolStripBorder(e);
        }

        sealed class Colors : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin => Navy;
            public override Color MenuStripGradientEnd => Navy;
            public override Color MenuItemSelected => GoldSoft;
            public override Color MenuItemBorder => Gold;
            public override Color MenuBorder => BorderStrong;
            public override Color MenuItemSelectedGradientBegin => GoldSoft;
            public override Color MenuItemSelectedGradientEnd => GoldSoft;
            public override Color MenuItemPressedGradientBegin => Color.White;
            public override Color MenuItemPressedGradientMiddle => Color.White;
            public override Color MenuItemPressedGradientEnd => Color.White;
            public override Color ToolStripDropDownBackground => Color.White;
            public override Color ImageMarginGradientBegin => Color.White;
            public override Color ImageMarginGradientMiddle => Color.White;
            public override Color ImageMarginGradientEnd => Color.White;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Color.White;
            public override Color StatusStripGradientBegin => Color.White;
            public override Color StatusStripGradientEnd => Color.White;
            public override Color CheckBackground => GoldSoft;
            public override Color CheckSelectedBackground => GoldSoft;
        }
    }
}
