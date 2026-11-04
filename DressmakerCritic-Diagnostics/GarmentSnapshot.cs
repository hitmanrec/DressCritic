using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class GarmentSnapshot
    {
        public string schemaVersion = "1.0.0";
        public string capturedAtUtc, unityScene, screen, garmentStatus;
        public string source = "GameManager.activeDress.state";
        public string completeness = "partial: commission, unlocks and visual appearance unresolved";
        public string commissionStatus = "unknown", unlockedInventoryStatus = "unknown", visualAppearanceStatus = "not-captured";
        public bool? sewingComplete, finished;
        public List<SelectedComponent> components = new List<SelectedComponent>();
        public int? panelCount, accessoryCount, joinStateCount;
        public string panelsStatus, accessoriesStatus;
        public List<GarmentPanel> panels = new List<GarmentPanel>();
        public List<GarmentAccessory> accessories = new List<GarmentAccessory>();
    }
    [Serializable] public sealed class AssetIdentity
    {
        public string status, guid, prettyName;
    }
    [Serializable] public sealed class SelectedComponent
    {
        public string slot;
        public AssetIdentity definition;
        public int? variant;
        public string skirtType, collarType;
        public bool? usesSleeves, detached;
    }
    [Serializable] public sealed class GarmentPanel
    {
        public int index;
        public string status;
        public bool? hasBeenCut, placedOnMannequin, hasBeenStitched;
        public double? grainQuality;
        public string grainQualityMeaning = "raw game value; scale not verified";
        public AssetIdentity fabric;
        public string colorStatus = "unknown";
    }
    [Serializable] public sealed class GarmentAccessory
    {
        public int index;
        public string status;
        public AssetIdentity definition;
        public bool? placed;
        public double? scale;
        public Coordinate placedPos, placedRot;
    }
    [Serializable] public sealed class Coordinate
    {
        public string status;
        public double? x, y, z, w;
    }

    // Pure transformation of diagnostic DTOs, shared by live export and offline tests.
    // Never inspects game objects or guesses a current dress from loaded copies.
    public sealed class GarmentSnapshotBuilder
    {
        private readonly List<ValueNode> roots;
        private readonly Dictionary<int, ValueNode> ids = new Dictionary<int, ValueNode>();
        public GarmentSnapshotBuilder(List<ValueNode> roots)
        {
            this.roots = roots ?? new List<ValueNode>();
            foreach (var root in this.roots) Index(root, 0);
        }
        private void Index(ValueNode node, int depth)
        {
            if (node == null || depth > 32) return;
            if (node.id != 0) ids[node.id] = node;
            foreach (var child in node.children ?? new List<ValueNode>()) Index(child, depth + 1);
        }
        private ValueNode Resolve(ValueNode node)
        {
            if (node != null && node.referenceId != 0)
            {
                ValueNode target;
                return ids.TryGetValue(node.referenceId, out target) && target.referenceId == 0 ? target : null;
            }
            return node;
        }
        internal ValueNode Root(string label) => Resolve(roots.FirstOrDefault(n => n != null && n.member == label));
        internal ValueNode Field(ValueNode node, string name)
        {
            node = Resolve(node);
            return Resolve(node?.children?.FirstOrDefault(c => c != null && c.member != null && c.member.EndsWith("." + name, StringComparison.Ordinal)));
        }
        internal string Scalar(ValueNode node) => Resolve(node)?.status == "scalar" ? Resolve(node).value : null;
        internal string Text(ValueNode node, string name) => Scalar(Field(node, name));
        internal bool? Boolean(ValueNode node, string name)
        { bool value; return bool.TryParse(Text(node, name), out value) ? value : (bool?)null; }
        internal int? Integer(ValueNode node, string name)
        { int value; return int.TryParse(Text(node, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : (int?)null; }
        internal double? Number(ValueNode node, string name)
        {
            double value;
            return double.TryParse(Text(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value) ? value : (double?)null;
        }
        internal string Status(ValueNode node) => Resolve(node)?.status ?? "unavailable";
        internal AssetIdentity Identity(ValueNode node) => new AssetIdentity { status = Status(node), guid = Text(node, "_guid"), prettyName = Text(node, "prettyName") };
        private Coordinate Vector(ValueNode node) => new Coordinate { status = Status(node), x = Number(node, "x"), y = Number(node, "y"), z = Number(node, "z"), w = Number(node, "w") };
        internal List<ValueNode> Items(ValueNode node, out int? count, out string status)
        {
            count = Integer(node, "_size");
            var array = Field(node, "_items");
            if (!count.HasValue || count < 0 || array == null || array.children == null)
            { status = Status(node) == "null" ? "null" : "unavailable"; return new List<ValueNode>(); }
            var items = array.children.Take(Math.Min(count.Value, FieldSnapshot.MaxItems)).Select(Resolve).ToList();
            status = items.Count == count && array.status != "node-limit" ? "complete" : "truncated";
            return items;
        }
        public GarmentSnapshot Build(string capturedAtUtc, string unityScene, string sourceRoot = "GameManager.activeDress.state")
        {
            var state = Root(sourceRoot);
            var result = new GarmentSnapshot { source = sourceRoot, capturedAtUtc = capturedAtUtc, unityScene = unityScene,
                screen = Scalar(Root("GameManager.<CurrentScene>k__BackingField")),
                garmentStatus = Status(state) == "null" ? "absent" : Status(state) == "fields" ? "available" : Status(state),
                sewingComplete = Boolean(state, "sewingComplete"), finished = Boolean(state, "finished") };
            foreach (var slot in new[] { "bodice", "sleeve", "collar", "skirt" })
            {
                var definition = Field(state, slot + "Definition");
                result.components.Add(new SelectedComponent { slot = slot, definition = Identity(definition), variant = Integer(state, slot + "Variant"),
                    skirtType = Text(definition, "skirtType"), collarType = Text(definition, "collarType"),
                    usesSleeves = Boolean(definition, "usesSleeves"), detached = Boolean(definition, "detached") });
            }
            int? count; string status;
            var panels = Items(Field(state, "panelStates"), out count, out status);
            result.panelCount = count; result.panelsStatus = status;
            for (int i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                result.panels.Add(new GarmentPanel { index = i, status = Status(panel),
                    hasBeenCut = Boolean(panel, "hasBeenCut"), placedOnMannequin = Boolean(panel, "placedOnMannequin"),
                    hasBeenStitched = Boolean(panel, "hasBeenStitched"), grainQuality = Number(panel, "grainQuality"), fabric = Identity(Field(panel, "fabric")) });
            }
            var accessories = Items(Field(state, "accessoryStates"), out count, out status);
            result.accessoryCount = count; result.accessoriesStatus = status;
            for (int i = 0; i < accessories.Count; i++)
            {
                var item = accessories[i];
                result.accessories.Add(new GarmentAccessory { index = i, status = Status(item), definition = Identity(Field(item, "definition")),
                    placed = Boolean(item, "placed"), scale = Number(item, "scale"), placedPos = Vector(Root(sourceRoot + ".accessoryStates[" + i + "].placedPos") ?? Field(item, "placedPos")), placedRot = Vector(Root(sourceRoot + ".accessoryStates[" + i + "].placedRot") ?? Field(item, "placedRot")) });
            }
            result.joinStateCount = Integer(Field(state, "joinStates"), "_size");
            return result;
        }
    }
}
