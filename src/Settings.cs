using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AMUI
{
    // Stored in HKEY_CURRENT_USER\Software\AMUI.
    static class Settings
    {
        const string AppKey = @"Software\AMUI";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "AMUI";

        public static bool AlwaysOnTop
        {
            get { return Read("AlwaysOnTop", false); }
            set { Write("AlwaysOnTop", value); }
        }

        public static bool CloseToTray
        {
            get { return Read("CloseToTray", true); }
            set { Write("CloseToTray", value); }
        }

        public static bool TrayHintShown
        {
            get { return Read("TrayHintShown", false); }
            set { Write("TrayHintShown", value); }
        }

        public static bool StartWithWindows
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key != null && key.GetValue(RunValue) != null;
            }
            set
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value)
                        key.SetValue(RunValue, "\"" + Application.ExecutablePath + "\" --tray");
                    else
                        key.DeleteValue(RunValue, false);
                }
            }
        }

        static bool Read(string name, bool fallback)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppKey))
                {
                    object value = key == null ? null : key.GetValue(name);
                    return value == null ? fallback : Convert.ToInt32(value) != 0;
                }
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        static void Write(string name, bool value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AppKey))
                    key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
            }
            catch (Exception) { }
        }
    }
}
