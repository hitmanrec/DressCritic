using System;

namespace DressmakerCritic.Diagnostics
{
    public static class NotebookPageGeometry
    {
        // Page-flip transforms can point at the facing leaf. The native Photo Mode
        // (or original brief) is an independent marker for the occupied left leaf.
        public static double LeftForMarker(double left, double width, double marker)
        {
            if (double.IsNaN(left) || double.IsNaN(width) || double.IsNaN(marker) ||
                double.IsInfinity(left) || double.IsInfinity(width) || double.IsInfinity(marker) || width <= 0)
                return double.NaN;
            double shift = Math.Floor((marker - left) / width);
            return Math.Abs(shift) > 1 ? double.NaN : left + shift * width;
        }
    }
}
