using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Teleprompter
{
    /// <summary>
    /// The text "page" shown both in the operator preview and on the prompter monitor.
    /// It is always laid out on a virtual canvas that is 1920 units wide (height follows the
    /// target monitor's aspect ratio) and then scaled with a Viewbox. This guarantees the preview
    /// and the prompter wrap lines identically and that the prompter is always exactly centred
    /// and fills the monitor, whatever its resolution or DPI scaling.
    /// Text is rendered by WPF's text engine with a proper paragraph direction, so the Unicode
    /// bidirectional algorithm handles mixed Hebrew / English / numbers correctly.
    /// Mirroring/flipping is applied to the finished rendered image only (never to the text itself).
    /// </summary>
    public class PrompterSurface : Canvas
    {
        public const double VirtualWidth = 1920;

        readonly TextBlock text;
        readonly TranslateTransform shift = new TranslateTransform();
        readonly ScaleTransform orient = new ScaleTransform(1, 1);
        readonly Polygon cueLeft, cueRight;
        double margin = 150;
        double readingFraction = 0.30;

        public PrompterSurface()
        {
            Width = VirtualWidth;
            Height = 1080;
            Background = Brushes.Black;
            ClipToBounds = true;
            SnapsToDevicePixels = false;
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = orient;

            text = new TextBlock();
            text.Foreground = Brushes.White;
            text.TextWrapping = TextWrapping.Wrap;
            text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            text.RenderTransform = shift;
            TextOptions.SetTextFormattingMode(text, TextFormattingMode.Ideal);
            TextOptions.SetTextHintingMode(text, TextHintingMode.Animated);
            TextOptions.SetTextRenderingMode(text, TextRenderingMode.Grayscale);
            Children.Add(text);

            cueLeft = MakeCue();
            cueRight = MakeCue();
            Children.Add(cueLeft);
            Children.Add(cueRight);
            Relayout();
        }

        static Polygon MakeCue()
        {
            Polygon p = new Polygon();
            p.Fill = new SolidColorBrush(Color.FromArgb(220, 255, 176, 0));
            p.IsHitTestVisible = false;
            return p;
        }

        /// <summary>The "reading line" (eye line) – first line of text starts here.</summary>
        public double ReadingLine { get { return Math.Round(Height * readingFraction); } }

        public double LineHeightValue { get { return text.LineHeight; } }

        /// <summary>Largest useful scroll offset: last line reaches the reading line.</summary>
        public double MaxOffset
        {
            get { return Math.Max(0, text.ActualHeight - text.LineHeight); }
        }

        public void SetText(string s)
        {
            // Normalise line endings; each line becomes its own bidi paragraph.
            text.Text = (s ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        }

        public void SetAspect(double heightOverWidth)
        {
            if (double.IsNaN(heightOverWidth) || heightOverWidth <= 0) heightOverWidth = 9.0 / 16.0;
            Height = Math.Round(VirtualWidth * heightOverWidth);
            Relayout();
        }

        public void ApplyStyle(FontFamily family, double fontSize, double lineSpacing,
                               TextAlignment alignment, bool rtl, double sideMargin, bool showCue, double readingLineFraction)
        {
            text.FontFamily = family;
            text.FontSize = fontSize;
            text.LineHeight = Math.Max(1, fontSize * lineSpacing);
            text.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            text.Language = XmlLanguage.GetLanguage(rtl ? "he-IL" : "en-US");
            text.TextAlignment = alignment;
            margin = Math.Max(0, Math.Min(sideMargin, VirtualWidth / 2 - 100));
            readingFraction = Math.Max(0.05, Math.Min(0.9, readingLineFraction));
            Visibility v = showCue ? Visibility.Visible : Visibility.Collapsed;
            cueLeft.Visibility = v;
            cueRight.Visibility = v;
            Relayout();
        }

        public void SetOrientation(bool mirror, bool flip)
        {
            orient.ScaleX = mirror ? -1 : 1;
            orient.ScaleY = flip ? -1 : 1;
        }

        public void SetOffset(double offset)
        {
            shift.Y = -offset;
        }

        void Relayout()
        {
            text.Width = VirtualWidth - 2 * margin;
            Canvas.SetLeft(text, margin);
            double lh = double.IsNaN(text.LineHeight) || text.LineHeight <= 0 ? 100 : text.LineHeight;
            Canvas.SetTop(text, ReadingLine - lh / 2);

            double y = ReadingLine, s = 34;
            cueLeft.Points = new PointCollection(new Point[] {
                new Point(6, y - s), new Point(6 + s * 1.3, y), new Point(6, y + s) });
            cueRight.Points = new PointCollection(new Point[] {
                new Point(VirtualWidth - 6, y - s), new Point(VirtualWidth - 6 - s * 1.3, y), new Point(VirtualWidth - 6, y + s) });
        }
    }
}
