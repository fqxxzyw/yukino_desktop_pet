using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace YukinoPet
{
    internal sealed class SpeechBubbleWindow : Window
    {
        private const int GwlExStyle = -20;
        private const int WsExTransparent = 0x20;
        private const int WsExNoActivate = 0x08000000;

        private readonly TextBlock textBlock;
        private double elapsed;
        private double visibleDuration;
        private double fadeInDuration;
        private double fadeOutDuration;
        private int currentPriority;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        public SpeechBubbleWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            SizeToContent = SizeToContent.WidthAndHeight;
            IsHitTestVisible = false;
            Opacity = 0;

            textBlock = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(38, 43, 57)),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 280,
                LineHeight = 22
            };

            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(238, 250, 252, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(145, 121, 137, 169)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(14, 10, 14, 10),
                Child = textBlock,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 12,
                    Direction = 270,
                    ShadowDepth = 3,
                    Opacity = 0.24,
                    Color = Color.FromRgb(28, 32, 47)
                }
            };
            Content = border;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | WsExTransparent | WsExNoActivate);
        }

        public bool IsSpeaking { get { return IsVisible && elapsed < fadeInDuration + visibleDuration + fadeOutDuration; } }
        public int CurrentPriority { get { return currentPriority; } }

        public bool ShowQuote(string text, int priority, Rect petBounds, Rect workArea, double bubbleScale)
        {
            if (IsSpeaking && priority < currentPriority) return false;
            textBlock.Text = text;
            textBlock.FontSize = 14 * bubbleScale;
            textBlock.MaxWidth = 280 * bubbleScale;
            fadeInDuration = 0.20;
            fadeOutDuration = 0.38;
            visibleDuration = Math.Max(1.6, Math.Min(6.5, 1.35 + text.Length * 0.105));
            elapsed = 0;
            currentPriority = priority;
            Opacity = 0;
            if (!IsVisible) Show();
            Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            Arrange(new Rect(DesiredSize));
            PositionNear(petBounds, workArea, 8);
            return true;
        }

        public void Update(double elapsedSeconds, Rect petBounds, Rect workArea)
        {
            if (!IsVisible) return;
            elapsed += Math.Max(0, elapsedSeconds);
            double total = fadeInDuration + visibleDuration + fadeOutDuration;
            if (elapsed >= total)
            {
                Hide();
                Opacity = 0;
                currentPriority = 0;
                return;
            }

            double targetOpacity;
            double rise;
            if (elapsed < fadeInDuration)
            {
                double t = Smooth(elapsed / fadeInDuration);
                targetOpacity = t;
                rise = 6 * (1 - t);
            }
            else if (elapsed < fadeInDuration + visibleDuration)
            {
                targetOpacity = 1;
                rise = 0;
            }
            else
            {
                double t = Smooth((elapsed - fadeInDuration - visibleDuration) / fadeOutDuration);
                targetOpacity = 1 - t;
                rise = -4 * t;
            }
            Opacity = targetOpacity;
            PositionNear(petBounds, workArea, rise);
        }

        public void Dismiss()
        {
            if (!IsVisible) return;
            elapsed = fadeInDuration + visibleDuration;
        }

        private void PositionNear(Rect pet, Rect area, double verticalOffset)
        {
            double width = ActualWidth > 1 ? ActualWidth : DesiredSize.Width;
            double height = ActualHeight > 1 ? ActualHeight : DesiredSize.Height;
            double x;
            if (pet.Left < area.Left + 310) x = pet.Right - Math.Min(24, pet.Width * 0.12);
            else if (pet.Right > area.Right - 310) x = pet.Left - width + Math.Min(24, pet.Width * 0.12);
            else x = pet.Left + (pet.Width - width) / 2;

            double y = pet.Top - height - 10 - verticalOffset;
            if (y < area.Top + 4) y = pet.Bottom + 8 + verticalOffset;
            x = Math.Max(area.Left + 4, Math.Min(x, area.Right - width - 4));
            y = Math.Max(area.Top + 4, Math.Min(y, area.Bottom - height - 4));
            Left = x;
            Top = y;
        }

        private static double Smooth(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            return value * value * (3 - 2 * value);
        }
    }
}
