using System;
namespace DressmakerCritic.Diagnostics
{
    public static class RuntimeContextCapture
    {
        public static ContextRuntime Capture(Type type, string time, string scene, int? explicitOrderIndex = null)
        {
            var result = new ContextRuntime { capturedAtUtc = time, scene = scene };
            string status;
            object progress = ProgressAccess.TryRead(type, out status);
            result.progressAccessStatus = status;
            if (progress == null) return result;
            result.unlockedInventory = new UnlockedInventory {
                patterns = ProgressSnapshotBuilder.CaptureMembership(FieldSnapshot.ResolveFields(progress, "unlockedPatterns")),
                fabrics = ProgressSnapshotBuilder.CaptureMembership(FieldSnapshot.ResolveFields(progress, "unlockedFabrics")),
                accessories = ProgressSnapshotBuilder.CaptureMembership(FieldSnapshot.ResolveFields(progress, "unlockedAccessories")) };
            var graph = new FieldSnapshot(4096);
            object index = explicitOrderIndex.HasValue ? (object)explicitOrderIndex.Value : FieldSnapshot.ResolveFields(progress, "activeCommissionIndex");
            object selected = index is int ? FieldSnapshot.ListItemAt(FieldSnapshot.ResolveFields(progress, "activeCommissions"), (int)index) : null;
            object definition = FieldSnapshot.ResolveFields(selected, "questDefinition");
            var requirements = FieldSnapshot.ListItems(FieldSnapshot.ResolveFields(definition, "completionRequirements"));
            for (int i = 0; i < requirements.Length && !graph.Exhausted; i++)
            {
                var requirement = requirements[i];
                if (requirement == null || requirement.GetType().FullName != "TagScoreRequirement") continue;
                var tag = graph.Read(FieldSnapshot.ResolveFields(requirement, "tag"));
                tag.member = "PlayerProgress.selectedCommission.completionRequirements[" + i + "].tag";
                result.objects.Add(tag);
            }
            foreach (var pair in new[] {
                new { label = "PlayerProgress.selectedCommission.questDefinition", value = FieldSnapshot.ResolveFields(selected, "questDefinition") },
                new { label = "PlayerProgress.selectedCommission", value = selected } })
            {
                var node = graph.Read(pair.value); node.member = pair.label; result.objects.Add(node);
            }
            foreach (string field in new[] { "activeCommissionIndex", "activeCommissions", "unlockedPatterns", "unlockedFabrics", "unlockedAccessories" })
            {
                if (graph.Exhausted) { result.note += " Graph budget exhausted."; break; }
                var node = graph.Read(FieldSnapshot.ResolveFields(progress, field));
                node.member = "PlayerProgress." + field;
                result.objects.Add(node);
            }
            return result;
        }

    }
}
