using System;
using System.Windows;

namespace YukinoPet
{
    // All inputs are in one window's DIP coordinate system. Never shrink the
    // work area by the pet size and then reuse that shrunken area on a later drag.
    internal static class WindowPlacement
    {
        public static Point DragPosition(Point origin, Point pointerLocal, Point grabOffset)
        {
            return new Point(origin.X + pointerLocal.X - grabOffset.X,
                origin.Y + pointerLocal.Y - grabOffset.Y);
        }

        public static Point Clamp(Point position, Size size, Rect area, double margin)
        {
            double left = area.Left + margin;
            double top = area.Top + margin;
            return new Point(
                Math.Max(left, Math.Min(position.X, Math.Max(left, area.Right - margin - size.Width))),
                Math.Max(top, Math.Min(position.Y, Math.Max(top, area.Bottom - margin - size.Height))));
        }

        public static Size Fit(Size requested, Rect area, double margin)
        {
            return new Size(Math.Min(requested.Width, Math.Max(1, area.Width - margin * 2)),
                Math.Min(requested.Height, Math.Max(1, area.Height - margin * 2)));
        }

        public static Point PlaceOverlay(Rect pet, Size size, Rect area, double gap, bool preferSides)
        {
            Point above = new Point(pet.Left + (pet.Width - size.Width) / 2, pet.Top - size.Height - gap);
            Point below = new Point(pet.Left + (pet.Width - size.Width) / 2, pet.Bottom + gap);
            Point right = new Point(pet.Right + gap, pet.Top + (pet.Height - size.Height) / 2);
            Point left = new Point(pet.Left - size.Width - gap, pet.Top + (pet.Height - size.Height) / 2);
            Point[] candidates = preferSides ? new[] { right, left, above, below } : new[] { above, below, right, left };
            Point best = Clamp(candidates[0], size, area, 4);
            double bestOverlap = Double.MaxValue;
            double bestShift = Double.MaxValue;
            foreach (Point candidate in candidates)
            {
                Point bounded = Clamp(candidate, size, area, 4);
                Rect intersection = Rect.Intersect(new Rect(bounded, size), pet);
                double overlap = intersection.IsEmpty ? 0 : intersection.Width * intersection.Height;
                double shift = Math.Abs(candidate.X - bounded.X) + Math.Abs(candidate.Y - bounded.Y);
                // Prefer not covering the character, then the least screen-edge
                // correction. Original ordering breaks ties without jitter.
                if (overlap < bestOverlap || (overlap == bestOverlap && shift < bestShift))
                {
                    best = bounded;
                    bestOverlap = overlap;
                    bestShift = shift;
                }
            }
            return best;
        }
    }
}
