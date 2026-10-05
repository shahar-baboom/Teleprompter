using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace Teleprompter
{
    /// <summary>Light / dark colour themes for the operator window.</summary>
    public static class Theme
    {
        static bool stylesLoaded;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void Apply(bool dark)
        {
            Application app = Application.Current;
            if (app == null) return;
            EnsureStyles(app);

            Set(app, "Bg",         dark ? "#202020" : "#F3F3F3");
            Set(app, "Panel",      dark ? "#2B2B2B" : "#E6E6E6");
            Set(app, "Fg",         dark ? "#EDEDED" : "#1A1A1A");
            Set(app, "Line",       dark ? "#4A4A4A" : "#BDBDBD");
            Set(app, "Btn",        dark ? "#333333" : "#FDFDFD");
            Set(app, "BtnHover",   dark ? "#3E3E3E" : "#E5F1FB");
            Set(app, "BtnPressed", dark ? "#4A4A4A" : "#CCE4F7");
            Set(app, "Accent",     dark ? "#4CA0E0" : "#0067C0");
            Set(app, "Input",      dark ? "#1B1B1B" : "#FFFFFF");
            Set(app, "Hi",         dark ? "#2F4F70" : "#CCE4F7");
            Set(app, "Popup",      dark ? "#2B2B2B" : "#FFFFFF");

            foreach (Window w in app.Windows) SetTitleBar(w, dark);
        }

        /// <summary>Windows 10/11 dark title bar.</summary>
        public static void SetTitleBar(Window w, bool dark)
        {
            try
            {
                IntPtr h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return;
                int v = dark ? 1 : 0;
                if (DwmSetWindowAttribute(h, 20, ref v, 4) != 0) DwmSetWindowAttribute(h, 19, ref v, 4);
            }
            catch { }
        }

        static void Set(Application app, string key, string hex)
        {
            SolidColorBrush b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            app.Resources[key] = b;
        }

        static void EnsureStyles(Application app)
        {
            if (stylesLoaded) return;
            stylesLoaded = true;
            try
            {
                Assembly asm = typeof(Theme).Assembly;
                string name = null;
                foreach (string n in asm.GetManifestResourceNames())
                    if (n.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase)) name = n;
                if (name == null) return;
                using (Stream s = asm.GetManifestResourceStream(name))
                {
                    ResourceDictionary d = (ResourceDictionary)XamlReader.Load(s);
                    app.Resources.MergedDictionaries.Add(d);
                }
            }
            catch (Exception ex)
            {
                // Styling is cosmetic - never block the app because of it.
                System.Diagnostics.Debug.WriteLine("Theme styles not loaded: " + ex.Message);
            }
        }
    }
}
