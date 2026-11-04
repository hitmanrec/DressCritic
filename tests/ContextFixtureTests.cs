using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DressmakerCritic.Diagnostics;

static class ContextFixtureTests
{
    class AssetFixture { public string _guid = "fixture-id"; public string prettyName = "fixture-name"; }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static void Run(string[] args)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var snapshots = new List<ProgressSnapshot>();
        foreach (string capture in new[] {"20261104-124653-858-b665a5ea", "20261104-124817-488-cd75ebd1", "20261104-125434-693-79d9b746"})
        {
            string path = Path.Combine(root, "DressmakerCritic-Diagnostics/exports/captures", capture, "context-runtime.json");
            var input = JsonSerializer.Deserialize<ContextRuntime>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
            var snapshot = ProgressSnapshotBuilder.Build(input); snapshots.Add(snapshot);
            using (var doc = JsonDocument.Parse(DiagnosticJson.Serialize(snapshot)))
                Check(doc.RootElement.GetProperty("schemaVersion").GetString() == "1.0.0", "progress snapshot JSON fixture " + snapshots.Count);
            if (args.Length == 1) File.WriteAllText(Path.Combine(args[0], "progress-" + snapshots.Count + ".json"), DiagnosticJson.Serialize(snapshot));
        }
        Check(snapshots[0].orderStatus == "none" && snapshots[0].activeOrderCount == 0 && snapshots[0].selectedOrderIndex == -1, "no order is explicitly represented");
        var order = snapshots[1];
        Check(order.orderStatus == "selected" && order.activeOrderCount == 1 && order.order.prettyName == "Undercover Servant" && order.budget == 250, "accepted story order and budget");
        Check(order.completionRequirements.Count == 4 && order.completionRequirements[0].threshold == 75 && order.completionRequirements.Count(r => r.type == "TagScoreRequirement") == 3, "four raw game requirements, no invented tag names");
        Check(order.completionRequirements.Skip(1).All(r => r.tag.guid == null && r.tag.status == "depth-limit"), "unknown score tags stay unknown");
        Check(order.unlocked.patterns.totalCount == 72 && order.unlocked.fabrics.totalCount == 212 && order.unlocked.accessories.totalCount == 182 && order.unlocked.patterns.items.Count == 32 && order.unlocked.patterns.status == "truncated", "old membership graph truncation remains explicit");
        var verified = snapshots[2];
        Check(verified.completionRequirements.Skip(1).Select(r => r.tag.prettyName).SequenceEqual(new[] { "Elegant", "Daywear", "Simple" }), "real focused requirement tags");
        Check(verified.completionRequirements.Skip(1).Select(r => r.targetScore).SequenceEqual(new double?[] { 50, 50, 25 }), "real score thresholds retain tag correspondence");
        var sections = new[] { verified.unlocked.patterns, verified.unlocked.fabrics, verified.unlocked.accessories };
        Check(sections.Select(s => s.totalCount).SequenceEqual(new int?[] { 72, 212, 182 }) && sections.All(s => s.status == "complete" && s.items.Count == s.totalCount), "real full unlocked membership counts");
        Check(sections.All(s => s.items.All(i => i.guid != null) && s.items.Select(i => i.guid).Distinct().Count() == s.items.Count), "real unlocked IDs are present and unique per category");
        string livePath = Path.Combine(root, "DressmakerCritic-Diagnostics/exports/captures/20261104-125434-693-79d9b746/progress-snapshot.json");
        Check(System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(DiagnosticJson.Serialize(verified)), System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(livePath))), "offline normalized context equals actual live export");
        var complete = ProgressSnapshotBuilder.CaptureMembership(Enumerable.Range(0, 72).Select(_ => new AssetFixture()).ToList());
        Check(complete.totalCount == 72 && complete.items.Count == 72 && complete.status == "complete" && complete.items.All(i => i.guid == "fixture-id"), "compact membership exceeds graph's 32-item cap");
        var limited = ProgressSnapshotBuilder.CaptureMembership(Enumerable.Range(0, 600).Select(_ => new AssetFixture()).ToList());
        Check(limited.items.Count == 512 && limited.status == "truncated", "compact membership hard cap");
        Check(ProgressSnapshotBuilder.Build(new ContextRuntime { progressAccessStatus = "blocked" }).orderStatus == "unavailable", "blocked access is not no-order");
        Check(ProgressSnapshotBuilder.CaptureMembership(new object()).status == "unavailable", "custom membership source refused");
    }
}
