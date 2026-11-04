using System;
using System.Collections.Generic;

namespace DressmakerCritic.Diagnostics
{
    public sealed class SavedDressChoice
    {
        public int index;
        public string label;
        internal object record;
    }
    public static class SavedDressAccess
    {
        public static List<SavedDressChoice> List(object progress, out string status)
        {
            var result = new List<SavedDressChoice>();
            object list = FieldSnapshot.ResolveFields(progress, "completedQuests");
            var size = FieldSnapshot.ResolveFields(list, "_size");
            var array = FieldSnapshot.ResolveFields(list, "_items") as Array;
            if (list == null || !list.GetType().IsGenericType || list.GetType().GetGenericTypeDefinition() != typeof(List<>) || !(size is int) || (int)size < 0 || array == null)
            { status = "Saved history unavailable"; return result; }
            int count = Math.Min(Math.Min((int)size, array.Length), 512);
            for (int i = 0; i < count; i++)
            {
                object record = array.GetValue(i);
                object dress = FieldSnapshot.ResolveFields(record, "dress");
                if (dress == null) continue;
                string name = FieldSnapshot.ResolveFields(record, "questDefinition", "prettyName") as string;
                if (string.IsNullOrEmpty(name)) name = "Saved garment";
                result.Add(new SavedDressChoice { index = i, record = record, label = "#" + (i + 1) + " " + name.Substring(0, Math.Min(name.Length, 128)) });
            }
            status = "Completed-order history: " + result.Count + " garments" + (count < (int)size ? " (partial; first 512 records)" : "");
            return result;
        }
        public static object Resolve(object currentProgress, object expectedProgress, SavedDressChoice choice, out string status)
        {
            status = "Selection is stale. Open Saved dresses again.";
            if (currentProgress == null || !ReferenceEquals(currentProgress, expectedProgress) || choice == null) return null;
            object record = FieldSnapshot.ListItemAt(FieldSnapshot.ResolveFields(currentProgress, "completedQuests"), choice.index);
            if (!ReferenceEquals(record, choice.record)) return null;
            object state = FieldSnapshot.ResolveFields(record, "dress");
            status = state == null ? "Saved garment has no readable dress state" : "available";
            return state;
        }
    }
}
