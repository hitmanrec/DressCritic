using System;
using System.Collections.Generic;

namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class InventorySection
    {
        public string status;
        public int? totalCount;
        public List<AssetIdentity> items = new List<AssetIdentity>();
    }
    [Serializable] public sealed class UnlockedInventory
    {
        public string meaning = "Unlocked membership only; not ownership, affordability, compatibility or available edit operations";
        public InventorySection patterns, fabrics, accessories;
    }
    [Serializable] public sealed class OrderRequirement
    {
        public string type, status, comparison;
        public double? threshold, targetScore;
        public bool? gated;
        public AssetIdentity tag;
        public string interpretation = "raw deterministic game requirement; no aesthetic or semantic brief evaluation";
    }
    [Serializable] public sealed class ProgressSnapshot
    {
        public string schemaVersion = "1.0.0";
        public string capturedAtUtc, progressAccessStatus, orderStatus;
        public int? activeOrderCount, selectedOrderIndex, budget, completionRequirementCount;
        public AssetIdentity order;
        public string completionRequirementsStatus;
        public List<OrderRequirement> completionRequirements = new List<OrderRequirement>();
        public UnlockedInventory unlocked;
        public string semanticBriefStatus = "unknown; no resolved client brief text";
    }
    public static class ProgressSnapshotBuilder
    {
        private static InventorySection FromGraph(GarmentSnapshotBuilder graph, string field)
        {
            int? count; string status;
            var values = graph.Items(graph.Root("PlayerProgress." + field), out count, out status);
            var section = new InventorySection { totalCount = count, status = status };
            foreach (var value in values) section.items.Add(graph.Identity(value));
            return section;
        }
        public static ProgressSnapshot Build(ContextRuntime source, int? explicitOrderIndex = null)
        {
            var graph = new GarmentSnapshotBuilder(source.objects);
            var definition = graph.Root("PlayerProgress.selectedCommission.questDefinition");
            int? count; string status;
            graph.Items(graph.Root("PlayerProgress.activeCommissions"), out count, out status);
            int index;
            string rawIndex = graph.Scalar(graph.Root("PlayerProgress.activeCommissionIndex"));
            int? selected = explicitOrderIndex ?? (int.TryParse(rawIndex, out index) ? index : (int?)null);
            bool available = source.progressAccessStatus == "available";
            var result = new ProgressSnapshot { capturedAtUtc = source.capturedAtUtc, progressAccessStatus = source.progressAccessStatus,
                activeOrderCount = count, selectedOrderIndex = selected, order = graph.Identity(definition), budget = graph.Integer(definition, "budget"),
                orderStatus = !available ? "unavailable" : count == 0 && selected == -1 ? "none" : count.HasValue && selected >= 0 && selected < count && graph.Status(definition) == "fields" ? "selected" : "unknown",
                unlocked = source.unlockedInventory ?? new UnlockedInventory {
                    patterns = FromGraph(graph, "unlockedPatterns"), fabrics = FromGraph(graph, "unlockedFabrics"), accessories = FromGraph(graph, "unlockedAccessories") } };
            var requirements = graph.Items(graph.Field(definition, "completionRequirements"), out count, out status);
            result.completionRequirementCount = count; result.completionRequirementsStatus = status;
            foreach (var requirement in requirements)
                result.completionRequirements.Add(new OrderRequirement { type = requirement?.type?.Split(',')[0], status = graph.Status(requirement),
                    comparison = graph.Text(requirement, "comparison") ?? graph.Text(requirement, "comparisonType"), threshold = graph.Number(requirement, "threshold"),
                    targetScore = graph.Number(requirement, "targetScore"), gated = graph.Boolean(requirement, "gated"), tag = graph.Identity(graph.Field(requirement, "tag")) });
            return result;
        }
        public static InventorySection CaptureMembership(object list)
        {
            var section = new InventorySection { status = "unavailable" };
            if (list == null) return section;
            Type type = list.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>)) return section;
            var size = FieldSnapshot.ResolveFields(list, "_size");
            var array = FieldSnapshot.ResolveFields(list, "_items") as Array;
            if (!(size is int) || (int)size < 0 || array == null) return section;
            section.totalCount = (int)size;
            int limit = Math.Min(Math.Min((int)size, array.Length), 512);
            for (int i = 0; i < limit; i++)
            {
                object item = array.GetValue(i);
                string guid = FieldSnapshot.ResolveFields(item, "_guid") as string;
                string name = FieldSnapshot.ResolveFields(item, "prettyName") as string;
                bool truncated = (guid?.Length > 512) == true || (name?.Length > 512) == true;
                section.items.Add(new AssetIdentity { status = item == null ? "null" : truncated ? "string-limit" : guid == null ? "identity-unavailable" : "fields",
                    guid = guid == null ? null : guid.Substring(0, Math.Min(guid.Length, 512)),
                    prettyName = name == null ? null : name.Substring(0, Math.Min(name.Length, 512)) });
            }
            section.status = limit == (int)size ? "complete" : "truncated";
            return section;
        }
    }
}
