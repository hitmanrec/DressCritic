using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using DressmakerCritic.Diagnostics;

static class NativeGarmentTests
{
    static int constructors, getters;
    class AssetBase { private string _guid; protected AssetBase(string guid) { _guid = guid; } }
    class Asset : AssetBase { public string name; public Asset(string value) : base(value.Substring(0, 32)) { name = value.Substring(33); } }
    struct Vec { public float x, y, z; }
    struct Rot { public float x, y, z, w; }
    class State
    {
        public Asset collarDefinition, bodiceDefinition, sleeveDefinition, skirtDefinition, mannequinDisplayColor;
        public int bodiceVariant, skirtVariant, collarVariant, sleeveVariant;
        public List<Panel> panelStates;
        public List<Join> joinStates;
        public List<Accessory> accessoryStates;
        public Measures measurements;
        public bool sewingComplete, finished;
        public State() { constructors++; }
        public string Trap { get { getters++; throw new Exception("Getter invoked"); } }
    }
    class Panel { public bool hasBeenCut, placedOnMannequin, hasBeenStitched, cutoutActive; public float grainQuality; public Vec cutPos; public Rot cutRotation; public Asset fabric; }
    class Join { public bool hasBeenStitched; public float accuracy; }
    class Accessory
    {
        public Asset definition; public bool placed, flushPath; public Vec placedPos, surfaceNormal; public Rot placedRot;
        public float scale, normalOffset, heightOffset, widthOffset, tiltAngleOffset, pivotOffset;
        public List<ValueTuple<Vec, Vec>> nodes; public int beadCount;
    }
    class Measures { public float bust, waist, hips; }
    sealed class CallbackState : State, Framework.ICustomJSONSerializable { }
    sealed class ExtraState : State { public int unexpected; }
    [AttributeUsage(AttributeTargets.Class)] sealed class TrapAttribute : Attribute { public TrapAttribute() { throw new Exception("Attribute constructor invoked"); } }
    [Trap] sealed class AttributedState : State { }
    sealed class Session : INativeMemorySession
    {
        internal string Json; internal int begins, ends, serializes; internal object Copy;
        internal bool Block, Throw, MutateCopy; internal Action OnSerialize;
        public void CheckCallbacks(IEnumerable<Type> types) { if (Block) throw new InvalidDataException("Registered callback fixture"); }
        public void Begin() { begins++; }
        public object Serialize(object copy)
        {
            serializes++; Copy = copy; OnSerialize?.Invoke();
            if (MutateCopy) ((State)copy).finished = false;
            if (Throw) throw new InvalidOperationException("Fixture failure");
            return Json;
        }
        public void End() { ends++; }
        public string Format(object value) => (string)value;
    }
    sealed class Registry
    {
        private static Dictionary<Type, Framework.JSONSerializationMethod> _customSerializationMethods = new Dictionary<Type, Framework.JSONSerializationMethod>();
        public static void Set(Dictionary<Type, Framework.JSONSerializationMethod> value) { _customSerializationMethods = value; }
    }
    sealed class TrapComparer : IEqualityComparer<Type>
    { public bool Equals(Type a, Type b) => throw new Exception("Comparer invoked"); public int GetHashCode(Type value) => throw new Exception("Comparer invoked"); }
    static Dictionary<string, Type> Types(Type root = null) => new Dictionary<string, Type> {
        ["DressState"] = root ?? typeof(State), ["PanelState"] = typeof(Panel), ["PanelJoinState"] = typeof(Join), ["AccessoryItemState"] = typeof(Accessory),
        ["Measurements"] = typeof(Measures), ["Vector3"] = typeof(Vec), ["Quaternion"] = typeof(Rot), ["Pair"] = typeof(ValueTuple<Vec, Vec>),
        ["CollarDefinition"] = typeof(Asset), ["BodiceDefinition"] = typeof(Asset), ["SleeveDefinition"] = typeof(Asset), ["SkirtDefinition"] = typeof(Asset),
        ["MannequinDisplayColor"] = typeof(Asset), ["Fabric"] = typeof(Asset), ["AccessoryDefinition"] = typeof(Asset)
    };
    static NativeStateContract Contract(Type root = null) => new NativeStateContract(Types(root), typeof(AssetBase), a => ((Asset)a).name);
    static object FromJson(XElement node, Type type)
    {
        if (node.Attribute("type")?.Value == "null") return null;
        if (type == typeof(Asset)) return new Asset(node.Value);
        if (type == typeof(float)) return float.Parse(node.Value, CultureInfo.InvariantCulture);
        if (type == typeof(int)) return int.Parse(node.Value, CultureInfo.InvariantCulture);
        if (type == typeof(bool)) return bool.Parse(node.Value);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (System.Collections.IList)Activator.CreateInstance(type);
            foreach (var item in node.Elements()) list.Add(FromJson(item, type.GetGenericArguments()[0])); return list;
        }
        object value = Activator.CreateInstance(type, true);
        if (type == typeof(Vec) || type == typeof(Rot))
        {
            string[] names = type == typeof(Vec) ? new[] { "x", "y", "z" } : new[] { "x", "y", "z", "w" };
            var parts = node.Value.Split(','); for (int i = 0; i < names.Length; i++) type.GetField(names[i]).SetValue(value, float.Parse(parts[i], CultureInfo.InvariantCulture));
        }
        else foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public)) field.SetValue(value, FromJson(node.Element(field.Name), field.FieldType));
        return value;
    }
    static string Read(string file)
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../tests/fixtures/dress-exports/", file + ".dress.gz"));
        using (var stream = File.OpenRead(path)) using (var gzip = new GZipStream(stream, CompressionMode.Decompress)) using (var text = new StreamReader(gzip)) return text.ReadToEnd();
    }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    public static void Run()
    {
        foreach (string fixture in new[] { "off-rack", "commission-labelled", "accessories" })
        {
            string json = Read(fixture); var root = NativeDressJson.Parse(json); var state = (State)FromJson(root, typeof(State)); var contract = Contract();
            int ctorCount = constructors;
            var session = new Session { Json = json, MutateCopy = true };
            var result = ReviewedNativeExport.CaptureChecked(state, "verified history state", contract, session);
            Check(result.Report.status == "accepted" && result.Report.detachedCopy && result.Report.isNativeGarmentPayload && session.begins == 1 && session.ends == 1,
                "native export lifecycle and real file field/value match " + fixture);
            Check(!ReferenceEquals(session.Copy, state) && !ReferenceEquals(((State)session.Copy).panelStates, state.panelStates) && state.finished &&
                constructors == ctorCount && getters == 0 && result.SourceUnchanged(), "native serializer gets a deep detached copy without constructors/getters " + fixture);
            var copied = (State)session.Copy;
            Check(!ReferenceEquals(copied.accessoryStates, state.accessoryStates) && !ReferenceEquals(copied.joinStates, state.joinStates) &&
                (state.accessoryStates.Count == 0 || (!ReferenceEquals(copied.accessoryStates[0].nodes, state.accessoryStates[0].nodes) &&
                    ReferenceEquals(copied.accessoryStates[0].definition, state.accessoryStates[0].definition))),
                "mutable accessory paths are copied; only reviewed asset leaves are shared " + fixture);
            var normalized = NativeDressJson.Normalize(result.Root, new GarmentSnapshot());
            Check(normalized.panelCount == state.panelStates.Count && normalized.accessoryCount == state.accessoryStates.Count &&
                normalized.components.Single(c => c.slot == "bodice").definition.guid == root.Element("bodiceDefinition").Value.Substring(0, 32),
                "native payload is the normalized garment state source " + fixture);
            var names = NativeDressJson.AssetNames(root, new List<EvaluationAssetName>());
            Check(names.Any(a => a.guid == normalized.components[0].definition.guid) && names.All(a => a.assetName != state.mannequinDisplayColor.name),
                "native visual asset names exclude mannequin colour " + fixture);
            state.finished = false; Check(!result.SourceUnchanged(), "full native fingerprint detects later source change " + fixture);
        }
        string sourceJson = Read("accessories");
        Func<State> source = () => (State)FromJson(NativeDressJson.Parse(sourceJson), typeof(State));
        var rejected = new Session { Json = sourceJson, Block = true };
        Check(ReviewedNativeExport.CaptureChecked(source(), "fixture", Contract(), rejected).Report.status == "blocked" && rejected.begins == 0,
            "registered native callbacks reject before session start");
        var failing = new Session { Json = sourceJson, Throw = true };
        Check(ReviewedNativeExport.CaptureChecked(source(), "fixture", Contract(), failing).Report.status == "blocked" && failing.ends == 1,
            "native session ends after serialization failure");
        var wrong = new Session { Json = Read("off-rack") };
        Check(ReviewedNativeExport.CaptureChecked(source(), "fixture", Contract(), wrong).Report.status == "blocked", "foreign native garment JSON cannot attach to selected state");
        var moving = source(); var mutation = new Session { Json = sourceJson, OnSerialize = () => moving.finished = false };
        Check(ReviewedNativeExport.CaptureChecked(moving, "fixture", Contract(), mutation).Report.status == "blocked", "source change invalidates native export before acceptance");
        var huge = source(); while (huge.accessoryStates.Count <= 128) huge.accessoryStates.Add(huge.accessoryStates[0]);
        var untouched = new Session { Json = sourceJson };
        Check(ReviewedNativeExport.CaptureChecked(huge, "fixture", Contract(), untouched).Report.status == "blocked" && untouched.begins == 0, "oversized native input rejects before serialization");
        var nonfinite = source(); nonfinite.panelStates[0].grainQuality = float.NaN;
        Check(ReviewedNativeExport.CaptureChecked(nonfinite, "fixture", Contract(), new Session { Json = sourceJson }).Report.status == "blocked", "nonfinite values do not reach native lossy formatting");
        foreach (var type in new[] { typeof(CallbackState), typeof(ExtraState), typeof(AttributedState) })
        {
            bool blocked = false; try { Contract(type); } catch (InvalidDataException) { blocked = true; }
            Check(blocked, "unreviewed callback/layout/attribute contract rejected " + type.Name);
        }
        var malformed = new Session { Json = sourceJson.Replace("\"finished\":true", "\"finished\":true,\"finished\":true") };
        Check(ReviewedNativeExport.CaptureChecked(source(), "fixture", Contract(), malformed).Report.status == "blocked", "duplicate native fields are rejected");
        Check(ReviewedNativeExport.Capture(source(), "fixture", "0.7.0", a => ((Asset)a).name).Report.status == "blocked", "production native adapter refuses unreviewed assemblies");
        var registry = new FrameworkMemorySession(typeof(Registry));
        Registry.Set(new Dictionary<Type, Framework.JSONSerializationMethod> { [typeof(string)] = (s, v) => throw new Exception("Callback invoked") });
        registry.CheckCallbacks(new[] { typeof(State) }); Check(true, "unrelated native handlers are inspected without invocation");
        Registry.Set(new Dictionary<Type, Framework.JSONSerializationMethod> { [typeof(State)] = (s, v) => throw new Exception("Callback invoked") });
        bool denied = false; try { registry.CheckCallbacks(new[] { typeof(State) }); } catch (InvalidDataException) { denied = true; }
        Check(denied, "actual callback registry reader blocks a payload-type handler");
        Registry.Set(new Dictionary<Type, Framework.JSONSerializationMethod>(new TrapComparer()));
        denied = false; try { registry.CheckCallbacks(new[] { typeof(State) }); } catch (InvalidDataException) { denied = true; }
        Check(denied, "registry custom comparer is rejected without execution");
    }
}
namespace Framework { interface ICustomJSONSerializable { } delegate object JSONSerializationMethod(object serializer, object value); }
