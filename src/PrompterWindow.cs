using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace Teleprompter
{
    /// <summary>Borderless full-screen output window for the teleprompter monitor.</summary>
    public class PrompterWindow : Window
    {
        public readonly PrompterSurface Surface;
        Forms.Screen screen;
        public Action<int> WheelScrolled;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040, SWP_FRAMECHANGED = 0x0020;

        public PrompterWindow(PrompterSurface surface, Forms.Screen target)
        {
            Surface = surface;
            screen = target;
            Title = "Teleprompter - Output";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowActivated = false;
            ShowInTaskbar = false;
            Background = Brushes.Black;
            Cursor = Cursors.None;
            WindowStartupLocation = WindowStartupLocation.Manual;

            Viewbox vb = new Viewbox();
            vb.Stretch = Stretch.Uniform;
            vb.HorizontalAlignment = HorizontalAlignment.Center;
            vb.VerticalAlignment = VerticalAlignment.Center;
            vb.Child = surface;
            Content = vb;

            PlaceRoughly();
            SourceInitialized += delegate { FitToScreen(); };
            Loaded += delegate { FitToScreen(); };
            ContentRendered += delegate { FitToScreen(); };
            DpiChanged += delegate { Dispatcher.BeginInvoke(new Action(FitToScreen), System.Windows.Threading.DispatcherPriority.Background); };
            MouseWheel += delegate(object s, MouseWheelEventArgs e) { if (WheelScrolled != null) WheelScrolled(e.Delta); e.Handled = true; };
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); };
        }

        public Forms.Screen TargetScreen { get { return screen; } }

        public void MoveTo(Forms.Screen target)
        {
            screen = target;
            PlaceRoughly();
            FitToScreen();
        }

        /// <summary>Initial WPF placement (DIPs) so the window is created on the right monitor.</summary>
        void PlaceRoughly()
        {
            double scale = 1.0;
            try
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                    scale = g.DpiX / 96.0;
            }
            catch { }
            System.Drawing.Rectangle b = screen.Bounds;
            Left = b.Left / scale + 10;
            Top = b.Top / scale + 10;
            Width = Math.Max(100, b.Width / scale - 20);
            Height = Math.Max(100, b.Height / scale - 20);
        }

        /// <summary>Exact placement in physical pixels: covers the whole target monitor.</summary>
        public void FitToScreen()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            System.Drawing.Rectangle b = screen.Bounds;
            SetWindowPos(hwnd, HWND_TOPMOST, b.X, b.Y, b.Width, b.Height,
                         SWP_NOACTIVATE | SWP_FRAMECHANGED | (IsVisible ? SWP_SHOWWINDOW : 0u));
        }
    }
}
