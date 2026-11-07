using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class HistoryOrderEvidence
    {
        public string schemaVersion = "1.1.0", status = "unavailable", origin = "unknown", association;
        public int historyIndex;
        public AssetIdentity definition;
        public string questGiverId, recipientId, assetKey;
        public int? budget;
        public string requirementsStatus;
        public int? requirementCount;
        public List<ValueNode> requirementFields = new List<ValueNode>();
        public HistoryBriefEvidence displayedBrief = new HistoryBriefEvidence();
        public EvaluationBrief brief = new EvaluationBrief();
        public List<OrderRequirement> requirements = new List<OrderRequirement>();
        public List<ValueNode> objects = new List<ValueNode>();
        public string limitation = "Origin IDs verified in game 0.7.0 capture 20261104-230842; unknown IDs are not classified. Sale completion and full dialogue are not inferred.";
    }
    public static class HistoryOrder
    {
        public const string OffRackGuid = "33dec54894f06e245bb3006dfd0174c0";
        private static readonly string[] commissions = {
            "e8367d99b82cdda30a34589a064254d6", "0f842c1626de12b778b9d9337135ee41",
            "7af8cbc4aff3b6071a6dad1dfbb05298", "afd57c35db162908c8c4708022bc8814",
            "919f89a4c2ac938bbb3f9d5723751a19", "421883f1dac843efdbfa1f8e8aaa231f",
            "77fbf8037002f1bdca9463cea766154b", "dbab0460e5c18c02e929937901f95243",
            "37b03a16942b9d474b98e77c84d80d26", "0b621731918917c2cb39197b720642e3" };
        public static string Classify(string guid, string gameVersion) => gameVersion != "0.7.0" ? "unknown" :
            guid == OffRackGuid ? "off-rack" : commissions.Contains(guid) ? "commission" : "unknown";
        public static string DescribeRequirement(OrderRequirement goal, ValueNode raw = null)
        {
            string label = goal.type == "TagScoreRequirement" ? goal.tag?.prettyName ?? goal.tag?.guid ?? "Unknown tag" :
                goal.type == "QualityRequirement" ? "Quality" : goal.type ?? "Unavailable goal";
            double? target = goal.targetScore ?? goal.threshold;
            var parameters = new List<string>();
            var fields = raw?.children?.FirstOrDefault(n => n.member == "goal")?.children;
            if (!target.HasValue && fields != null)
                foreach (var field in fields.Take(32))
                {
                    string name = field.member?.Split('.').Last() ?? "field";
                    if (name.StartsWith("_", StringComparison.Ordinal) || name.StartsWith("gate", StringComparison.Ordinal)) continue;
                    if (field.status == "scalar") parameters.Add(name + "=" + field.value?.Substring(0, Math.Min(field.value.Length, 120)));
                    if (parameters.Count == 6) break;
                }
            return label + (target.HasValue ? " · " + goal.comparison + " " + target.Value.ToString("0.##", CultureInfo.InvariantCulture) :
                parameters.Count > 0 ? " · " + string.Join(", ", parameters) : " · raw parameters exported") +
                (goal.gated == true ? " · conditional; activation unknown" : "");
        }

        public static HistoryOrderEvidence Capture(object record, object selectedState, int index, string gameVersion)
        {
            var result = new HistoryOrderEvidence { historyIndex = index, association = "history record " + index };
            if (record == null || selectedState == null || !ReferenceEquals(FieldSnapshot.ResolveFields(record, "dress"), selectedState))
            { result.status = "record-state-mismatch"; return result; }
            object definition = FieldSnapshot.ResolveFields(record, "questDefinition");
            var graph = new FieldSnapshot(2048);
            var requirements = FieldSnapshot.ListItems(FieldSnapshot.ResolveFields(definition, "completionRequirements"));
            for (int i = 0; i < requirements.Length && !graph.Exhausted; i++)
            {
                var r = requirements[i];
                if (r == null || r.GetType().FullName != "TagScoreRequirement") continue;
                var tag = graph.Read(FieldSnapshot.ResolveFields(r, "tag"));
                tag.member = "History.selectedRecord.completionRequirements[" + i + "].tag";
                result.objects.Add(tag);
            }
            var node = graph.Read(definition); node.member = "History.selectedRecord.questDefinition"; result.objects.Add(node);
            var normalized = ProgressSnapshotBuilder.Build(new ContextRuntime { progressAccessStatus = "available", objects = result.objects },
                definitionPath: node.member);
            result.definition = normalized.order;
            result.budget = normalized.budget;
            // Read each goal near the root, outside the character/gift-quest graph.
            // Preserve unfamiliar types and nested parameter fields without guessing their meaning.
            object list = FieldSnapshot.ResolveFields(definition, "completionRequirements");
            Type listType = list?.GetType();
            result.requirementCount = listType != null && listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>)
                ? FieldSnapshot.ResolveFields(list, "_size") as int? : null;
            if (result.requirementCount < 0) result.requirementCount = null;
            result.requirementsStatus = result.requirementCount.HasValue ? "complete" : "unavailable";
            for (int i = 0; i < requirements.Length; i++)
            {
                var focused = new FieldSnapshot(128);
                var tag = focused.Read(FieldSnapshot.ResolveFields(requirements[i], "tag"));
                tag.member = "goal.tag";
                var goal = focused.Read(requirements[i]); goal.member = "goal";
                var goalGraph = new GarmentSnapshotBuilder(new List<ValueNode> { tag, goal });
                result.requirements.Add(ProgressSnapshotBuilder.ReadRequirement(goalGraph, goal));
                // Keep the focused tag root too: goal.tag can be a reference to it.
                result.requirementFields.Add(new ValueNode { member = "completionRequirements[" + i + "]", status = "fields",
                    children = new List<ValueNode> { tag, goal } });
                if (goal.status == "null" || Limited(tag) || Limited(goal)) result.requirementsStatus = "partial-fields";
            }
            if (result.requirementCount.HasValue && result.requirementCount != requirements.Length)
                result.requirementsStatus = "truncated";
            result.questGiverId = FieldSnapshot.ResolveFields(definition, "questGiverCharacter", "_guid") as string;
            result.recipientId = FieldSnapshot.ResolveFields(record, "dressRecipient", "_guid") as string;
            result.origin = Classify(result.definition?.guid, gameVersion);
            result.status = definition == null ? "definition-unavailable" : "record-state-verified";
            return result;
        }
        private static bool Limited(ValueNode node) => node.status == "unavailable" ||
            node.status?.EndsWith("limit", StringComparison.Ordinal) == true || node.children.Any(Limited);
    }

    [Serializable] public sealed class HistoryBriefEvidence
    {
        public string status = "unavailable", title, text;
        public int? displayedIndex;
        public string source = "Sketchbook.oldQuestTitle/oldQuestText TMP backing fields; displayed card only, not full dialogue";
    }
    public static class HistoryBriefCapture
    {
        public static HistoryBriefEvidence Read(object book, object record, object state, int index, int? verifiedHistoryIndex)
        {
            var result = new HistoryBriefEvidence { displayedIndex = FieldSnapshot.ResolveFields(book, "_oldQuestIndex") as int? };
            if (!verifiedHistoryIndex.HasValue) { result.status = "display-unverified"; return result; }
            // UI page numbering need not equal completedQuests ordering. Only the independently
            // verified history-state index admits a record; retain the UI index as evidence.
            if (verifiedHistoryIndex != index || book == null || record == null || state == null ||
                !ReferenceEquals(FieldSnapshot.ResolveFields(record, "dress"), state) ||
                !ReferenceEquals(FieldSnapshot.ResolveFields(book, "_displayDress", "state"), state))
            { result.status = "record-display-mismatch"; return result; }
            // TMP's stored string only. No text getters, dialogue methods or localization callbacks.
            string title = FieldSnapshot.ResolveFields(book, "oldQuestTitle", "m_text") as string;
            string text = FieldSnapshot.ResolveFields(book, "oldQuestText", "m_text") as string;
            if (title?.Length > 512 || text?.Length > 4096)
            { result.status = "text-limit"; return result; }
            result.title = title; result.text = text;
            string expected = FieldSnapshot.ResolveFields(record, "questDefinition", "prettyName") as string;
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(expected) || title.Trim() != expected.Trim())
            { result.status = "title-mismatch-or-unavailable"; return result; }
            result.status = string.IsNullOrWhiteSpace(text) ? "text-unavailable" : "record-display-verified";
            return result;
        }
        public static EvaluationBrief Resolve(HistoryBriefEvidence displayed, EvaluationBrief reference, int index, string guid)
        {
            if (displayed?.status != "record-display-verified") return reference ?? new EvaluationBrief();
            return new EvaluationBrief { status = "runtime-history-verified", summary = displayed.text,
                sourceUrl = "game://history/" + index + "/quest/" + guid,
                coverage = "Displayed game brief in the current game language; no full dialogue or inferred customer preferences",
                uncertainties = new List<string> { "Only the displayed card is captured; hidden dialogue and client preferences are unknown." } };
        }
    }
}
