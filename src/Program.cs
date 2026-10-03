// AMUI - Alvin's Roblox Multi-Instance
//
// Roblox only allows one client per PC. When a client starts it claims two
// named Windows objects, and a new client that finds them taken tells the old
// client to close. AMUI claims those names first (SingletonLock.cs), so every
// client starts as if it were the only one.
//
// AMUI also lists every open client with its account, avatar and current game.
// It gets that from Roblox's own log files (ClientTracker.cs) and Roblox's
// public web API (RobloxApi.cs). It never reads or writes a client's memory;
// the only thing it ever does to a client is close it when you click its
// power button.

using System;
using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("AMUI")]
[assembly: AssemblyDescription("Alvin's Roblox Multi-Instance")]
[assembly: AssemblyProduct("AMUI")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Alvino. MIT License.")]
[assembly: AssemblyVersion("1.1.1.0")]
[assembly: AssemblyFileVersion("1.1.1.0")]
// Without this, Windows runs the app in .NET 4.0 compatibility mode, which
// means old TLS defaults and no support for the PNG frames in the icon.
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace AMUI
{
    static class Program
    {
        public const string Version = "1.1.1";
        public const string RepoUrl = "https://github.com/AlvinP11/AMUI";

        [STAThread]
        static void Main(string[] args)
        {
            bool startInTray = Array.IndexOf(args, "--tray") >= 0;

            // Also the single-instance guard: a second copy of AMUI asks the
            // first one to show its window, then exits.
            bool isFirstCopy;
            using (EventWaitHandle showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "AMUI_ShowWindow", out isFirstCopy))
            {
                if (!isFirstCopy)
                {
                    showSignal.Set();
                    return;
                }

                Native.SetProcessDPIAware();
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Theme.Init();

                MainForm form = new MainForm(startInTray);
                ThreadPool.RegisterWaitForSingleObject(showSignal,
                    delegate { form.ShowFromAnotherThread(); }, null, Timeout.Infinite, false);
                Application.Run(form);
            }
        }
    }
}
