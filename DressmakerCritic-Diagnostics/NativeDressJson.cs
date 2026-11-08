using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DressmakerCritic.Diagnostics
{
    internal static class NativeDressJson
    {
        internal const int MaxBytes = 2 * 1024 * 1024;
        internal static XElement Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException("Native JSON size budget");
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), new XmlDictionaryReaderQuotas { MaxDepth = 16, MaxStringContentLength = MaxBytes, MaxArrayLength = MaxBytes }))
            { reader.MoveToContent(); return XElement.Load(reader); }
        }
        private static string Kind(XElement node) => node?.Attribute("type")?.Value;
        private static double Number(XElement node)
        {
            double value;
            if (Kind(node) != "number" || !double.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidDataException("Invalid native number");
            return value;
        }
        private static void Close(double actual, double expected)
        { if (Math.Abs(actual - expected) > 0.000001 * Math.Max(1, Math.Abs(expected))) throw new InvalidDataException("Native numeric mismatch"); }
        private static string Text(XElement node)
        { if (Kind(node) != "string") throw new InvalidDataException("Native string expected"); return node.Value; }
        private static bool Flag(XElement node)
        { bool value; if (Kind(node) != "boolean" || !bool.TryParse(node.Value, out value)) throw new InvalidDataException("Native boolean expected"); return value; }
        internal static void Match(XElement json, NativeStateTree expected)
        {
            if (expected.Kind == "null") { if (Kind(json) != "null") throw new InvalidDataException("Native null mismatch"); return; }
            if (expected.Kind == "asset") { if (Text(json) != expected.Value) throw new InvalidDataException("Native asset mismatch"); return; }
            if (expected.Kind == "bool") { if (Flag(json) != bool.Parse(expected.Value)) throw new InvalidDataException("Native flag mismatch"); return; }
            if (expected.Kind == "int") { if (Number(json) != int.Parse(expected.Value, CultureInfo.InvariantCulture)) throw new InvalidDataException("Native integer mismatch"); return; }
            if (expected.Kind == "float") { Close(Number(json), double.Parse(expected.Value, CultureInfo.InvariantCulture)); return; }
            if (expected.Kind == "vector")
            {
                var values = VectorValues(Text(json));
                var names = values.Length == 4 ? new[] { "x", "y", "z", "w" } : new[] { "x", "y", "z" };
                if (values.Length != expected.Children.Count) throw new InvalidDataException("Native vector mismatch");
                for (int i = 0; i < names.Length; i++) Close(values[i], double.Parse(expected.Children.Single(c => c.Name == names[i]).Value, CultureInfo.InvariantCulture));
                return;
            }
            if (expected.Kind == "list")
            {
                if (Kind(json) != "array" || json.Elements().Count() != expected.Children.Count) throw new InvalidDataException("Native collection mismatch");
                var values = json.Elements().ToArray(); for (int i = 0; i < values.Length; i++) Match(values[i], expected.Children[i]); return;
            }
            if (Kind(json) != "object" || json.Elements().Count() != expected.Children.Count || expected.Children.Any(c => json.Elements(c.Name).Count() != 1))
                throw new InvalidDataException("Native object layout mismatch");
            foreach (var child in expected.Children) Match(json.Element(child.Name), child);
        }
        private static double[] VectorValues(string text)
        {
            var parts = text.Split(',');
            if (text.Length > 256 || (parts.Length != 3 && parts.Length != 4)) throw new InvalidDataException("Native vector format");
            return parts.Select(p => { double n; if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out n) || double.IsNaN(n) || double.IsInfinity(n)) throw new InvalidDataException("Native vector number"); return n; }).ToArray();
        }
        private static Coordinate Coordinate(XElement node)
        {
            if (Kind(node) == "null") return new Coordinate { status = "null" };
            var values = VectorValues(Text(node));
            return new Coordinate { status = "native-export", x = values[0], y = values[1], z = values[2], w = values.Length == 4 ? (double?)values[3] : null };
        }
        private static IEnumerable<XElement> Items(XElement node) => Kind(node) == "array" ? node.Elements() : Enumerable.Empty<XElement>();
        internal static GarmentSnapshot Normalize(XElement root, GarmentSnapshot metadata)
        {
            // The native payload supplies state. Existing identity-matched metadata only
            // supplies friendly asset names and definition facts absent from .dress.
            var identities = metadata.components.Select(c => c.definition).Concat(metadata.panels.Select(p => p.fabric)).Concat(metadata.accessories.Select(a => a.definition))
                .Where(a => a?.guid != null).GroupBy(a => a.guid).ToDictionary(g => g.Key, g => g.First());
            Func<XElement, AssetIdentity> identity = node => {
                if (Kind(node) == "null") return new AssetIdentity { status = "null" };
                string value = Text(node), guid = value.Split(',')[0];
                if (guid.Length != 32 || value.Length < 33 || value[32] != ',') throw new InvalidDataException("Native reference format");
                AssetIdentity known; identities.TryGetValue(guid, out known);
                return new AssetIdentity { status = "fields", guid = guid, prettyName = known?.prettyName };
            };
            var result = new GarmentSnapshot { capturedAtUtc = metadata.capturedAtUtc, unityScene = metadata.unityScene, screen = metadata.screen, source = metadata.source,
                garmentStatus = "available", completeness = "bounded complete native state; identity-matched definition metadata is supplemental; semantic/editor context remains separate",
                finished = Flag(root.Element("finished")), sewingComplete = Flag(root.Element("sewingComplete")) };
            foreach (string slot in new[] { "bodice", "sleeve", "collar", "skirt" })
            {
                var asset = identity(root.Element(slot + "Definition"));
                var known = metadata.components.FirstOrDefault(c => c.slot == slot && c.definition?.guid == asset.guid);
                result.components.Add(new SelectedComponent { slot = slot, definition = asset, variant = (int)Number(root.Element(slot + "Variant")),
                    skirtType = known?.skirtType, collarType = known?.collarType, usesSleeves = known?.usesSleeves, detached = known?.detached });
            }
            foreach (var panel in Items(root.Element("panelStates"))) result.panels.Add(new GarmentPanel { index = result.panels.Count, status = "fields",
                hasBeenCut = Flag(panel.Element("hasBeenCut")), placedOnMannequin = Flag(panel.Element("placedOnMannequin")), hasBeenStitched = Flag(panel.Element("hasBeenStitched")),
                grainQuality = Number(panel.Element("grainQuality")), fabric = identity(panel.Element("fabric")) });
            foreach (var accessory in Items(root.Element("accessoryStates"))) result.accessories.Add(new GarmentAccessory { index = result.accessories.Count, status = "fields",
                definition = identity(accessory.Element("definition")), placed = Flag(accessory.Element("placed")), scale = Number(accessory.Element("scale")),
                placedPos = Coordinate(accessory.Element("placedPos")), placedRot = Coordinate(accessory.Element("placedRot")) });
            result.panelCount = Kind(root.Element("panelStates")) == "null" ? null : (int?)result.panels.Count;
            result.accessoryCount = Kind(root.Element("accessoryStates")) == "null" ? null : (int?)result.accessories.Count;
            result.joinStateCount = Kind(root.Element("joinStates")) == "null" ? null : (int?)Items(root.Element("joinStates")).Count();
            result.panelsStatus = result.panelCount.HasValue ? "complete" : "null"; result.accessoriesStatus = result.accessoryCount.HasValue ? "complete" : "null";
            return result;
        }
        internal static List<EvaluationAssetName> AssetNames(XElement root, List<EvaluationAssetName> metadata)
        {
            var refs = new[] { "bodiceDefinition", "sleeveDefinition", "collarDefinition", "skirtDefinition" }.Select(name => root.Element(name))
                .Concat(Items(root.Element("panelStates")).Select(p => p.Element("fabric")))
                .Concat(Items(root.Element("accessoryStates")).Select(a => a.Element("definition")));
            var native = refs.Where(r => Kind(r) == "string").Select(r => Text(r)).GroupBy(r => r.Substring(0, 32)).ToDictionary(g => g.Key, g => g.First().Substring(33));
            return metadata.Select(m => m.guid).Where(id => id != null && native.ContainsKey(id)).Concat(native.Keys).Distinct().Select(id =>
                new EvaluationAssetName { guid = id, assetName = native[id], runtimeType = metadata.FirstOrDefault(m => m.guid == id)?.runtimeType }).ToList();
        }
    }
}
