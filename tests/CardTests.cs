using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DressmakerCritic.Diagnostics;

static class CardTests
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var options = new JsonSerializerOptions { IncludeFields = true };
        string prefix = Path.Combine(root, "DressmakerCritic-Diagnostics/exports/captures");
        var input = JsonSerializer.Deserialize<SnapshotTests.Input>(File.ReadAllText(Path.Combine(prefix, "20261104-122108-178-1e6bac1a/editor-runtime.json")), options);
        var garment = new GarmentSnapshotBuilder(input.objects).Build(input.timeUtc, input.scene);
        string text = GarmentCardText.Build(garment, null);
        Check(text.Contains("Stitched: 7/14") && text.Contains("On mannequin: 14/14") && text.Contains("Burlap: 14"), "card shows actual partial garment construction and fabric");
        var context = JsonSerializer.Deserialize<ContextRuntime>(File.ReadAllText(Path.Combine(prefix, "20261104-125434-693-79d9b746/context-runtime.json")), options);
        var order = ProgressSnapshotBuilder.Build(context);
        text = GarmentCardText.Build(garment, order);
        Check(text.Contains("Elegant >= 50") && text.Contains("Daywear >= 50") && text.Contains("Simple >= 25"), "card displays known order requirements");
        Check(text.Contains("REFERENCE ONLY") && text.Contains("has not been linked") && text.Contains("No compliance verdict"), "card does not assign a historical garment to current order");
        Check(text.Contains("Patterns: 72 | complete") && text.Contains("Actual game scores and artistic brief interpretation are unavailable"), "card separates unlocked membership and unknown scores");
        var finished = JsonSerializer.Deserialize<GarmentSnapshot>(File.ReadAllText(Path.Combine(root, "docs/examples/garment-snapshot.json")), options);
        text = GarmentCardText.Build(finished, order);
        Check(text.Contains("Finished: yes") && text.Contains("Accessories: 7") && text.Contains("unknown (details unavailable)"), "finished dress shown with unknown accessory identities");
        text = GarmentCardText.Build(new GarmentSnapshot { garmentStatus = "absent" }, order);
        Check(text.Contains("No active garment") && text.Contains("Undercover Servant") && !text.Contains("Cut: 0/"), "no garment still shows order without fake empty garment");
        text = GarmentCardText.Build(new GarmentSnapshot { garmentStatus = "available", panelsStatus = "truncated", panelCount = 100 }, new ProgressSnapshot { orderStatus = "unavailable", progressAccessStatus = "blocked" });
        Check(text.Contains("Panel list: truncated | observed 0") && text.Contains("Order data unavailable: blocked"), "card exposes incomplete data and blocked progress");
    }
}
