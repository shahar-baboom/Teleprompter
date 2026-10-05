using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace Teleprompter
{
    /// <summary>Operator window: script editor, live preview and all controls.</summary>
    public class MainWindow : Window
    {
        readonly Settings S;

        // UI
        TextBox editor;
        PrompterSurface preview;
        PrompterWindow prompter;
        PrompterSurface prompterSurface;
        ComboBox monitorCombo, fontCombo;
        Slider sizeSlider, spacingSlider, marginSlider, speedSlider, markerSlider, thresholdSlider, holdSlider;
        RadioButton alignLeft, alignCenter, alignRight, dirRtl, dirLtr;
        CheckBox mirrorChk, flipChk, cueChk, darkChk, voiceChk;
        ComboBox micCombo;
        Border meterBar, meterThreshold;
        const double MeterWidth = 140, MeterMinDb = -70, MeterMaxDb = 0;
        Button showBtn, playBtn, pauseBtn, stopBtn;
        TextBlock statusText;
        Border previewFrame;

        // Scrolling state (virtual units)
        double pos, target;
        bool playing;
        double lastFrameTime = -1;
        bool loading = true;

        // Voice-activated scrolling
        readonly MicMonitor mic = new MicMonitor();
        double voiceGain = 1;

        public MainWindow()
        {
            S = Settings.Load();
            Title = "Teleprompter";
            Width = 1280;
            Height = 800;
            MinWidth = 900;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Theme.Apply(S.Dark);
            SetResourceReference(ForegroundProperty, "Fg");
            SetResourceReference(BackgroundProperty, "Bg");
            SourceInitialized += delegate { Theme.SetTitleBar(this, S.Dark); };

            BuildUi();
            RefreshMonitors();
            string last = Settings.LoadScript();
            editor.Text = last ?? "ברוכים הבאים לטלפרומפטר.\nזו שורה עם English בתוך משפט בעברית, ומספרים כמו 2026 או 3.5% או 054-1234567.\n\nWelcome to the teleprompter. Paste or type your script here.";
            loading = false;
            OnMonitorChanged();
            ApplyAll();

            System.Windows.Media.CompositionTarget.Rendering += OnFrame;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            Closing += delegate
            {
                Settings.SaveScript(editor.Text);
                S.Save();
            };
            Closed += delegate
            {
                System.Windows.Media.CompositionTarget.Rendering -= OnFrame;
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                mic.Dispose();
                if (prompter != null) prompter.Close();
            };
        }

        // ------------------------------------------------------------------ UI construction

        void BuildUi()
        {
            DockPanel root = new DockPanel();
            root.SetResourceReference(Panel.BackgroundProperty, "Bg");

            // ---- toolbar
            WrapPanel bar = new WrapPanel();
            bar.Margin = new Thickness(6, 4, 6, 2);
            DockPanel.SetDock(bar, Dock.Top);
            root.Children.Add(bar);

            // Display group
            monitorCombo = new ComboBox(); monitorCombo.MinWidth = 210;
            monitorCombo.SelectionChanged += delegate { OnMonitorChanged(); };
            Button refresh = SmallButton("↻", "Refresh monitor list");
            refresh.Click += delegate { RefreshMonitors(); };
            showBtn = new Button(); showBtn.Padding = new Thickness(10, 3, 10, 3); showBtn.Margin = new Thickness(6, 0, 0, 0);
            showBtn.FontWeight = FontWeights.SemiBold;
            showBtn.Click += delegate { TogglePrompter(); };
            mirrorChk = Check("Mirror", S.Mirror); flipChk = Check("Flip vertical", S.Flip);
            mirrorChk.ToolTip = "Mirror left/right on the prompter monitor only";
            flipChk.ToolTip = "Flip upside-down on the prompter monitor only";
            bar.Children.Add(Group("Prompter monitor", monitorCombo, refresh, showBtn, Spacer(10), mirrorChk, flipChk));

            // Text group
            fontCombo = new ComboBox(); fontCombo.Width = 160;
            fontCombo.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
            foreach (string f in Fonts.SystemFontFamilies.Select(x => x.Source).OrderBy(x => x)) fontCombo.Items.Add(f);
            fontCombo.SelectedItem = fontCombo.Items.Contains(S.FontFamily) ? S.FontFamily : (fontCombo.Items.Contains("Arial") ? "Arial" : null);
            fontCombo.SelectionChanged += delegate { ApplyAll(); };
            sizeSlider = SliderWithValue(20, 300, S.FontSize, 1, "0");
            spacingSlider = SliderWithValue(1.0, 2.5, S.LineSpacing, 0.05, "0.00");
            marginSlider = SliderWithValue(0, 700, S.Margin, 5, "0");
            bar.Children.Add(Group("Text",
                Lbl("Font"), fontCombo,
                Lbl("Size"), Wrap(sizeSlider, 110),
                Lbl("Line spacing"), Wrap(spacingSlider, 80),
                Lbl("Side margin"), Wrap(marginSlider, 90)));

            // Reading marker
            cueChk = Check("Show", S.Cue);
            markerSlider = SliderWithValue(10, 80, S.ReadingLine * 100, 1, "0'%'");
            markerSlider.ToolTip = "Height of the reading line, measured from the top of the screen";
            bar.Children.Add(Group("Reading marker", cueChk, Lbl("Position"), Wrap(markerSlider, 110)));

            // Alignment
            alignLeft = Radio("Left", "align"); alignCenter = Radio("Center", "align"); alignRight = Radio("Right", "align");
            if (S.Align == "Left") alignLeft.IsChecked = true; else if (S.Align == "Right") alignRight.IsChecked = true; else alignCenter.IsChecked = true;
            bar.Children.Add(Group("Alignment", alignLeft, alignCenter, alignRight));

            // Direction (always starts RTL)
            dirRtl = Radio("RTL  (עברית)", "dir"); dirLtr = Radio("LTR", "dir");
            dirRtl.IsChecked = true;
            bar.Children.Add(Group("Direction", dirRtl, dirLtr));

            // Playback
            playBtn = BigButton("▶  Play"); pauseBtn = BigButton("❚❚  Pause"); stopBtn = BigButton("■  Stop");
            playBtn.Click += delegate { Play(); };
            pauseBtn.Click += delegate { Pause(); };
            stopBtn.Click += delegate { StopToStart(); };
            stopBtn.ToolTip = "Stop and return to the start of the script";
            speedSlider = SliderWithValue(5, 500, S.Speed, 1, "0");
            bar.Children.Add(Group("Playback", playBtn, pauseBtn, stopBtn, Spacer(10), Lbl("Speed"), Wrap(speedSlider, 200)));

            // File
            Button openBtn = SmallButton("Open…", "Open a text file (UTF-8 or Hebrew Windows-1255)");
            Button saveBtn = SmallButton("Save…", "Save the script as UTF-8 text");
            openBtn.Click += delegate { OpenFile(); };
            saveBtn.Click += delegate { SaveFile(); };
            bar.Children.Add(Group("Script file", openBtn, saveBtn));

            // Voice control
            voiceChk = Check("Move only while I speak", false);
            voiceChk.ToolTip = "Voice-activated scrolling: after pressing Play, the text moves at the set speed while you talk and stops when you pause.";
            micCombo = new ComboBox(); micCombo.Width = 190;
            micCombo.Items.Add("Default microphone");
            List<string> mics = MicMonitor.DeviceNames();
            foreach (string m in mics) micCombo.Items.Add(m);
            micCombo.SelectedIndex = Math.Max(0, micCombo.Items.IndexOf(S.Mic));
            thresholdSlider = SliderWithValue(-70, -10, S.VoiceThreshold, 1, "0' dB'");
            thresholdSlider.ToolTip = "Voice threshold: sounds louder than this count as speech. Lower = more sensitive.";
            holdSlider = SliderWithValue(0.2, 3, S.VoiceHold, 0.1, "0.0's'");
            holdSlider.ToolTip = "How long the text keeps moving after you stop speaking";
            Grid meter = new Grid(); meter.Width = MeterWidth; meter.Height = 14;
            Border meterBg = new Border(); meterBg.SetResourceReference(Border.BackgroundProperty, "Input");
            meterBg.SetResourceReference(Border.BorderBrushProperty, "Line"); meterBg.BorderThickness = new Thickness(1);
            meterBar = new Border(); meterBar.HorizontalAlignment = HorizontalAlignment.Left; meterBar.Width = 0; meterBar.Margin = new Thickness(1);
            meterBar.Background = Brushes.Gray;
            meterThreshold = new Border(); meterThreshold.HorizontalAlignment = HorizontalAlignment.Left; meterThreshold.Width = 2;
            meterThreshold.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x00));
            meter.Children.Add(meterBg); meter.Children.Add(meterBar); meter.Children.Add(meterThreshold);
            meter.ToolTip = "Microphone level (orange line = threshold)";
            bar.Children.Add(Group("Voice control", voiceChk, micCombo, meter,
                Lbl("Threshold"), Wrap(thresholdSlider, 90), Lbl("Hold"), Wrap(holdSlider, 70)));

            // Interface
            darkChk = Check("Dark mode", S.Dark);
            darkChk.Click += delegate { S.Dark = darkChk.IsChecked == true; Theme.Apply(S.Dark); };
            bar.Children.Add(Group("Interface", darkChk));

            // ---- status bar
            Border status = new Border();
            status.Padding = new Thickness(10, 4, 10, 4);
            status.SetResourceReference(Border.BackgroundProperty, "Panel");
            statusText = new TextBlock();
            status.Child = statusText;
            DockPanel.SetDock(status, Dock.Bottom);
            root.Children.Add(status);

            // ---- main area: editor | splitter | preview
            Grid main = new Grid();
            main.Margin = new Thickness(8, 4, 8, 8);
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 250 });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 250 });
            main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock h1 = Header("Script (edit here)");
            TextBlock h2 = Header("Operator preview  —  mouse wheel scrolls the prompter");
            Grid.SetColumn(h1, 0); Grid.SetColumn(h2, 2);
            main.Children.Add(h1); main.Children.Add(h2);

            editor = new TextBox();
            editor.AcceptsReturn = true;
            editor.AcceptsTab = true;
            editor.TextWrapping = TextWrapping.Wrap;
            editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            editor.FontSize = 22;
            editor.Padding = new Thickness(10);
            editor.FlowDirection = FlowDirection.RightToLeft;
            SpellCheck.SetIsEnabled(editor, false);
            editor.TextChanged += delegate { if (!loading) PushText(); };
            Grid.SetRow(editor, 1);
            main.Children.Add(editor);

            GridSplitter split = new GridSplitter();
            split.Width = 8; split.HorizontalAlignment = HorizontalAlignment.Stretch;
            split.Background = Brushes.Transparent;
            Grid.SetColumn(split, 1); Grid.SetRow(split, 1);
            main.Children.Add(split);

            preview = new PrompterSurface();
            Viewbox vb = new Viewbox();
            vb.Stretch = Stretch.Uniform;
            vb.VerticalAlignment = VerticalAlignment.Top;
            vb.Child = preview;
            previewFrame = new Border();
            previewFrame.Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
            previewFrame.BorderBrush = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
            previewFrame.BorderThickness = new Thickness(1);
            previewFrame.Padding = new Thickness(4);
            previewFrame.Child = vb;
            previewFrame.MouseWheel += delegate(object s, MouseWheelEventArgs e) { WheelScroll(e.Delta); e.Handled = true; };
            preview.SizeChanged += delegate { ClampTarget(); };
            Grid.SetColumn(previewFrame, 2); Grid.SetRow(previewFrame, 1);
            main.Children.Add(previewFrame);

            root.Children.Add(main);
            Content = root;

            // change handlers (after all controls exist)
            RoutedEventHandler any = delegate { ApplyAll(); };
            mirrorChk.Click += any; flipChk.Click += any; cueChk.Click += any;
            alignLeft.Checked += any; alignCenter.Checked += any; alignRight.Checked += any;
            dirRtl.Checked += any; dirLtr.Checked += any;
            RoutedPropertyChangedEventHandler<double> sl = delegate { ApplyAll(); };
            sizeSlider.ValueChanged += sl; spacingSlider.ValueChanged += sl; marginSlider.ValueChanged += sl; markerSlider.ValueChanged += sl;
            voiceChk.Click += delegate { UpdateVoice(); };
            micCombo.SelectionChanged += delegate { S.Mic = micCombo.SelectedIndex > 0 ? (string)micCombo.SelectedItem : ""; if (mic.IsRunning) UpdateVoice(); };
            thresholdSlider.ValueChanged += delegate { S.VoiceThreshold = thresholdSlider.Value; mic.ThresholdDb = S.VoiceThreshold; };
            holdSlider.ValueChanged += delegate { S.VoiceHold = holdSlider.Value; mic.HangoverMs = (int)(S.VoiceHold * 1000); };
            mic.ThresholdDb = S.VoiceThreshold;
            mic.HangoverMs = (int)(S.VoiceHold * 1000);
            speedSlider.ValueChanged += delegate { S.Speed = speedSlider.Value; };
        }

        static GroupBox Group(string header, params UIElement[] items)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            foreach (UIElement e in items)
            {
                FrameworkElement fe = e as FrameworkElement;
                if (fe != null) { fe.VerticalAlignment = VerticalAlignment.Center; if (fe.Margin == new Thickness(0)) fe.Margin = new Thickness(3, 0, 3, 0); }
                sp.Children.Add(e);
            }
            GroupBox g = new GroupBox();
            g.Header = header;
            g.Content = sp;
            g.Margin = new Thickness(0, 0, 8, 4);
            g.Padding = new Thickness(4, 4, 4, 4);
            return g;
        }

        static TextBlock Lbl(string t) { TextBlock b = new TextBlock(); b.Text = t; b.Margin = new Thickness(8, 0, 2, 0); return b; }
        static TextBlock Header(string t) { TextBlock b = new TextBlock(); b.Text = t; b.FontWeight = FontWeights.SemiBold; b.Margin = new Thickness(2, 0, 0, 4); return b; }
        static FrameworkElement Spacer(double w) { Border b = new Border(); b.Width = w; return b; }
        static CheckBox Check(string t, bool v) { CheckBox c = new CheckBox(); c.Content = t; c.IsChecked = v; c.Margin = new Thickness(6, 0, 6, 0); return c; }
        static RadioButton Radio(string t, string group) { RadioButton r = new RadioButton(); r.Content = t; r.GroupName = group; r.Margin = new Thickness(4, 0, 6, 0); return r; }
        static Button SmallButton(string t, string tip) { Button b = new Button(); b.Content = t; b.ToolTip = tip; b.Padding = new Thickness(8, 2, 8, 2); b.Margin = new Thickness(4, 0, 0, 0); return b; }
        static Button BigButton(string t) { Button b = new Button(); b.Content = t; b.MinWidth = 82; b.Padding = new Thickness(10, 4, 10, 4); b.Margin = new Thickness(3, 0, 3, 0); b.FontSize = 14; return b; }

        static Slider SliderWithValue(double min, double max, double val, double step, string fmt)
        {
            Slider s = new Slider();
            s.Minimum = min; s.Maximum = max;
            s.Value = Math.Max(min, Math.Min(max, val));
            s.SmallChange = step; s.LargeChange = step * 10;
            s.IsSnapToTickEnabled = true; s.TickFrequency = step;
            s.Tag = fmt;
            return s;
        }

        /// <summary>Slider + live value label.</summary>
        static FrameworkElement Wrap(Slider s, double width)
        {
            s.Width = width;
            s.VerticalAlignment = VerticalAlignment.Center;
            TextBlock v = new TextBlock();
            v.MinWidth = 34; v.Margin = new Thickness(4, 0, 0, 0); v.VerticalAlignment = VerticalAlignment.Center;
            string fmt = (string)s.Tag;
            v.Text = s.Value.ToString(fmt);
            s.ValueChanged += delegate { v.Text = s.Value.ToString(fmt); };
            StackPanel sp = new StackPanel(); sp.Orientation = Orientation.Horizontal;
            sp.Children.Add(s); sp.Children.Add(v);
            return sp;
        }

        // ------------------------------------------------------------------ settings -> view

        void ApplyAll()
        {
            if (loading || editor == null) return;
            S.FontFamily = fontCombo.SelectedItem as string ?? "Arial";
            S.FontSize = sizeSlider.Value;
            S.LineSpacing = spacingSlider.Value;
            S.Margin = marginSlider.Value;
            S.Align = alignLeft.IsChecked == true ? "Left" : alignRight.IsChecked == true ? "Right" : "Center";
            S.Rtl = dirRtl.IsChecked == true;
            S.Mirror = mirrorChk.IsChecked == true;
            S.Flip = flipChk.IsChecked == true;
            S.Cue = cueChk.IsChecked == true;
            S.Speed = speedSlider.Value;
            S.ReadingLine = markerSlider.Value / 100.0;

            FontFamily ff = new FontFamily(S.FontFamily);
            TextAlignment ta = MapAlignment(S.Align, S.Rtl);

            editor.FlowDirection = S.Rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            editor.Language = System.Windows.Markup.XmlLanguage.GetLanguage(S.Rtl ? "he-IL" : "en-US");
            editor.TextAlignment = ta;
            editor.FontFamily = ff;

            foreach (PrompterSurface p in Surfaces())
                p.ApplyStyle(ff, S.FontSize, S.LineSpacing, ta, S.Rtl, S.Margin, S.Cue, S.ReadingLine);
            // The operator preview is never mirrored/flipped – it reads like the script window.
            preview.SetOrientation(false, false);
            if (prompterSurface != null) prompterSurface.SetOrientation(S.Mirror, S.Flip);

            PushText();
            UpdateButtons();
        }

        /// <summary>
        /// WPF interprets TextAlignment.Left/Right relative to the flow direction (Left = "start").
        /// The UI offers absolute on-screen positions, so swap them in RTL mode.
        /// </summary>
        static TextAlignment MapAlignment(string align, bool rtl)
        {
            if (align == "Center") return TextAlignment.Center;
            bool left = align == "Left";
            if (rtl) left = !left;
            return left ? TextAlignment.Left : TextAlignment.Right;
        }

        void PushText()
        {
            foreach (PrompterSurface p in Surfaces()) p.SetText(editor.Text);
            ClampTarget();
        }

        IEnumerable<PrompterSurface> Surfaces()
        {
            yield return preview;
            if (prompterSurface != null) yield return prompterSurface;
        }

        // ------------------------------------------------------------------ monitors

        class MonitorItem
        {
            public Forms.Screen Screen;
            public string Label;
            public override string ToString() { return Label; }
        }

        void RefreshMonitors()
        {
            string wanted = SelectedScreen() != null ? SelectedScreen().DeviceName : S.Monitor;
            bool wasLoading = loading;
            loading = true;
            monitorCombo.Items.Clear();
            Forms.Screen[] all = Forms.Screen.AllScreens;
            for (int i = 0; i < all.Length; i++)
            {
                Forms.Screen sc = all[i];
                MonitorItem mi = new MonitorItem();
                mi.Screen = sc;
                mi.Label = string.Format("Monitor {0}: {1}×{2}{3}", i + 1, sc.Bounds.Width, sc.Bounds.Height, sc.Primary ? "  (main)" : "");
                monitorCombo.Items.Add(mi);
            }
            int sel = -1;
            for (int i = 0; i < monitorCombo.Items.Count; i++)
                if (((MonitorItem)monitorCombo.Items[i]).Screen.DeviceName == wanted) sel = i;
            if (sel < 0)
                for (int i = 0; i < monitorCombo.Items.Count; i++)
                    if (!((MonitorItem)monitorCombo.Items[i]).Screen.Primary) { sel = i; break; }
            if (sel < 0 && monitorCombo.Items.Count > 0) sel = 0;
            monitorCombo.SelectedIndex = sel;
            loading = wasLoading;
            OnMonitorChanged();
        }

        Forms.Screen SelectedScreen()
        {
            MonitorItem mi = monitorCombo == null ? null : monitorCombo.SelectedItem as MonitorItem;
            return mi == null ? null : mi.Screen;
        }

        void OnMonitorChanged()
        {
            if (loading) return;
            Forms.Screen sc = SelectedScreen();
            if (sc == null) return;
            S.Monitor = sc.DeviceName;
            double aspect = (double)sc.Bounds.Height / Math.Max(1, sc.Bounds.Width);
            preview.SetAspect(aspect);
            if (prompterSurface != null) prompterSurface.SetAspect(aspect);
            if (prompter != null) prompter.MoveTo(sc);
            ClampTarget();
            UpdateButtons();
        }

        void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshMonitors), System.Windows.Threading.DispatcherPriority.Background);
        }

        void TogglePrompter()
        {
            if (prompter != null) { prompter.Close(); return; }
            Forms.Screen sc = SelectedScreen();
            if (sc == null) return;
            prompterSurface = new PrompterSurface();
            prompterSurface.SetAspect((double)sc.Bounds.Height / Math.Max(1, sc.Bounds.Width));
            prompter = new PrompterWindow(prompterSurface, sc);
            prompter.WheelScrolled = WheelScroll;
            prompter.Closed += delegate
            {
                prompter = null;
                prompterSurface = null;
                UpdateButtons();
            };
            ApplyAll();
            prompterSurface.SetOffset(pos);
            prompter.Show();
            UpdateButtons();
            Activate();
        }

        // ------------------------------------------------------------------ scrolling

        void Play() { playing = true; UpdateButtons(); }
        void Pause() { playing = false; UpdateButtons(); }
        void StopToStart() { playing = false; target = 0; UpdateButtons(); }

        void WheelScroll(int delta)
        {
            // Wheel down (negative delta) moves the script forward, exactly like the script window.
            double step = S.FontSize * S.LineSpacing;
            target -= delta / 120.0 * step;
            ClampTarget();
        }

        void ClampTarget()
        {
            if (preview == null) return;
            double max = preview.MaxOffset;
            if (target > max) target = max;
            if (target < 0) target = 0;
        }

        void OnFrame(object sender, EventArgs e)
        {
            RenderingEventArgs re = e as RenderingEventArgs;
            double now = re != null ? re.RenderingTime.TotalSeconds : Environment.TickCount / 1000.0;
            if (now == lastFrameTime) return;           // same frame reported twice
            double dt = lastFrameTime < 0 ? 0 : now - lastFrameTime;
            lastFrameTime = now;
            if (dt > 0.1) dt = 0.1;

            // Voice control: speed is multiplied by a gain that eases to 1 while speaking and to 0 in silence.
            double gainTarget = mic.IsRunning ? (mic.Speaking ? 1.0 : 0.0) : 1.0;
            voiceGain += (gainTarget - voiceGain) * (1 - Math.Exp(-dt * 8));
            UpdateMeter();

            if (playing)
            {
                target += S.Speed * dt * voiceGain;
                double max = preview.MaxOffset;
                if (target >= max) { target = max; playing = false; UpdateButtons(); }
            }
            ClampTarget();

            // Exponential smoothing towards the target gives smooth wheel steps, smooth start/stop
            // and an exact, constant speed while playing.
            double diff = target - pos;
            if (Math.Abs(diff) < 0.02) pos = target;
            else pos += diff * (1 - Math.Exp(-dt * 12));

            preview.SetOffset(pos);
            if (prompterSurface != null) prompterSurface.SetOffset(pos);

            UpdateStatus();
        }

        string lastStatus;
        void UpdateStatus()
        {
            double max = preview.MaxOffset;
            int pct = max <= 0 ? 0 : (int)Math.Round(100 * pos / max);
            string st = playing ? "Playing" : (pos < 0.5 ? "At start" : "Paused");
            string mon = prompter != null ? "Prompter ON" : "Prompter off";
            if (mic.IsRunning) st += mic.Speaking ? "  (voice: speaking)" : "  (voice: waiting for speech)";
            string txt = string.Format("{0}   |   Position {1}%   |   Speed {2:0}   |   {3}{4}{5}", st, pct, S.Speed, mon,
                S.Mirror ? "  • mirrored" : "", S.Flip ? "  • flipped" : "");
            if (txt != lastStatus) { statusText.Text = txt; lastStatus = txt; }
        }

        void UpdateVoice()
        {
            bool want = voiceChk.IsChecked == true;
            mic.Stop();
            if (!want) return;
            try { mic.Start(micCombo.SelectedIndex - 1); }
            catch (Exception ex)
            {
                voiceChk.IsChecked = false;
                MessageBox.Show(this, ex.Message, "Microphone", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        double lastMeterW = -1, lastThrX = -1; bool lastSpeaking;
        void UpdateMeter()
        {
            double range = MeterMaxDb - MeterMinDb;
            double w = Math.Max(0, Math.Min(1, (mic.LevelDb - MeterMinDb) / range)) * (MeterWidth - 2);
            double x = Math.Max(0, Math.Min(1, (S.VoiceThreshold - MeterMinDb) / range)) * (MeterWidth - 2);
            bool sp = mic.Speaking;
            if (Math.Abs(w - lastMeterW) > 0.5) { meterBar.Width = w; lastMeterW = w; }
            if (x != lastThrX) { meterThreshold.Margin = new Thickness(x, 0, 0, 0); lastThrX = x; }
            if (sp != lastSpeaking)
            {
                meterBar.Background = sp ? new SolidColorBrush(Color.FromRgb(0x3C, 0xB3, 0x71)) : Brushes.Gray;
                lastSpeaking = sp;
            }
        }

        void UpdateButtons()
        {
            if (playBtn == null) return;
            playBtn.IsEnabled = !playing;
            pauseBtn.IsEnabled = playing;
            showBtn.Content = prompter != null ? "■  Close prompter" : "▶  Show on monitor";
        }

        // ------------------------------------------------------------------ files

        void OpenFile()
        {
            Microsoft.Win32.OpenFileDialog d = new Microsoft.Win32.OpenFileDialog();
            d.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            if (d.ShowDialog(this) != true) return;
            try
            {
                editor.Text = Settings.ReadTextFile(d.FileName);
                StopToStart();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Open failed"); }
        }

        void SaveFile()
        {
            Microsoft.Win32.SaveFileDialog d = new Microsoft.Win32.SaveFileDialog();
            d.Filter = "Text files (*.txt)|*.txt";
            d.DefaultExt = ".txt";
            if (d.ShowDialog(this) != true) return;
            try { File.WriteAllText(d.FileName, editor.Text, new UTF8Encoding(true)); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Save failed"); }
        }
    }
}
