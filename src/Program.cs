using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;

[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: AssemblyTitle("Teleprompter")]
[assembly: AssemblyProduct("Teleprompter")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Teleprompter
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                MessageBox.Show(e.Exception.Message, "Teleprompter", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Handled = true;
            };
            MainWindow w = new MainWindow();
            app.Run(w);
        }
    }
}
