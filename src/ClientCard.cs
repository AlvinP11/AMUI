using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AMUI
{
    // One row in the list: avatar, names, what the client is doing, and a
    // power button that closes it.
    sealed class ClientCard : Control
    {
        const TextFormatFlags LineFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                                          TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

        readonly ToolTip _tip;
        bool _hover;
        bool _hoverPower;

        Bitmap _avatar;
        Image _avatarSource;
        Bitmap _gameIcon;
        Image _gameIconSource;

        public readonly RobloxClient Client;
        public event EventHandler PowerClick;
        public event EventHandler BodyClick;

        public ClientCard(RobloxClient client, ToolTip tip)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Client = client;
            _tip = tip;
            Height = Theme.S(80);
            Margin = new Padding(0, 0, 0, Theme.S(10));
            Cursor = Cursors.Hand;
            _tip.SetToolTip(this, "Switch to this window");
        }

        public RobloxUser User
        {
            get { return Client.UserId == 0 ? null : RobloxApi.GetUser(Client.UserId); }
        }

        public RobloxGame Game
        {
            get { return Client.UniverseId == 0 ? null : RobloxApi.GetGame(Client.UniverseId); }
        }

        // Starts the account and game lookups without waiting for a repaint,
        // so they're ready even while AMUI sits in the tray.
        public void Prefetch()
        {
            if (Client.UserId != 0)
                RobloxApi.GetUser(Client.UserId);
            if (Client.UniverseId != 0)
                RobloxApi.GetGame(Client.UniverseId);
        }

        Rectangle PowerRect
        {
            get
            {
                int d = Theme.S(36);
                return new Rectangle(Width - Theme.S(18) - d, (Height - d) / 2, d, d);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            SetPowerHover(false);
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            SetPowerHover(PowerRect.Contains(e.Location));
            base.OnMouseMove(e);
        }

        void SetPowerHover(bool value)
        {
            if (_hoverPower == value)
                return;
            _hoverPower = value;
            _tip.SetToolTip(this, value ? "Close this client" : "Switch to this window");
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left)
                return;
            EventHandler handler = PowerRect.Contains(e.Location) ? PowerClick : BodyClick;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            Color surface = _hover ? Theme.SurfaceHover : Theme.Surface;
            using (GraphicsPath path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Theme.S(12)))
            {
                using (SolidBrush brush = new SolidBrush(surface))
                    g.FillPath(brush, path);
                using (Pen pen = new Pen(Theme.Border))
                    g.DrawPath(pen, path);
            }

            RobloxUser user = User;
            RobloxGame game = Game;

            int avatarSize = Theme.S(48);
            Rectangle avatarRect = new Rectangle(Theme.S(16), (Height - avatarSize) / 2, avatarSize, avatarSize);
            DrawAvatar(g, avatarRect, user);
            DrawStatusDot(g, avatarRect, surface);

            int left = avatarRect.Right + Theme.S(14);
            int right = PowerRect.Left - Theme.S(12);

            // Line 1: display name and @username
            string displayName = user != null && user.DisplayName != null ? user.DisplayName
                               : Client.UserId != 0 ? "Loading account..." : "Signing in...";
            Rectangle nameBox = new Rectangle(left, Theme.S(12), right - left, Theme.S(21));
            Size nameSize = TextRenderer.MeasureText(g, displayName, Theme.Name, nameBox.Size, LineFlags);
            TextRenderer.DrawText(g, displayName, Theme.Name, nameBox, Theme.Text, surface, LineFlags | TextFormatFlags.Bottom);
            if (user != null && user.Name != null && nameSize.Width + Theme.S(40) < nameBox.Width)
            {
                int x = left + nameSize.Width + Theme.S(6);
                Rectangle handleBox = new Rectangle(x, nameBox.Y, nameBox.Right - x, nameBox.Height - Theme.S(1));
                TextRenderer.DrawText(g, "@" + user.Name, Theme.Small, handleBox, Theme.TextMuted, surface, LineFlags | TextFormatFlags.Bottom);
            }

            // Line 2: what the client is doing
            Rectangle statusBox = new Rectangle(left, Theme.S(35), right - left, Theme.S(19));
            Color statusColor = Theme.TextDim;
            string status = DescribeStatus(game, ref statusColor);
            if (Client.State == ClientState.InGame && game != null && game.Icon != null)
            {
                int iconSize = Theme.S(18);
                if (_gameIconSource != game.Icon || _gameIcon == null)
                {
                    if (_gameIcon != null)
                        _gameIcon.Dispose();
                    _gameIcon = Theme.Crop(game.Icon, iconSize, Theme.Border, Theme.S(4));
                    _gameIconSource = game.Icon;
                }
                g.DrawImage(_gameIcon, statusBox.X, statusBox.Y + (statusBox.Height - iconSize) / 2);
                statusBox.X += iconSize + Theme.S(7);
                statusBox.Width -= iconSize + Theme.S(7);
            }
            TextRenderer.DrawText(g, status, Theme.Body, statusBox, statusColor, surface, LineFlags | TextFormatFlags.VerticalCenter);

            // Line 3: uptime, RAM and process ID
            string details = Theme.FormatUptime(DateTime.UtcNow - Client.StartTimeUtc) + "  ·  " +
                             Theme.FormatBytes(Client.MemoryBytes) + "  ·  PID " + Client.Pid;
            Rectangle detailBox = new Rectangle(left, Theme.S(55), right - left, Theme.S(16));
            TextRenderer.DrawText(g, details, Theme.Small, detailBox, Theme.TextMuted, surface, LineFlags | TextFormatFlags.VerticalCenter);

            DrawPowerButton(g, surface);
        }

        string DescribeStatus(RobloxGame game, ref Color color)
        {
            switch (Client.State)
            {
                case ClientState.InGame:
                    return game != null && game.Name != null ? game.Name : "In a game";
                case ClientState.Joining:
                    color = Theme.Amber;
                    return "Joining a game...";
                case ClientState.InMenus:
                    return "In the Roblox menus";
                case ClientState.Disconnected:
                    color = Theme.Red;
                    return ClientTracker.DescribeDisconnect(Client.DisconnectCode) + " (error " + Client.DisconnectCode + ")";
                default:
                    color = Theme.Amber;
                    return "Starting Roblox...";
            }
        }

        void DrawAvatar(Graphics g, Rectangle r, RobloxUser user)
        {
            Image source = user != null ? user.Headshot : null;
            if (source != null)
            {
                if (_avatarSource != source || _avatar == null)
                {
                    if (_avatar != null)
                        _avatar.Dispose();
                    _avatar = Theme.Crop(source, r.Width, Theme.Border, -1);
                    _avatarSource = source;
                }
                g.DrawImage(_avatar, r);
                return;
            }

            using (LinearGradientBrush brush = new LinearGradientBrush(r, Theme.AccentA, Theme.AccentB, 45f))
                g.FillEllipse(brush, r);
            string initial = user != null && !string.IsNullOrEmpty(user.DisplayName)
                ? user.DisplayName.Substring(0, 1).ToUpperInvariant() : "?";
            TextRenderer.DrawText(g, initial, Theme.Title, r, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        void DrawStatusDot(Graphics g, Rectangle avatar, Color surface)
        {
            Color color;
            switch (Client.State)
            {
                case ClientState.InGame: color = Theme.Green; break;
                case ClientState.InMenus: color = Theme.TextMuted; break;
                case ClientState.Disconnected: color = Theme.Red; break;
                default: color = Theme.Amber; break;
            }
            int size = Theme.S(16);
            Rectangle ring = new Rectangle(avatar.Right - size + Theme.S(2), avatar.Bottom - size + Theme.S(2), size, size);
            using (SolidBrush brush = new SolidBrush(surface))
                g.FillEllipse(brush, ring);
            ring.Inflate(-Theme.S(3), -Theme.S(3));
            using (SolidBrush brush = new SolidBrush(color))
                g.FillEllipse(brush, ring);
        }

        void DrawPowerButton(Graphics g, Color surface)
        {
            Rectangle r = PowerRect;
            Color fill = _hoverPower ? Theme.Red : _hover ? Theme.Border : Theme.SurfaceHover;
            using (SolidBrush brush = new SolidBrush(fill))
                g.FillEllipse(brush, r);
            float pad = r.Width * 0.3f;
            Theme.DrawPowerIcon(g, RectangleF.Inflate(r, -pad, -pad), _hoverPower ? Color.White : Theme.TextDim);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_avatar != null) _avatar.Dispose();
                if (_gameIcon != null) _gameIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
