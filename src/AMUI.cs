// AMUI - Alvin's Roblox Multi-Instance
//
// Roblox only allows one client per PC. When a client starts it claims a
// named Windows object, and a new client that finds that name already taken
// tells the old client to close. AMUI claims those names first and holds on
// to them, so every client starts as if it were the only one.
//
// AMUI never opens, reads, writes or injects into the Roblox process. It only
// creates two named objects in Windows and keeps them alive until you exit.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("AMUI")]
[assembly: AssemblyDescription("Alvin's Roblox Multi-Instance")]
[assembly: AssemblyProduct("AMUI")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Alvino. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace AMUI
{
    static class Program
    {
        const string Version = "1.0.0";
        const string RepoUrl = "https://github.com/AlvinP11/AMUI";

        // Roblox's single-instance mutex.
        const string SingletonMutexName = "ROBLOX_singletonMutex";
        // The event a new client fires to tell the old one to close. Holding a
        // mutex under the same name means Roblox can't create the event.
        const string SingletonEventName = "ROBLOX_singletonEvent";

        const string RobloxProcessName = "RobloxPlayerBeta";

        static Mutex _selfGuard;
        static Mutex _singletonMutex;
        static Mutex _singletonEvent;
        static NotifyIcon _tray;
        static ToolStripMenuItem _clientCountItem;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool isFirstCopy;
            _selfGuard = new Mutex(true, "AMUI_SelfGuard", out isFirstCopy);
            if (!isFirstCopy)
            {
                MessageBox.Show("AMUI is already running. Look for its icon in the system tray.",
                    "AMUI", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!WaitForRobloxToClose())
                return;

            if (!ClaimSingletonNames())
                return;

            CreateTrayIcon();
            Application.Run();

            _tray.Visible = false;
            _tray.Dispose();
            ReleaseAll();
        }

        // If Roblox is already open it owns the names, so we can't claim them.
        static bool WaitForRobloxToClose()
        {
            while (CountRobloxClients() > 0)
            {
                DialogResult result = MessageBox.Show(
                    "Roblox is already open.\n\nClose every Roblox window, then click Retry.",
                    "AMUI", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                if (result != DialogResult.Retry)
                    return false;
            }
            return true;
        }

        static bool ClaimSingletonNames()
        {
            try
            {
                _singletonMutex = new Mutex(true, SingletonMutexName);
                _singletonEvent = new Mutex(true, SingletonEventName);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "AMUI couldn't take over Roblox's single-instance lock.\n\n" +
                    "Make sure Roblox is fully closed and try again.\n\nDetails: " + ex.Message,
                    "AMUI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ReleaseAll();
                return false;
            }
        }

        static void CreateTrayIcon()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem header = new ToolStripMenuItem("AMUI v" + Version + "  -  Multi-instance ON");
            header.Enabled = false;
            menu.Items.Add(header);

            _clientCountItem = new ToolStripMenuItem();
            _clientCountItem.Enabled = false;
            menu.Items.Add(_clientCountItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("View on GitHub", null, delegate { Process.Start(RepoUrl); });
            menu.Items.Add("Exit (turns multi-instance off)", null, delegate { Application.Exit(); });

            menu.Opening += delegate { UpdateClientCount(); };
            UpdateClientCount();

            _tray = new NotifyIcon();
            _tray.Icon = LoadAppIcon();
            _tray.Text = "AMUI - Multi-instance ON";
            _tray.ContextMenuStrip = menu;
            _tray.Visible = true;
            _tray.ShowBalloonTip(5000, "AMUI is on",
                "Open as many Roblox clients as you want. Right-click this icon to turn it off.",
                ToolTipIcon.Info);
        }

        static void UpdateClientCount()
        {
            int count = CountRobloxClients();
            _clientCountItem.Text = "Roblox clients open: " + count;
        }

        static int CountRobloxClients()
        {
            Process[] clients = Process.GetProcessesByName(RobloxProcessName);
            foreach (Process p in clients)
                p.Dispose();
            return clients.Length;
        }

        static Icon LoadAppIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        static void ReleaseAll()
        {
            Release(ref _singletonEvent);
            Release(ref _singletonMutex);
            Release(ref _selfGuard);
        }

        static void Release(ref Mutex mutex)
        {
            if (mutex == null)
                return;
            try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
            mutex.Dispose();
            mutex = null;
        }
    }
}
