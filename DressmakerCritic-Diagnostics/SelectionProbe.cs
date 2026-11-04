using System;
using System.Collections.Generic;
using System.Reflection;
namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class SelectionFieldEvidence
    {
        public string field, type;
        public List<int> matchingHistoryIndices = new List<int>();
    }
    [Serializable] public sealed class SelectionOwnerEvidence
    {
        public string owner;
        public List<ValueNode> shallowFields = new List<ValueNode>();
        public List<SelectionFieldEvidence> references = new List<SelectionFieldEvidence>();
    }
    public static class SelectionProbe
    {
        public static SelectionOwnerEvidence Read(object owner, object progress, string label)
        {
            var result = new SelectionOwnerEvidence { owner = label };
            if (owner == null) return result;
            result.shallowFields.Add(new FieldSnapshot(512).Read(owner, FieldSnapshot.MaxDepth - 1));
            object history = FieldSnapshot.ResolveFields(progress, "completedQuests");
            int count = FieldSnapshot.ResolveFields(history, "_size") as int? ?? 0;
            int fields = 0;
            foreach (var type in FieldSnapshot.Hierarchy(owner.GetType()))
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (fields++ >= 128) return result;
                    object value; try { value = field.GetValue(owner); } catch { continue; }
                    if (value == null) continue;
                    var evidence = new SelectionFieldEvidence { field = type.FullName + "." + field.Name, type = field.FieldType.FullName };
                    object candidateDress = field.FieldType.Name == "QuestState" ? FieldSnapshot.ResolveFields(value, "dress") : field.FieldType.Name == "Dress" ? FieldSnapshot.ResolveFields(value, "state") : value;
                    for (int i = 0; i < Math.Min(Math.Max(count, 0), 512); i++)
                    {
                        object record = FieldSnapshot.ListItemAt(history, i);
                        if (ReferenceEquals(record, value) || (candidateDress != null && ReferenceEquals(FieldSnapshot.ResolveFields(record, "dress"), candidateDress))) evidence.matchingHistoryIndices.Add(i);
                    }
                    if (evidence.matchingHistoryIndices.Count > 0) result.references.Add(evidence);
                }
            return result;
        }
    }
}
