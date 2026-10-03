using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AMUI
{
    class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
        }
    }

    sealed class CardList : FlowLayoutPanel
    {
        public CardList()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoScroll = true;
            BackColor = Theme.Background;
        }
    }

    sealed class PillButton : Control
    {
        bool _hover;
        bool _pressed;

        public Color Fill = Theme.SurfaceHover;
        public Color FillHover = Theme.Border;
        public Color Foreground = Theme.Text;
        public bool ShowDots;

        public PillButton(string text)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            Font = Theme.BodyBold;
            Cursor = Cursors.Hand;
            int width = string.IsNullOrEmpty(text) ? Theme.S(34) : TextRenderer.MeasureText(text, Font).Width + Theme.S(24);
            Size = new Size(width, Theme.S(34));
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = !Enabled ? Theme.Surface : _pressed ? Theme.Mix(FillHover, Color.Black, 0.15f) : _hover ? FillHover : Fill;
            Color fg = Enabled ? Foreground : Theme.TextMuted;
            using (GraphicsPath path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Theme.S(8)))
            using (SolidBrush brush = new SolidBrush(fill))
                g.FillPath(brush, path);

            if (ShowDots)
            {
                float d = Theme.S(4);
                float gap = Theme.S(4);
                float x = (Width - (d * 3 + gap * 2)) / 2f;
                float y = (Height - d) / 2f;
                using (SolidBrush brush = new SolidBrush(fg))
                {
                    for (int i = 0; i < 3; i++)
                        g.FillEllipse(brush, x + i * (d + gap), y, d, d);
                }
            }
            else
            {
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg, fill,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
        }
    }

    // A colored notice strip with a message and one button.
    sealed class Banner : BufferedPanel
    {
        readonly PillButton _button;
        Color _accent = Theme.Amber;
        string _message = "";
        bool _hasButton = true;

        public event EventHandler ButtonClick;

        public Banner(string buttonText)
        {
            _button = new PillButton(buttonText);
            _button.Click += delegate { if (ButtonClick != null) ButtonClick(this, EventArgs.Empty); };
            Controls.Add(_button);
            Height = Theme.S(66);
            Padding = new Padding(Theme.S(16), Theme.S(10), Theme.S(16), 0);
            Visible = false;
        }

        public void Set(string message, Color accent, string buttonText)
        {
            _message = message;
            _accent = accent;
            _button.Fill = Theme.Mix(Theme.Background, accent, 0.35f);
            _button.FillHover = Theme.Mix(Theme.Background, accent, 0.5f);
            if (buttonText != null)
            {
                _button.Text = buttonText;
                _button.Width = TextRenderer.MeasureText(buttonText, _button.Font).Width + Theme.S(24);
            }
            _hasButton = buttonText != null;
            _button.Visible = _hasButton;
            LayoutButton();
            Invalidate();
            _button.Invalidate();
        }

        Rectangle Box
        {
            get { return new Rectangle(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Top - Padding.Bottom); }
        }

        void LayoutButton()
        {
            Rectangle box = Box;
            _button.Location = new Point(box.Right - _button.Width - Theme.S(12), box.Top + (box.Height - _button.Height) / 2);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutButton();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle box = Box;
            Color fill = Theme.Mix(Theme.Background, _accent, 0.12f);
            using (GraphicsPath path = Theme.RoundRect(new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width - 1, box.Height - 1), Theme.S(10)))
            {
                using (SolidBrush brush = new SolidBrush(fill))
                    g.FillPath(brush, path);
                using (Pen pen = new Pen(Theme.Mix(Theme.Background, _accent, 0.4f)))
                    g.DrawPath(pen, path);
            }

            int dot = Theme.S(8);
            using (SolidBrush brush = new SolidBrush(_accent))
                g.FillEllipse(brush, box.X + Theme.S(14), box.Y + (box.Height - dot) / 2, dot, dot);

            int textLeft = box.X + Theme.S(14) + dot + Theme.S(10);
            int textRight = _hasButton ? _button.Left - Theme.S(10) : box.Right - Theme.S(12);
            Rectangle textBox = new Rectangle(textLeft, box.Y, textRight - textLeft, box.Height);
            TextRenderer.DrawText(g, _message, Theme.Body, textBox, Theme.Text, fill,
                TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.SurfaceHover; } }
        public override Color MenuItemSelected { get { return Theme.SurfaceHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.SurfaceHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.SurfaceHover; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.SurfaceHover; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.SurfaceHover; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Surface; } }
        public override Color CheckBackground { get { return Theme.AccentA; } }
        public override Color CheckSelectedBackground { get { return Theme.AccentA; } }
        public override Color CheckPressedBackground { get { return Theme.AccentA; } }
    }

    sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColors())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.TextMuted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = e.ImageRectangle;
            r.Inflate(Theme.S(1), Theme.S(1));
            using (GraphicsPath path = Theme.RoundRect(r, Theme.S(4)))
            using (SolidBrush brush = new SolidBrush(Theme.AccentA))
                g.FillPath(brush, path);
            using (Pen pen = new Pen(Color.White, Math.Max(1.5f, Theme.S(2))))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(r.X + r.Width * 0.27f, r.Y + r.Height * 0.52f),
                    new PointF(r.X + r.Width * 0.44f, r.Y + r.Height * 0.68f),
                    new PointF(r.X + r.Width * 0.74f, r.Y + r.Height * 0.34f)
                });
            }
        }

        public static ContextMenuStrip NewMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer();
            menu.BackColor = Theme.Surface;
            menu.ForeColor = Theme.Text;
            menu.Font = Theme.Body;
            menu.ShowImageMargin = true;
            return menu;
        }
    }
}
