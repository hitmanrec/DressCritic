using System;
using System.Collections.Generic;
using DressmakerCritic.Diagnostics;

static class SavedDressTests
{
    // Synthetic records test selection semantics only; no game API is emulated.
    class Definition { public string prettyName = "Old order"; }
    class Record { public object dress; public Definition questDefinition = new Definition(); }
    class Progress { public List<Record> completedQuests = new List<Record>(); }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    public static void Run()
    {
        string status;
        var state = new object(); var progress = new Progress();
        progress.completedQuests.Add(new Record { dress = null });
        progress.completedQuests.Add(new Record { dress = state });
        var choices = SavedDressAccess.List(progress, out status);
        Check(choices.Count == 1 && choices[0].index == 1 && choices[0].label.Contains("#2 Old order"), "history skips absent garments without renumbering source records");
        Check(ReferenceEquals(SavedDressAccess.Resolve(progress, progress, choices[0], out status), state), "explicit saved choice resolves its own record");
        Check(SavedDressAccess.Resolve(new Progress(), progress, choices[0], out status) == null, "save switch invalidates saved choice");
        progress.completedQuests[1] = new Record { dress = new object() };
        Check(SavedDressAccess.Resolve(progress, progress, choices[0], out status) == null, "history reordering/replacement cannot select another garment silently");
        Check(SavedDressAccess.List(null, out status).Count == 0 && status.Contains("unavailable"), "missing history is explicit");
        var graph = new List<ValueNode> { new ValueNode { member = "PlayerProgress.completedQuests[1].dress", status = "fields" } };
        var snapshot = new GarmentSnapshotBuilder(graph).Build("test", "test", "PlayerProgress.completedQuests[1].dress");
        Check(snapshot.garmentStatus == "available" && snapshot.source == "PlayerProgress.completedQuests[1].dress", "saved snapshot keeps real source provenance");
        string text = GarmentCardText.Build(snapshot, null);
        Check(text.Contains("ВЫБРАННОЕ СОХРАНЁННОЕ ПЛАТЬЕ") && text.Contains("Автоматической привязки нет"), "saved card cannot be mistaken for current garment/order pairing");
    }
}
