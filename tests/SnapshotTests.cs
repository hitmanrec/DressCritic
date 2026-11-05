using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DressmakerCritic.Diagnostics;

static class SnapshotTests
{
    public sealed class Input { public List<ValueNode> objects = null; public string timeUtc = null, scene = null; }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static void Run(string[] args)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/nine-stage-captures.json")));
        var options = new JsonSerializerOptions { IncludeFields = true };
        var snapshots = new List<GarmentSnapshot>();
        foreach (var entry in manifest.RootElement.EnumerateArray())
        {
            string directory = Path.Combine(root, entry.GetProperty("path").GetString());
            var input = JsonSerializer.Deserialize<Input>(RuntimeFixtures.Read(Path.GetFileName(directory), "editor-runtime.json"), options);
            var snapshot = new GarmentSnapshotBuilder(input.objects).Build(input.timeUtc, input.scene);
            snapshots.Add(snapshot);
            using (var json = JsonDocument.Parse(DiagnosticJson.Serialize(snapshot)))
                Check(json.RootElement.GetProperty("schemaVersion").GetString() == "1.0.0", "versioned snapshot stage " + snapshots.Count);
            if (args.Length == 1)
            {
                Directory.CreateDirectory(args[0]);
                File.WriteAllText(Path.Combine(args[0], "stage-" + snapshots.Count + ".json"), DiagnosticJson.Serialize(snapshot));
            }
        }
        Check(snapshots.Count == 9 && snapshots.Take(2).All(s => s.garmentStatus == "absent" && s.panelCount == null), "empty screens do not fabricate garment/counts");
        Check(snapshots.Skip(2).All(s => s.panelCount == 14 && s.panels.Count == 14 && s.panelsStatus == "complete"), "actual active dress has 14 panels, photo copies excluded");
        Check(snapshots[2].panels.All(p => p.fabric.status == "null" && p.hasBeenCut == false), "uncut panels have explicit absent fabric");
        Check(snapshots[4].panels.All(p => p.fabric.prettyName == "Burlap" && p.fabric.guid != null && p.hasBeenCut == true), "fabric references resolve consistently");
        Check(snapshots[6].panels.Count(p => p.hasBeenStitched == true) == 7 && snapshots[6].panels.All(p => p.placedOnMannequin == true), "partial sewing 7/14");
        Check(snapshots[7].sewingComplete == true && snapshots[7].finished == false && snapshots[8].finished == true, "sewing and finish are separate");
        Check(snapshots[8].accessoryCount == 7 && snapshots[8].accessories.All(a => a.placed == true && a.definition.guid == null && a.definition.status == "depth-limit"), "accessory count preserved; limited identity remains unknown");
        using (var json = JsonDocument.Parse(DiagnosticJson.Serialize(snapshots[4])))
            Check(json.RootElement.GetProperty("panels")[0].GetProperty("grainQuality").ValueKind == JsonValueKind.Number && json.RootElement.GetProperty("finished").ValueKind == JsonValueKind.False, "normalized JSON uses numeric/boolean types");
        // Mutate a deserialized fixture, never a captured file, to simulate incomplete graphs.
        var fixture = JsonSerializer.Deserialize<Input>(RuntimeFixtures.Read("20261104-122434-315-f7e1a777", "editor-runtime.json"), options);
        var state = fixture.objects.First(n => n.member == "GameManager.activeDress.state");
        var panels = state.children.First(n => n.member == "DressState.panelStates");
        var backing = panels.children.First(n => n.member.EndsWith("._items"));
        backing.children.RemoveRange(2, backing.children.Count - 2);
        var partial = new GarmentSnapshotBuilder(fixture.objects).Build(fixture.timeUtc, fixture.scene);
        Check(partial.panelCount == 14 && partial.panels.Count == 2 && partial.panelsStatus == "truncated", "truncated arrays do not become complete");
        var broken = new GarmentSnapshotBuilder(new List<ValueNode> { new ValueNode { member = "GameManager.activeDress.state", referenceId = 999 } }).Build("test", "test");
        Check(broken.garmentStatus == "unavailable" && broken.finished == null, "dangling reference remains unknown");
    }
}
