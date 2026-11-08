using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class NativeGarmentReport
    {
        public string schemaVersion = "1.0.0", status = "unavailable", reason, source, file, sha256, sourceFingerprint;
        public string serializer = "Framework.JSONSerializer", gameAssemblySha256, frameworkAssemblySha256;
        public int bytes;
        public bool detachedCopy, isNativeGarmentPayload;
        public string limitation = "Native garment state only; order, client preferences, deterministic completion and editor operations are separate. Unknown profiles/callbacks/layouts fail before serialization.";
    }
    internal sealed class NativeGarmentResult
    {
        internal NativeGarmentReport Report = new NativeGarmentReport();
        internal string Json;
        internal XElement Root;
        internal Func<bool> SourceUnchanged;
    }
    internal interface INativeMemorySession
    {
        void CheckCallbacks(IEnumerable<Type> types);
        void Begin(); object Serialize(object copy); void End(); string Format(object value);
    }
    internal static class ReviewedNativeExport
    {
        internal const string GameHash = "441da5e958f7b27c9d6de65874914874d490e670e13163aa516410f8cf4d9e53";
        internal const string FrameworkHash = "e53a0a1e864c8cffd393bed4e3e640363d9f086759f92323ea9b5cb09239543c";
        private static Type cachedStateType;
        private static NativeStateContract cachedContract;
        private static Type cachedSerializer;
        internal static NativeGarmentResult Capture(object state, string source, string version, Func<object, string> assetName)
        {
            var result = new NativeGarmentResult(); result.Report.source = source;
            try
            {
                if (state == null || version != "0.7.0") throw new InvalidDataException("Unsupported native state/version");
                Type type = state.GetType();
                if (cachedContract == null || cachedStateType != type)
                {
                    var game = type.Assembly;
                    var framework = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetType("Framework.JSONSerializer", false) != null);
                    VerifyAssembly(game, "Assembly-CSharp", GameHash); VerifyAssembly(framework, "Framework", FrameworkHash);
                    var types = new Dictionary<string, Type>();
                    foreach (string name in new[] { "DressState", "PanelState", "PanelJoinState", "AccessoryItemState", "Measurements", "CollarDefinition", "BodiceDefinition", "SleeveDefinition", "SkirtDefinition", "MannequinDisplayColor", "Fabric", "AccessoryDefinition" })
                        types.Add(name, game.GetType(name, true));
                    var engine = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "UnityEngine.CoreModule");
                    types.Add("Vector3", engine.GetType("UnityEngine.Vector3", true)); types.Add("Quaternion", engine.GetType("UnityEngine.Quaternion", true));
                    types.Add("Pair", typeof(ValueTuple<,>).MakeGenericType(types["Vector3"], types["Vector3"]));
                    var contract = new NativeStateContract(types, framework.GetType("Framework.ScriptableEnum", true), assetName);
                    cachedSerializer = framework.GetType("Framework.JSONSerializer", true); cachedStateType = type; cachedContract = contract;
                }
                result = CaptureChecked(state, source, cachedContract, new FrameworkMemorySession(cachedSerializer));
                result.Report.gameAssemblySha256 = GameHash; result.Report.frameworkAssemblySha256 = FrameworkHash;
            }
            catch (Exception error) { result.Report.status = "blocked"; result.Report.reason = Reason(error); }
            return result;
        }
        private static void VerifyAssembly(Assembly assembly, string name, string expected)
        {
            if (assembly == null || assembly.GetName().Name != name || string.IsNullOrEmpty(assembly.Location)) throw new InvalidDataException("Unreviewed " + name + " assembly");
            var path = new FileInfo(assembly.Location);
            if (!path.Exists || path.Length > 32 * 1024 * 1024) throw new InvalidDataException("Assembly fingerprint unavailable");
            using (var file = path.OpenRead()) using (var sha = SHA256.Create())
                if (Hex(sha.ComputeHash(file)) != expected) throw new InvalidDataException("Unreviewed " + name + " fingerprint");
        }
        internal static NativeGarmentResult CaptureChecked(object state, string source, NativeStateContract contract, INativeMemorySession session)
        {
            var result = new NativeGarmentResult(); result.Report.source = source;
            try
            {
                var snapshot = contract.Snapshot(state);
                session.CheckCallbacks(snapshot.ObjectTypes()); // before any session/registered handler invocation.
                object nativeValue;
                bool started = false;
                try { session.Begin(); started = true; nativeValue = session.Serialize(snapshot.Copy); }
                finally { if (started) session.End(); }
                string json = session.Format(nativeValue);
                var root = NativeDressJson.Parse(json);
                NativeDressJson.Match(root, snapshot);
                string fingerprint = snapshot.Fingerprint();
                if (contract.Snapshot(state).Fingerprint() != fingerprint) throw new InvalidDataException("Native source changed during capture");
                result.Json = json; result.Root = root;
                result.SourceUnchanged = () => contract.Snapshot(state).Fingerprint() == fingerprint;
                result.Report.status = "accepted"; result.Report.file = "native-garment.json"; result.Report.bytes = Encoding.UTF8.GetByteCount(json);
                using (var sha = SHA256.Create()) result.Report.sha256 = Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(json)));
                result.Report.sourceFingerprint = fingerprint; result.Report.detachedCopy = true; result.Report.isNativeGarmentPayload = true;
            }
            catch (Exception error) { result.Report.status = "blocked"; result.Report.reason = Reason(error); }
            return result;
        }
        private static string Reason(Exception error)
        {
            if (error is TargetInvocationException invocation && invocation.InnerException != null) error = invocation.InnerException;
            return error is InvalidDataException ? error.Message : error.GetType().Name;
        }
        private static string Hex(byte[] value) => BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
    }
    internal sealed class FrameworkMemorySession : INativeMemorySession
    {
        private readonly Type type, configType, logType;
        private object serializer;
        internal FrameworkMemorySession(Type type)
        { this.type = type; configType = type.GetNestedType("Config"); logType = type.GetNestedType("LogLevel"); }
        public void CheckCallbacks(IEnumerable<Type> types)
        {
            // Reviewed private registry, read without dictionary enumeration, comparers or delegate calls.
            var registry = type.GetField("_customSerializationMethods", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            if (registry == null || !registry.GetType().IsGenericType || registry.GetType().GetGenericTypeDefinition() != typeof(Dictionary<,>)) throw new InvalidDataException("Native callback registry unavailable");
            var arguments = registry.GetType().GetGenericArguments();
            if (arguments[0] != typeof(Type) || arguments[1].FullName != "Framework.JSONSerializationMethod") throw new InvalidDataException("Unreviewed callback registry type");
            var comparerField = registry.GetType().GetField("_comparer", BindingFlags.Instance | BindingFlags.NonPublic) ?? registry.GetType().GetField("comparer", BindingFlags.Instance | BindingFlags.NonPublic);
            if (comparerField == null) throw new InvalidDataException("Unreviewed callback comparer layout");
            object comparer = comparerField.GetValue(registry);
            if (comparer != null)
            {
                Type ct = comparer.GetType();
                if (ct.Assembly != typeof(object).Assembly || !ct.IsGenericType || ct.GetGenericArguments()[0] != typeof(Type) ||
                    !new[] { "ObjectEqualityComparer`1", "GenericEqualityComparer`1" }.Contains(ct.GetGenericTypeDefinition().Name))
                    throw new InvalidDataException("Unreviewed callback comparer");
            }
            object countValue = FieldSnapshot.ResolveFields(registry, "_count") ?? FieldSnapshot.ResolveFields(registry, "count");
            var entries = (FieldSnapshot.ResolveFields(registry, "_entries") ?? FieldSnapshot.ResolveFields(registry, "entries")) as Array;
            if (!(countValue is int count) || count < 0 || count > 256 || (count > 0 && (entries == null || entries.Length < count))) throw new InvalidDataException("Native callback registry budget/layout");
            var known = types.ToArray();
            var keyField = count == 0 ? null : entries.GetType().GetElementType().GetField("key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (count > 0 && (keyField == null || keyField.FieldType != typeof(Type))) throw new InvalidDataException("Unreviewed callback entry layout");
            for (int i = 0; i < count; i++)
            {
                object key = keyField.GetValue(entries.GetValue(i));
                if (key != null && key.GetType() != typeof(object).GetType()) throw new InvalidDataException("Unreviewed callback key");
                if (known.Any(t => ReferenceEquals(t, key))) throw new InvalidDataException("Registered native serialization callback");
            }
        }
        public void Begin()
        {
            serializer = Activator.CreateInstance(type); // reviewed constructor owns only its session/dictionary.
            object config = Activator.CreateInstance(configType);
            configType.GetField("SerializeObjectReferences").SetValue(config, false);
            type.GetMethod("BeginSerialization", new[] { configType, logType }).Invoke(serializer, new[] { config, Enum.ToObject(logType, 0) }); // reviewed None=0.
        }
        public object Serialize(object copy) => type.GetMethod("Serialize", new[] { typeof(object) }).Invoke(serializer, new[] { copy });
        public void End() => type.GetMethod("EndSerialization", Type.EmptyTypes).Invoke(serializer, null);
        public string Format(object value)
        {
            if (value == null || value.GetType() != type.Assembly.GetType("Framework.JSONObject")) throw new InvalidDataException("Native serializer returned no garment object");
            return type.Assembly.GetType("Framework.JSONValue").GetMethod("GetJSONString", new[] { typeof(bool) }).Invoke(value, new object[] { false }) as string;
        }
    }
}
