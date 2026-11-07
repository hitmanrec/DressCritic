using System;
using System.Linq;

namespace DressmakerCritic.Diagnostics
{
    public static class FixedViewCaptureGate
    {
        private static readonly string[] files = { "fixed-axis-plus-z.png", "fixed-axis-minus-z.png", "fixed-axis-plus-x.png", "fixed-axis-minus-x.png" };
        public static bool Run(EvaluationInput input, Func<bool> capture)
        {
            // The original viewer image is diagnostic evidence, never an implicit fallback.
            input.images.Clear();
            bool accepted = false;
            try
            {
                accepted = capture() && input.images.Count == 4 &&
                    files.All(file => input.images.Count(i => i.file == file) == 1) &&
                    input.images.All(i => i.width == 768 && i.height == 768 && !string.IsNullOrWhiteSpace(i.sha256));
                return accepted;
            }
            finally { if (!accepted) input.images.Clear(); }
        }
    }
}
