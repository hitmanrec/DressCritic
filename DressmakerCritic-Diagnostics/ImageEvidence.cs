using System;
namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class ImageEvidence
    {
        public string schemaVersion = "1.0.0", capturedAtUtc, source;
        public string pairing = "unpaired: image is not automatically assigned to the inspected garment or order";
        public string uiStatus, file = "image.png";
        public int width, height;
        public string viewsStatus = "single-view";
        public System.Collections.Generic.List<string> files = new System.Collections.Generic.List<string> { "image.png" };
        public static int[] Fit(int width, int height, int limit)
        {
            if (width < 1 || height < 1 || limit < 1 || limit > 2048) throw new ArgumentOutOfRangeException("image dimensions");
            double scale = Math.Min(1d, (double)limit / Math.Max(width, height));
            return new[] { Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)) };
        }
    }
}
