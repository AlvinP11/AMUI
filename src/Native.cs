using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace AMUI
{
    static class Native
    {
        const int SW_RESTORE = 9;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_BORDER_COLOR = 34;
        const int DWMWA_CAPTION_COLOR = 35;

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        public static void BringToFront(IntPtr window)
        {
            if (window == IntPtr.Zero)
                return;
            if (IsIconic(window))
                ShowWindow(window, SW_RESTORE);
            SetForegroundWindow(window);
        }

        // Dark title bar in the app's own colors. Older Windows versions
        // ignore the attributes they don't know.
        public static void StyleTitleBar(IntPtr window, Color caption, Color border)
        {
            try
            {
                int on = 1;
                DwmSetWindowAttribute(window, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
                int captionRef = ToColorRef(caption);
                DwmSetWindowAttribute(window, DWMWA_CAPTION_COLOR, ref captionRef, sizeof(int));
                int borderRef = ToColorRef(border);
                DwmSetWindowAttribute(window, DWMWA_BORDER_COLOR, ref borderRef, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        // Dark scrollbars.
        public static void UseDarkScrollbars(IntPtr window)
        {
            try { SetWindowTheme(window, "DarkMode_Explorer", null); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        static int ToColorRef(Color c)
        {
            return c.R | (c.G << 8) | (c.B << 16);
        }
    }
}
