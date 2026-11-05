using System;
using System.Collections.Generic;
using System.Linq;

namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class HistoryOrderEvidence
    {
        public string schemaVersion = "1.0.0", status = "unavailable", origin = "unknown", association;
        public int historyIndex;
        public AssetIdentity definition;
        public string questGiverId, recipientId, assetKey;
        public int? budget;
        public string requirementsStatus;
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
            result.requirements = normalized.completionRequirements;
            result.requirementsStatus = normalized.completionRequirementsStatus;
            result.questGiverId = FieldSnapshot.ResolveFields(definition, "questGiverCharacter", "_guid") as string;
            result.recipientId = FieldSnapshot.ResolveFields(record, "dressRecipient", "_guid") as string;
            result.origin = Classify(result.definition?.guid, gameVersion);
            result.status = definition == null ? "definition-unavailable" : "record-state-verified";
            return result;
        }
    }
}
