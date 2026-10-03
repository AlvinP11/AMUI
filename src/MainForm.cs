using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AMUI
{
    sealed class MainForm : Form
    {
        readonly SingletonLock _lock = new SingletonLock();
        readonly ClientTracker _tracker = new ClientTracker();
        readonly Dictionary<int, ClientCard> _cards = new Dictionary<int, ClientCard>();
        readonly ToolTip _tip = new ToolTip();
        readonly Timer _timer = new Timer();

        readonly BufferedPanel _header;
        readonly PillButton _closeAllButton;
        readonly PillButton _menuButton;
        readonly Banner _lockBanner;
        readonly Banner _updateBanner;
        readonly CardList _list;
        readonly BufferedPanel _footer;
        readonly NotifyIcon _tray;
        readonly Bitmap _logo;

        Rectangle _footerLink;
        string _updateUrl;
        bool _allowVisible;
        bool _exiting;

        public MainForm(bool startInTray)
        {
            _allowVisible = !startInTray;

            Text = "AMUI";
            Icon = Theme.AppIcon(Theme.S(32));
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Theme.S(480), Theme.S(600));
            MinimumSize = new Size(Theme.S(400), Theme.S(380));
            TopMost = Settings.AlwaysOnTop;
            _logo = Theme.AppLogo(Theme.S(40));

            _list = new CardList();
            _list.Dock = DockStyle.Fill;
            _list.Padding = new Padding(Theme.S(16), Theme.S(12), Theme.S(16), Theme.S(6));
            _list.Paint += PaintEmptyState;
            _list.ClientSizeChanged += delegate { SizeCards(); };
            _list.HandleCreated += delegate { Native.UseDarkScrollbars(_list.Handle); };

            _lockBanner = new Banner("Close all");
            _lockBanner.Dock = DockStyle.Top;
            _lockBanner.ButtonClick += delegate { CloseAllClients(true); };

            _updateBanner = new Banner("Download");
            _updateBanner.Dock = DockStyle.Top;
            _updateBanner.ButtonClick += delegate { OpenUrl(_updateUrl); };

            _header = new BufferedPanel();
            _header.Dock = DockStyle.Top;
            _header.Height = Theme.S(76);
            _header.Paint += PaintHeader;
            _closeAllButton = new PillButton("Close all");
            _closeAllButton.Click += delegate { CloseAllClients(true); };
            _menuButton = new PillButton(null);
            _menuButton.ShowDots = true;
            _menuButton.Click += delegate { ShowSettingsMenu(); };
            _tip.SetToolTip(_closeAllButton, "Close every Roblox client");
            _tip.SetToolTip(_menuButton, "Settings");
            _header.Controls.Add(_closeAllButton);
            _header.Controls.Add(_menuButton);
            _header.Resize += delegate { LayoutHeader(); };

            _footer = new BufferedPanel();
            _footer.Dock = DockStyle.Bottom;
            _footer.Height = Theme.S(40);
            _footer.Paint += PaintFooter;
            _footer.MouseMove += delegate(object s, MouseEventArgs e) { _footer.Cursor = _footerLink.Contains(e.Location) ? Cursors.Hand : Cursors.Default; };
            _footer.MouseClick += delegate(object s, MouseEventArgs e) { if (_footerLink.Contains(e.Location)) OpenUrl(Program.RepoUrl); };

            // Docking goes from the last-added control inwards.
            Controls.Add(_list);
            Controls.Add(_lockBanner);
            Controls.Add(_updateBanner);
            Controls.Add(_header);
            Controls.Add(_footer);

            _tray = new NotifyIcon();
            _tray.Icon = Theme.AppIcon(SystemInformation.SmallIconSize.Width);
            _tray.Text = "AMUI";
            _tray.ContextMenuStrip = BuildTrayMenu();
            _tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowMain(); };
            _tray.Visible = true;

            RobloxApi.Changed += OnApiChanged;

            if (ClientTracker.ProcessCount() == 0)
                _lock.TryAcquire();

            _timer.Interval = 1500;
            _timer.Tick += delegate { Tick(); };
            _timer.Start();
            Tick();

            if (startInTray)
                _tray.ShowBalloonTip(3000, "AMUI is running", "Click the tray icon to see your Roblox clients.", ToolTipIcon.Info);

            CheckForUpdates(false);
        }

        // --- Window visibility -------------------------------------------------

        protected override void SetVisibleCore(bool value)
        {
            if (!_allowVisible)
            {
                value = false;
                if (!IsHandleCreated)
                    CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        public void ShowMain()
        {
            _allowVisible = true;
            Show();
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
            Tick();
        }

        // Called when someone starts a second copy of AMUI.
        public void ShowFromAnotherThread()
        {
            try { BeginInvoke(new Action(ShowMain)); }
            catch (InvalidOperationException) { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.StyleTitleBar(Handle, Theme.Background, Theme.Border);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_exiting && e.CloseReason == CloseReason.UserClosing && Settings.CloseToTray)
            {
                e.Cancel = true;
                Hide();
                if (!Settings.TrayHintShown)
                {
                    _tray.ShowBalloonTip(4000, "AMUI is still running",
                        "Multi-instance stays on. Right-click the tray icon to exit.", ToolTipIcon.Info);
                    Settings.TrayHintShown = true;
                }
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            RobloxApi.Changed -= OnApiChanged;
            _tray.Visible = false;
            _tray.Dispose();
            _lock.Release();
            base.OnFormClosed(e);
        }

        void ExitApp()
        {
            _exiting = true;
            Close();
        }

        // --- Refreshing ----------------------------------------------------------

        void OnApiChanged()
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(new Action(RefreshCards));
            }
            catch (InvalidOperationException) { }
        }

        void Tick()
        {
            _tracker.Refresh();
            List<RobloxClient> clients = _tracker.Clients;

            bool wasActive = _lock.IsActive;
            if (!wasActive && clients.Count == 0 && _lock.TryAcquire())
                _tray.ShowBalloonTip(3000, "Multi-instance is on", "Open as many Roblox clients as you want.", ToolTipIcon.Info);

            SyncCards(clients);
            UpdateChrome(clients);
        }

        void SyncCards(List<RobloxClient> clients)
        {
            HashSet<int> live = new HashSet<int>();
            foreach (RobloxClient c in clients)
                live.Add(c.Pid);

            foreach (int pid in new List<int>(_cards.Keys))
            {
                if (live.Contains(pid))
                    continue;
                _cards[pid].Dispose();
                _cards.Remove(pid);
            }

            foreach (RobloxClient c in clients)
            {
                if (_cards.ContainsKey(c.Pid))
                    continue;
                ClientCard card = new ClientCard(c, _tip);
                card.PowerClick += delegate { CloseClient(card.Client); };
                card.BodyClick += delegate { Native.BringToFront(card.Client.WindowHandle); };
                card.MouseUp += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Right) ShowCardMenu(card, e.Location); };
                _cards[c.Pid] = card;
                _list.Controls.Add(card);
            }

            SizeCards();
            RefreshCards();
        }

        void SizeCards()
        {
            int width = _list.ClientSize.Width - _list.Padding.Horizontal;
            foreach (ClientCard card in _cards.Values)
            {
                if (card.Width != width)
                    card.Width = width;
            }
        }

        void RefreshCards()
        {
            foreach (ClientCard card in _cards.Values)
            {
                card.Prefetch();
                card.Invalidate();
            }
            _list.Invalidate();
        }

        void UpdateChrome(List<RobloxClient> clients)
        {
            _closeAllButton.Enabled = clients.Count > 0;

            if (_lock.IsActive)
            {
                _lockBanner.Visible = false;
            }
            else
            {
                string message = _lock.LastError == null || clients.Count > 0
                    ? "Roblox was already open, so multi-instance is paused. Close every client to turn it on."
                    : "Couldn't turn multi-instance on: " + _lock.LastError;
                _lockBanner.Set(message, Theme.Amber, clients.Count > 0 ? "Close all" : null);
                _lockBanner.Visible = true;
            }

            _tray.Text = clients.Count == 1 ? "AMUI - 1 Roblox client" : "AMUI - " + clients.Count + " Roblox clients";
            _header.Invalidate();
            _footer.Invalidate();
        }

        // --- Actions -----------------------------------------------------------

        void CloseClient(RobloxClient client)
        {
            try
            {
                using (Process p = Process.GetProcessById(client.Pid))
                    p.Kill();
            }
            catch (ArgumentException) { } // Already gone.
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't close that client: " + ex.Message, "AMUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            Tick();
        }

        void CloseAllClients(bool confirm)
        {
            List<RobloxClient> clients = _tracker.Clients;
            if (clients.Count == 0)
                return;
            if (confirm)
            {
                string question = clients.Count == 1 ? "Close the open Roblox client?" : "Close all " + clients.Count + " Roblox clients?";
                if (MessageBox.Show(this, question, "AMUI", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }
            foreach (RobloxClient c in clients)
            {
                try
                {
                    using (Process p = Process.GetProcessById(c.Pid))
                        p.Kill();
                }
                catch (Exception) { }
            }
            Tick();
        }

        static void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            try { Process.Start(url); }
            catch (Exception) { }
        }

        async void CheckForUpdates(bool manual)
        {
            UpdateInfo info;
            try
            {
                info = await Task.Run(new Func<UpdateInfo>(UpdateChecker.GetLatest));
            }
            catch (Exception ex)
            {
                if (manual)
                    MessageBox.Show(this, "Couldn't check for updates: " + ex.Message, "AMUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (info.IsNewer)
            {
                _updateUrl = info.Url;
                _updateBanner.Set("AMUI " + info.Tag + " is out. You have " + Program.Version + ".", Theme.AccentA, "Download");
                _updateBanner.Visible = true;
            }
            else if (manual)
            {
                MessageBox.Show(this, "You're on the latest version (" + Program.Version + ").", "AMUI", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // --- Menus -------------------------------------------------------------

        ContextMenuStrip BuildTrayMenu()
        {
            ContextMenuStrip menu = DarkMenuRenderer.NewMenu();
            menu.Items.Add("Show AMUI", null, delegate { ShowMain(); }).Font = Theme.BodyBold;
            ToolStripItem closeAll = menu.Items.Add("Close all Roblox clients", null, delegate { CloseAllClients(true); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit AMUI", null, delegate { ExitApp(); });
            menu.Opening += delegate { closeAll.Enabled = _tracker.Clients.Count > 0; };
            return menu;
        }

        void ShowSettingsMenu()
        {
            ContextMenuStrip menu = DarkMenuRenderer.NewMenu();

            ToolStripMenuItem onTop = new ToolStripMenuItem("Always on top");
            onTop.Checked = Settings.AlwaysOnTop;
            onTop.Click += delegate { Settings.AlwaysOnTop = TopMost = !Settings.AlwaysOnTop; };
            menu.Items.Add(onTop);

            ToolStripMenuItem startup = new ToolStripMenuItem("Start with Windows");
            startup.Checked = Settings.StartWithWindows;
            startup.Click += delegate { Settings.StartWithWindows = !Settings.StartWithWindows; };
            menu.Items.Add(startup);

            ToolStripMenuItem toTray = new ToolStripMenuItem("Keep running in the tray when closed");
            toTray.Checked = Settings.CloseToTray;
            toTray.Click += delegate { Settings.CloseToTray = !Settings.CloseToTray; };
            menu.Items.Add(toTray);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Open Roblox logs folder", null, delegate { OpenUrl(ClientTracker.LogFolder); });
            menu.Items.Add("Check for updates", null, delegate { CheckForUpdates(true); });
            menu.Items.Add("View on GitHub", null, delegate { OpenUrl(Program.RepoUrl); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit AMUI", null, delegate { ExitApp(); });

            menu.Closed += delegate { BeginInvoke(new Action(menu.Dispose)); };
            menu.Show(_menuButton, new Point(_menuButton.Width - menu.PreferredSize.Width, _menuButton.Height + Theme.S(4)));
        }

        void ShowCardMenu(ClientCard card, Point location)
        {
            RobloxClient c = card.Client;
            RobloxUser user = card.User;
            RobloxGame game = card.Game;
            ContextMenuStrip menu = DarkMenuRenderer.NewMenu();

            menu.Items.Add("Switch to this window", null, delegate { Native.BringToFront(c.WindowHandle); }).Font = Theme.BodyBold;
            menu.Items.Add(new ToolStripSeparator());

            ToolStripItem profile = menu.Items.Add("Open profile", null, delegate { OpenUrl("https://www.roblox.com/users/" + c.UserId + "/profile"); });
            profile.Enabled = c.UserId != 0;

            long placeId = game != null && game.RootPlaceId != 0 ? game.RootPlaceId : c.PlaceId;
            ToolStripItem gamePage = menu.Items.Add("Open game page", null, delegate { OpenUrl("https://www.roblox.com/games/" + placeId); });
            gamePage.Enabled = c.State == ClientState.InGame && placeId != 0;

            ToolStripItem copyName = menu.Items.Add("Copy username", null, delegate { Clipboard.SetText(user.Name); });
            copyName.Enabled = user != null && user.Name != null;

            ToolStripItem copyId = menu.Items.Add("Copy user ID", null, delegate { Clipboard.SetText(c.UserId.ToString()); });
            copyId.Enabled = c.UserId != 0;

            menu.Items.Add(new ToolStripSeparator());
            ToolStripItem close = menu.Items.Add("Close this client", null, delegate { CloseClient(c); });
            close.ForeColor = Theme.Red;

            menu.Closed += delegate { BeginInvoke(new Action(menu.Dispose)); };
            menu.Show(card, location);
        }

        // --- Painting ----------------------------------------------------------

        void LayoutHeader()
        {
            int right = _header.Width - Theme.S(16);
            int y = (_header.Height - _menuButton.Height) / 2;
            _menuButton.Location = new Point(right - _menuButton.Width, y);
            _closeAllButton.Location = new Point(_menuButton.Left - Theme.S(8) - _closeAllButton.Width, y);
        }

        void PaintHeader(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int logoY = (_header.Height - _logo.Height) / 2;
            g.DrawImage(_logo, Theme.S(16), logoY);
            int x = Theme.S(16) + _logo.Width + Theme.S(12);

            TextRenderer.DrawText(g, "AMUI", Theme.Title, new Point(x - Theme.S(2), logoY - Theme.S(4)), Theme.Text, Theme.Background);

            bool on = _lock.IsActive;
            Color color = on ? Theme.Green : Theme.Amber;
            string status = on ? "Multi-instance on" : "Multi-instance paused";
            int dot = Theme.S(8);
            int statusY = logoY + Theme.S(25);
            using (SolidBrush brush = new SolidBrush(color))
                g.FillEllipse(brush, x, statusY + Theme.S(4), dot, dot);
            TextRenderer.DrawText(g, status, Theme.Small, new Point(x + dot + Theme.S(6), statusY), Theme.TextDim, Theme.Background);

            using (Pen pen = new Pen(Theme.Border))
                g.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
        }

        void PaintFooter(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (Pen pen = new Pen(Theme.Border))
                g.DrawLine(pen, 0, 0, _footer.Width, 0);

            List<RobloxClient> clients = _tracker.Clients;
            long memory = 0;
            foreach (RobloxClient c in clients)
                memory += c.MemoryBytes;
            string stats = clients.Count == 1 ? "1 client" : clients.Count + " clients";
            if (clients.Count > 0)
                stats += "  ·  " + Theme.FormatBytes(memory) + " RAM";

            Rectangle box = new Rectangle(Theme.S(16), 1, _footer.Width - Theme.S(32), _footer.Height - 1);
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, stats, Theme.Small, box, Theme.TextDim, Theme.Background, flags);

            string link = "GitHub";
            string version = "v" + Program.Version + "  ·  ";
            Size linkSize = TextRenderer.MeasureText(g, link, Theme.Small, box.Size, flags | TextFormatFlags.NoPadding);
            Size versionSize = TextRenderer.MeasureText(g, version, Theme.Small, box.Size, flags | TextFormatFlags.NoPadding);
            _footerLink = new Rectangle(box.Right - linkSize.Width, box.Y, linkSize.Width, box.Height);
            Rectangle versionBox = new Rectangle(_footerLink.Left - versionSize.Width, box.Y, versionSize.Width, box.Height);
            TextRenderer.DrawText(g, version, Theme.Small, versionBox, Theme.TextMuted, Theme.Background, flags | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, link, Theme.Small, _footerLink, Theme.Mix(Theme.AccentA, Color.White, 0.35f), Theme.Background, flags | TextFormatFlags.NoPadding);
        }

        void PaintEmptyState(object sender, PaintEventArgs e)
        {
            if (_cards.Count > 0)
                return;
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int logoSize = Theme.S(56);
            int blockHeight = logoSize + Theme.S(16) + Theme.S(26) + Theme.S(44);
            int top = (_list.ClientSize.Height - blockHeight) / 2;
            int width = _list.ClientSize.Width;

            using (Bitmap logo = Theme.AppLogo(logoSize))
            {
                System.Drawing.Imaging.ColorMatrix faded = new System.Drawing.Imaging.ColorMatrix();
                faded.Matrix33 = 0.35f;
                using (System.Drawing.Imaging.ImageAttributes attributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    attributes.SetColorMatrix(faded);
                    g.DrawImage(logo, new Rectangle((width - logoSize) / 2, top, logoSize, logoSize), 0, 0, logoSize, logoSize, GraphicsUnit.Pixel, attributes);
                }
            }

            Rectangle titleBox = new Rectangle(0, top + logoSize + Theme.S(16), width, Theme.S(26));
            TextRenderer.DrawText(g, "No Roblox clients open", Theme.Name, titleBox, Theme.Text, Theme.Background,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            Rectangle hintBox = new Rectangle(Theme.S(32), titleBox.Bottom, width - Theme.S(64), Theme.S(44));
            TextRenderer.DrawText(g, "Launch Roblox from the website or your launcher. Every client you open shows up here.",
                Theme.Body, hintBox, Theme.TextDim, Theme.Background,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _tip.Dispose();
                _logo.Dispose();
                _lock.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
