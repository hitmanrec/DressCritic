using System.Collections.Generic;
namespace DressmakerCritic.Diagnostics
{
    public static class FocusedGarmentFields
    {
        public static void AddAccessories(List<ValueNode> roots, FieldSnapshot graph, object state, string source)
        {
            var items = FieldSnapshot.ListItems(FieldSnapshot.ResolveFields(state, "accessoryStates"));
            for (int i = 0; i < items.Length && !graph.Exhausted; i++)
                foreach (string field in new[] { "definition", "placedPos", "placedRot", "surfaceNormal" })
                {
                    if (graph.Exhausted) break;
                    var node = graph.Read(FieldSnapshot.ResolveFields(items[i], field));
                    node.member = source + ".accessoryStates[" + i + "]." + field;
                    roots.Add(node);
                }
        }
    }
}
