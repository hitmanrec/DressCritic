using System;
using System.Collections.Generic;

namespace DressmakerCritic.Diagnostics
{
    public sealed class AcceptedOrderChoice
    {
        public int index;
        public string label;
        internal object record;
    }
    public static class AcceptedOrderAccess
    {
        public static List<AcceptedOrderChoice> List(object progress)
        {
            var result = new List<AcceptedOrderChoice>();
            object list = FieldSnapshot.ResolveFields(progress, "activeCommissions");
            object size = FieldSnapshot.ResolveFields(list, "_size");
            if (!(size is int)) return result;
            for (int i = 0; i < Math.Min(Math.Max((int)size, 0), 512); i++)
            {
                object record = FieldSnapshot.ListItemAt(list, i);
                if (record == null) continue;
                string name = FieldSnapshot.ResolveFields(record, "questDefinition", "prettyName") as string;
                result.Add(new AcceptedOrderChoice { index = i, record = record,
                    label = "#" + (i + 1) + " " + (string.IsNullOrEmpty(name) ? "Заказ без названия" : name.Substring(0, Math.Min(128, name.Length))) });
            }
            return result;
        }
        public static bool IsCurrent(object progress, object expected, AcceptedOrderChoice choice)
        {
            return progress != null && ReferenceEquals(progress, expected) && choice != null &&
                ReferenceEquals(FieldSnapshot.ListItemAt(FieldSnapshot.ResolveFields(progress, "activeCommissions"), choice.index), choice.record);
        }
    }
}
