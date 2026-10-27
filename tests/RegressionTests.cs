using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace YukinoPet
{
    internal static class RegressionTests
    {
        private static int assertions;

        private static void Assert(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception(message);
        }

        private static void Equal(double actual, double expected, string message)
        {
            Assert(Math.Abs(actual - expected) < 0.00001, message + ": " + actual + " != " + expected);
        }

        private static AnimationController Controller()
        {
            return new AnimationController(new Dictionary<string, AnimationClip>
            {
                { "idle", new AnimationClip("idle", 0, 0, 8, new[] { 100 }, true, 5, true, 0, 0) },
                { "action", new AnimationClip("action", 1, 0, 3, new[] { 100 }, false, 70, false, 0, 0) },
                { "fast", new AnimationClip("fast", 2, 0, 8, new[] { 16 }, true, 80, true, 0, 0) }
            }, new Random(42));
        }

        private static void AnimationTests()
        {
            AnimationController regular = Controller();
            AnimationController stalled = Controller();
            for (int i = 0; i < 10; i++) regular.Update(0.05, false);
            stalled.Update(0.5, false);
            Assert(regular.CurrentColumn == 5 && stalled.CurrentColumn == 5, "A 500 ms render stall preserves timeline");

            AnimationController remainder = Controller();
            remainder.Update(0.275, false);
            Assert(remainder.CurrentColumn == 2, "Multiple frames advance on one callback");
            remainder.Update(0.025, false);
            Assert(remainder.CurrentColumn == 3, "Fractional frame time is retained");
            remainder.Play("idle", false, null);
            Assert(remainder.CurrentColumn == 3, "Non-forced loop playback does not restart");
            remainder.Play("idle", true, null);
            Assert(remainder.CurrentColumn == 0, "Explicit restart remains available");
            remainder.Update(0.9, true);
            Assert(remainder.CurrentColumn == 0, "Pause does not accumulate frame debt");
            remainder.Update(0.1, false);
            Assert(remainder.CurrentColumn == 1, "Resume starts at retained frame time");
            remainder.Update(Double.NaN, false);
            remainder.Update(Double.PositiveInfinity, false);
            remainder.Update(-1, false);
            Assert(remainder.CurrentColumn == 1, "Invalid or negative elapsed time is ignored");

            int completed = 0;
            AnimationController oneShot = Controller();
            oneShot.Play("action", true, delegate { completed++; });
            Assert(!oneShot.Play("idle", false, null), "High priority one-shot is protected");
            oneShot.Update(0.45, false);
            oneShot.Update(0.4, false);
            Assert(completed == 1 && oneShot.CurrentName == "idle", "One-shot completes exactly once and returns to idle");
            oneShot.Play("action", true, delegate { oneShot.Play("fast", true, null); });
            oneShot.Update(0.45, false);
            Assert(oneShot.CurrentName == "fast", "Completion callback may select the next clip");
            oneShot.Play("fast", true, null);
            oneShot.Update(3600, false);
            Assert(oneShot.CurrentColumn == 6, "Suspend catch-up is bounded to one second / 62 fast frames");
        }

        private static void Inside(Point point, Size size, Rect area, string message)
        {
            Assert(point.X >= area.Left && point.Y >= area.Top && point.X + size.Width <= area.Right + 0.00001 && point.Y + size.Height <= area.Bottom + 0.00001, message);
        }

        private static void PlacementTests()
        {
            Rect area = new Rect(0, 0, 1920, 1040);
            Size bubble = new Size(320, 128);
            Rect pet = new Rect(900, 600, 192, 208);
            Point result = WindowPlacement.PlaceOverlay(pet, bubble, area, 12, false);
            Assert(result.Y + bubble.Height < pet.Top, "Bubble prefers above when space permits");
            pet.Y = 0;
            result = WindowPlacement.PlaceOverlay(pet, bubble, area, 12, false);
            Assert(result.Y > pet.Bottom, "Bubble flips below at top edge");
            pet = new Rect(900, 500, 192, 208);
            result = WindowPlacement.PlaceOverlay(pet, new Size(300, 700), area, 12, true);
            Assert(result.X > pet.Right, "Tall menu opens beside character");
            pet.X = area.Right - pet.Width;
            result = WindowPlacement.PlaceOverlay(pet, new Size(300, 700), area, 12, true);
            Assert(result.X + 300 < pet.Left, "Menu flips left at right edge");

            Rect shortArea = new Rect(-800, -100, 800, 300);
            pet = new Rect(-550, -50, 192, 208);
            result = WindowPlacement.PlaceOverlay(pet, bubble, shortArea, 12, false);
            Inside(result, bubble, shortArea, "Bubble remains in negative-origin work area");
            Assert(!new Rect(result, bubble).IntersectsWith(pet), "Bubble uses a side when neither vertical anchor fits");
            Size tinyFit = WindowPlacement.Fit(bubble, new Rect(-100, 0, 100, 80), 4);
            Equal(tinyFit.Width, 92, "Bubble adapts only to narrower work area");
            Equal(tinyFit.Height, 72, "Bubble adapts only to shorter work area");

            foreach (double scale in new[] { 0.6, 0.8, 1.0, 1.25, 1.5, 2.0 })
            {
                Rect monitor = new Rect(-1920, -120, 1920, 1040);
                Size petSize = new Size(192 * scale, 208 * scale);
                Size quoteSize = new Size(320 * scale, 128 * scale);
                for (int x = 0; x <= 4; x++)
                for (int y = 0; y <= 4; y++)
                {
                    pet = new Rect(monitor.Left + (monitor.Width - petSize.Width) * x / 4,
                        monitor.Top + (monitor.Height - petSize.Height) * y / 4, petSize.Width, petSize.Height);
                    result = WindowPlacement.PlaceOverlay(pet, quoteSize, monitor, 12, false);
                    Inside(result, quoteSize, monitor, "Scaled bubble stays on screen");
                    Assert(!new Rect(result, quoteSize).IntersectsWith(pet), "Scaled bubble avoids character when space exists");
                    Point bounded = WindowPlacement.Clamp(new Point(monitor.Right + 500, monitor.Bottom + 500), petSize, monitor, 0);
                    for (int repeat = 0; repeat < 50; repeat++) bounded = WindowPlacement.Clamp(bounded, petSize, monitor, 0);
                    Equal(bounded.X, monitor.Right - petSize.Width, "Repeated clamp retains full horizontal range");
                    Equal(bounded.Y, monitor.Bottom - petSize.Height, "Repeated clamp retains full vertical range");
                }
            }
        }

        private static void DragTests()
        {
            Point origin = new Point(-500, 300);
            Point grab = new Point(40, 80);
            for (int i = 0; i < 100; i++)
            {
                // PointerFromScreen is provided by WPF in production. These
                // local points simulate successive 7 / -3 DIP cursor moves.
                origin = WindowPlacement.DragPosition(origin, new Point(grab.X + 7, grab.Y - 3), grab);
            }
            Equal(origin.X, 200, "Repeated moves preserve pointer anchor");
            Equal(origin.Y, 0, "Repeated moves preserve vertical pointer anchor");
            Point stationary = WindowPlacement.DragPosition(origin, grab, grab);
            Assert(stationary == origin, "Stationary pointer does not accumulate drift");
            Point newGrab = new Point(150, 190);
            Point secondDrag = WindowPlacement.DragPosition(origin, new Point(180, 200), newGrab);
            Equal(secondDrag.X, 230, "Second drag uses its new grab offset");
            Equal(secondDrag.Y, 10, "Second drag does not reuse previous origin");
        }

        private static void AssetTests(string root)
        {
            string project = Path.Combine(root, "src", "YukinoPet");
            string source = File.ReadAllText(Path.Combine(project, "PetWindow.cs"));
            MatchCollection clips = Regex.Matches(source, "new AnimationClip\\(\"([^\"]+)\",\\s*(\\d+),\\s*(\\d+),\\s*(\\d+),");
            Assert(clips.Count == 15, "Every configured animation is included in asset checks");
            foreach (Match clip in clips)
            {
                string name = clip.Groups[1].Value;
                int row = Int32.Parse(clip.Groups[2].Value);
                int start = Int32.Parse(clip.Groups[3].Value);
                int count = Int32.Parse(clip.Groups[4].Value);
                string filename = row == 11 ? "walking-right.png" : row == 12 ? "walking-left.png" : "spritesheet.png";
                int sourceRow = row >= 11 ? 0 : row;
                using (System.Drawing.Bitmap atlas = new System.Drawing.Bitmap(Path.Combine(project, "assets", filename)))
                {
                    Assert((start + count) * 192 <= atlas.Width && (sourceRow + 1) * 208 <= atlas.Height, name + " stays within atlas bounds");
                    for (int column = start; column < start + count; column++)
                    {
                        bool visible = false;
                        for (int y = sourceRow * 208; y < (sourceRow + 1) * 208 && !visible; y++)
                        for (int x = column * 192; x < (column + 1) * 192 && !visible; x++)
                            visible = atlas.GetPixel(x, y).A > 0;
                        Assert(visible, name + " frame " + column + " is not transparent");
                    }
                }
            }
        }

        private static void BubbleLayoutTests()
        {
            SpeechBubbleWindow bubble = new SpeechBubbleWindow();
            try
            {
                Rect area = new Rect(0, 0, 1920, 1040);
                Rect pet = new Rect(900, 700, 192, 208);
                bubble.ShowQuote("你好。", 100, pet, area, 1);
                bubble.UpdateLayout();
                Equal(bubble.Width, 320, "Short quote width is fixed");
                Equal(bubble.Height, 128, "Short quote height is fixed");
                bubble.ShowQuote("你已经很久没有移动了。是在思考，还是单纯卡住了？", 100, pet, area, 1);
                bubble.UpdateLayout();
                Equal(bubble.ActualWidth, 320, "Longest bundled Chinese quote preserves width");
                Equal(bubble.ActualHeight, 128, "Longest bundled Chinese quote preserves height");
                bubble.ShowQuote(new String('雪', 240), 100, pet, area, 1);
                bubble.UpdateLayout();
                Border border = (Border)bubble.Content;
                TextBlock text = (TextBlock)border.Child;
                Assert(text.TextWrapping == TextWrapping.Wrap && text.TextTrimming == TextTrimming.CharacterEllipsis && border.ClipToBounds,
                    "Oversized custom quotes wrap/trim inside the fixed frame");
                Equal(bubble.ActualWidth, 320, "Long custom quote does not grow width");
                Equal(bubble.ActualHeight, 128, "Long custom quote does not grow height");
                Rect tiny = new Rect(-200, -50, 200, 100);
                bubble.ShowQuote("小屏幕边缘测试。", 100, new Rect(-180, -40, 100, 80), tiny, 2);
                bubble.UpdateLayout();
                Inside(new Point(bubble.Left, bubble.Top), new Size(bubble.Width, bubble.Height), tiny, "Bubble fits unusually small work area");
                bubble.Dismiss();
                bubble.Update(2, pet, area);
                Assert(!bubble.IsVisible, "Dismissal hides the quote");
            }
            finally { bubble.Close(); }
        }

        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                AnimationTests();
                PlacementTests();
                DragTests();
                AssetTests(args[0]);
                BubbleLayoutTests();
                Console.WriteLine("PASS: " + assertions + " regression assertions");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }
    }
}
