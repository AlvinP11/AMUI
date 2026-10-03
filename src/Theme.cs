using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace AMUI
{
    static class Theme
    {
        public static readonly Color Background = Color.FromArgb(15, 17, 23);
        public static readonly Color Surface = Color.FromArgb(24, 27, 36);
        public static readonly Color SurfaceHover = Color.FromArgb(31, 35, 48);
        public static readonly Color Border = Color.FromArgb(42, 47, 61);
        public static readonly Color Text = Color.FromArgb(243, 244, 246);
        public static readonly Color TextDim = Color.FromArgb(156, 163, 175);
        public static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        public static readonly Color AccentA = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentB = Color.FromArgb(124, 58, 237);
        public static readonly Color Green = Color.FromArgb(34, 197, 94);
        public static readonly Color Amber = Color.FromArgb(245, 158, 11);
        public static readonly Color Red = Color.FromArgb(239, 68, 68);

        public static float Scale = 1f;
        public static Font Title;
        public static Font Name;
        public static Font Body;
        public static Font BodyBold;
        public static Font Small;

        public static void Init()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                Scale = g.DpiX / 96f;
            Title = new Font("Segoe UI Semibold", 14f);
            Name = new Font("Segoe UI Semibold", 10.5f);
            Body = new Font("Segoe UI", 9.5f);
            BodyBold = new Font("Segoe UI Semibold", 9.5f);
            Small = new Font("Segoe UI", 8.5f);
        }

        // Pixels at 100% display scaling -> pixels on this display.
        public static int S(float px)
        {
            return (int)Math.Round(px * Scale);
        }

        public static Color Mix(Color a, Color b, float amountOfB)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amountOfB),
                (int)(a.G + (b.G - a.G) * amountOfB),
                (int)(a.B + (b.B - a.B) * amountOfB));
        }

        public static Icon AppIcon(int size)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("AMUI.amui.ico"))
                return new Icon(s, size, size);
        }

        public static Bitmap AppLogo(int size)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("AMUI.amui.png"))
            using (Image full = Image.FromStream(s))
                return Resize(full, size);
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Bitmap Resize(Image source, int size)
        {
            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, size, size));
            }
            return result;
        }

        // Crops an image to an anti-aliased circle (radius < 0) or rounded square.
        public static Bitmap Crop(Image source, int size, Color fill, float radius)
        {
            using (Bitmap scaled = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(scaled))
                {
                    g.Clear(fill);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(source, new Rectangle(0, 0, size, size));
                }

                Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(result))
                using (TextureBrush brush = new TextureBrush(scaled))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    if (radius < 0)
                    {
                        g.FillEllipse(brush, 0, 0, size - 1, size - 1);
                    }
                    else
                    {
                        using (GraphicsPath path = RoundRect(new RectangleF(0, 0, size - 1, size - 1), radius))
                            g.FillPath(brush, path);
                    }
                }
                return result;
            }
        }

        public static void DrawPowerIcon(Graphics g, RectangleF r, Color color)
        {
            float w = Math.Max(1.5f, r.Width * 0.11f);
            using (Pen pen = new Pen(color, w))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                RectangleF ring = RectangleF.Inflate(r, -w / 2, -w / 2);
                ring.Y += r.Height * 0.06f;
                g.DrawArc(pen, ring, 305, 290);
                float cx = r.X + r.Width / 2;
                g.DrawLine(pen, cx, r.Y + w / 2, cx, r.Y + r.Height * 0.45f);
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1L << 30)
                return (bytes / (double)(1L << 30)).ToString("0.0") + " GB";
            return (bytes >> 20) + " MB";
        }

        public static string FormatUptime(TimeSpan t)
        {
            if (t.TotalMinutes < 1)
                return "Just started";
            if (t.TotalHours < 1)
                return "Up " + (int)t.TotalMinutes + "m";
            if (t.TotalDays < 1)
                return "Up " + (int)t.TotalHours + "h " + t.Minutes.ToString("00") + "m";
            return "Up " + (int)t.TotalDays + "d " + t.Hours + "h";
        }
    }
}
