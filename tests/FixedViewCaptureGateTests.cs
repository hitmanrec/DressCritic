using System;
using DressmakerCritic.Diagnostics;

static class FixedViewCaptureGateTests
{
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static void Views(EvaluationInput input)
    {
        foreach (string axis in new[] { "plus-z", "minus-z", "plus-x", "minus-x" })
            input.images.Add(new EvaluationImage { file = "fixed-axis-" + axis + ".png", width = 768, height = 768, sha256 = "fixture", binding = "history-display-chain-verified" });
    }
    public static void Run()
    {
        var input = new EvaluationInput { garment = new GarmentSnapshot { garmentStatus = "available" } };
        input.images.Add(new EvaluationImage { file = "image.png" });
        Check(!FixedViewCaptureGate.Run(input, () => false) && input.images.Count == 0, "failed fixed capture cannot retain original viewer fallback");
        Check(!FixedViewCaptureGate.Run(input, () => { Views(input); return false; }) && input.images.Count == 0, "post-render identity failure clears all candidate views");
        Check(!FixedViewCaptureGate.Run(input, () => { Views(input); input.images.RemoveAt(3); return true; }), "partial canonical set rejected even if stage reports success");
        Check(!FixedViewCaptureGate.Run(input, () => { Views(input); input.images[3].file = input.images[0].file; return true; }), "duplicate axis cannot substitute for missing view");
        try { FixedViewCaptureGate.Run(input, () => { Views(input); throw new InvalidOperationException("render fault"); }); }
        catch (InvalidOperationException) { }
        Check(input.images.Count == 0 && !EvaluationReadiness.Check(input, "fixture").canEvaluate, "unexpected render exception leaves no evaluable images");
        Check(FixedViewCaptureGate.Run(input, () => { Views(input); return true; }) && EvaluationReadiness.Check(input, "fixture").canEvaluate, "complete canonical capture remains evaluable");
    }
}
