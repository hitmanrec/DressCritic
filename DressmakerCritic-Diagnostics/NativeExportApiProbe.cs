using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Globalization;

namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class NativeExportApiReport
    {
        public string schemaVersion = "1.0.0", status = "metadata-only", nativeSerializationStatus = "not-invoked-unverified";
        public bool isNativeGarmentPayload = false, truncated;
        public int inspectedIlBytes, instructionCount;
        public List<NativeExportType> types = new List<NativeExportType>();
        public List<NativeExportAssembly> assemblies = new List<NativeExportAssembly>();
        public List<NativeExportMethod> methods = new List<NativeExportMethod>();
        public List<string> unavailable = new List<string>();
        public string limitation = "No game method, constructor, getter or attribute constructor is executed. No raw IL, string literals, file paths or game assets are exported. Call graph is bounded and cannot establish purity: virtual dispatch, reflection, native calls and callbacks may be unresolved. Absence of writes is not proof of read-only serialization.";
    }
    [Serializable] public sealed class NativeExportAssembly
    { public string name, version, moduleVersionId; }
    [Serializable] public sealed class NativeExportType
    {
        public TypeDescription members;
        public List<string> interfaces = new List<string>(), attributes = new List<string>(), methods = new List<string>();
        public List<NativeExportField> fields = new List<NativeExportField>();
    }
    [Serializable] public sealed class NativeExportField
    { public string signature; public bool isStatic; public List<string> attributes = new List<string>(); }
    [Serializable] public sealed class NativeExportMethod
    {
        public string signature, assembly, status, bodySha256;
        public bool isStatic, isVirtual, isAbstract;
        public int depth, bodyBytes;
        public List<string> attributes = new List<string>();
        public List<string> indirectOperations = new List<string>();
        public List<NativeExportReference> references = new List<NativeExportReference>();
    }
    [Serializable] public sealed class NativeExportReference
    { public string operation, member, assembly, status; }

    // Reflection metadata only. In particular, ExportDress must NEVER be invoked here:
    // observed UI entrypoints return void and may select a state, open dialogs or write files.
    public sealed class NativeExportApiProbe
    {
        public const int MaxMethods = 64, MaxTypes = 12, MaxDepth = 4, MaxBodyBytes = 16384,
            MaxTotalBytes = 131072, MaxInstructions = 8192, MaxReferences = 64;
        private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).GroupBy(c => c.Value).ToDictionary(g => g.Key, g => g.First());
        private readonly NativeExportApiReport report = new NativeExportApiReport();
        private readonly HashSet<Assembly> allowed = new HashSet<Assembly>();
        private readonly HashSet<MethodBase> seen = new HashSet<MethodBase>();
        private readonly HashSet<MethodBase> queued = new HashSet<MethodBase>();
        private readonly Queue<Tuple<MethodBase, int>> pending = new Queue<Tuple<MethodBase, int>>();
        private static string Bound(string text) => text == null ? null : text.Substring(0, Math.Min(text.Length, 1024));
        private static string Name(MemberInfo member)
        {
            if (member is Type type) return Bound(type.FullName);
            var method = member as MethodBase;
            return Bound(method == null ? member.DeclaringType?.FullName + "." + member.Name :
                method.DeclaringType?.FullName + "." + method.Name + "(" + string.Join(",", method.GetParameters().Take(32).Select(p => p.ParameterType.FullName ?? p.ParameterType.Name)) + ")" +
                (method is MethodInfo info ? " -> " + info.ReturnType.FullName : ""));
        }
        private static string Argument(CustomAttributeTypedArgument value) => value.Value is Type type ? type.FullName :
            value.Value is bool boolean ? (boolean ? "true" : "false") : value.ArgumentType.IsEnum && value.Value != null && value.Value.GetType().IsPrimitive ?
            value.ArgumentType.FullName + "=" + Convert.ToString(value.Value, CultureInfo.InvariantCulture) : value.ArgumentType.FullName;
        private static List<string> Attributes(IList<CustomAttributeData> attributes) => attributes.Take(16)
            .Select(a => Bound(a.AttributeType.FullName + "(" + string.Join(",", a.ConstructorArguments.Take(8).Select(Argument)) + ") " +
                string.Join(",", a.NamedArguments.Take(8).Select(v => v.MemberName + "=" + Argument(v.TypedValue))))).ToList();
        public static NativeExportApiReport Capture(Type sketchbook, Type photoScene, Type state, Type jsonObject)
        {
            var probe = new NativeExportApiProbe();
            foreach (var type in new[] { sketchbook, photoScene, state, jsonObject }.Where(t => t != null)) probe.allowed.Add(type.Assembly);
            foreach (var assembly in probe.allowed)
                try { probe.report.assemblies.Add(new NativeExportAssembly { name = assembly.GetName().Name, version = assembly.GetName().Version.ToString(),
                    moduleVersionId = assembly.ManifestModule.ModuleVersionId.ToString() }); }
                catch (Exception e) { probe.report.unavailable.Add("assembly-metadata:" + e.GetType().Name); }
            foreach (var pair in new[] { Tuple.Create("Sketchbook", sketchbook), Tuple.Create("PhotoScene", photoScene) })
            {
                if (pair.Item2 == null) { probe.report.unavailable.Add(pair.Item1); continue; }
                try
                {
                    var methods = pair.Item2.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name == "ExportDress" && m.GetParameters().Length == 0).Take(2).ToArray();
                    if (methods.Length == 0) probe.report.unavailable.Add(pair.Item1 + ".ExportDress");
                    foreach (var method in methods) probe.Enqueue(method, 0);
                }
                catch (Exception e) { probe.report.unavailable.Add(pair.Item1 + ":" + e.GetType().Name); }
            }
            if (state == null) probe.report.unavailable.Add("DressState");
            if (jsonObject == null) probe.report.unavailable.Add("Framework.JSONObject");
            foreach (var type in new[] { state, jsonObject }.Where(t => t != null))
                try
                {
                    foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .Take(128).Where(m => m.Name.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("serializ", StringComparison.OrdinalIgnoreCase) >= 0).Take(8))
                        probe.Enqueue(method, 0);
                }
                catch (Exception e) { probe.report.unavailable.Add(Bound(type.FullName + ":" + e.GetType().Name)); }
            var types = new List<Type> { sketchbook, photoScene, state, jsonObject };
            if (state != null)
                try
                {
                    foreach (var field in state.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Take(64)
                        .Where(f => new[] { "panelStates", "accessoryStates", "joinStates", "measurements" }.Contains(f.Name)))
                    {
                        var type = field.FieldType;
                        types.Add(type.IsArray ? type.GetElementType() : type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0] : type);
                    }
                }
                catch (Exception e) { probe.report.unavailable.Add("state-fields:" + e.GetType().Name); }
            foreach (var type in types.Where(t => t != null).Distinct().Take(MaxTypes))
                try { probe.AddType(type); } catch (Exception e) { probe.report.unavailable.Add(Bound(type.FullName + ":" + e.GetType().Name)); }
            while (probe.pending.Count > 0 && probe.report.methods.Count < MaxMethods && probe.report.instructionCount < MaxInstructions)
            {
                var work = probe.pending.Dequeue();
                if (probe.seen.Add(work.Item1)) probe.ReadMethod(work.Item1, work.Item2);
            }
            if (probe.pending.Count > 0) probe.report.truncated = true;
            if (probe.report.methods.Count == 0) probe.report.status = "entrypoints-unavailable";
            return probe.report;
        }
        private void Enqueue(MethodBase method, int depth)
        {
            if (method == null || !allowed.Contains(method.DeclaringType?.Assembly)) return;
            if (seen.Contains(method) || queued.Contains(method)) return;
            if (depth > MaxDepth || seen.Count + pending.Count >= MaxMethods) { report.truncated = true; return; }
            queued.Add(method); pending.Enqueue(Tuple.Create(method, depth));
        }
        private void AddType(Type type)
        {
            var item = new NativeExportType { members = FieldSnapshot.Describe(type), interfaces = type.GetInterfaces().Take(16).Select(t => Bound(t.FullName)).ToList(),
                attributes = Attributes(type.GetCustomAttributesData()) };
            foreach (var declaring in FieldSnapshot.Hierarchy(type))
                foreach (var field in declaring.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Take(64))
                    item.fields.Add(new NativeExportField { signature = Bound(field.DeclaringType.FullName + "." + field.Name + ": " + field.FieldType.FullName),
                        isStatic = field.IsStatic, attributes = Attributes(field.GetCustomAttributesData()) });
            item.methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Take(128)
                .Select(m => Name(m) + " [" + string.Join("; ", Attributes(m.GetCustomAttributesData())) + "]").ToList();
            report.types.Add(item);
        }
        private void ReadMethod(MethodBase method, int depth)
        {
            var item = new NativeExportMethod { signature = Name(method), assembly = method.DeclaringType.Assembly.GetName().Name, depth = depth,
                isStatic = method.IsStatic, isVirtual = method.IsVirtual, isAbstract = method.IsAbstract };
            report.methods.Add(item);
            try
            {
                var attributes = method.GetCustomAttributesData(); item.attributes = Attributes(attributes);
                foreach (var attribute in attributes.Take(16).Where(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.AsyncStateMachineAttribute"))
                    if (attribute.ConstructorArguments.Count == 1 && attribute.ConstructorArguments[0].Value is Type machine)
                        Enqueue(machine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), depth + 1);
                var body = method.GetMethodBody();
                if (body == null) { item.status = "body-unavailable"; return; }
                byte[] il = body.GetILAsByteArray(); item.bodyBytes = il.Length;
                if (il.Length > MaxBodyBytes || report.inspectedIlBytes + il.Length > MaxTotalBytes)
                { item.status = "byte-limit"; report.truncated = true; return; }
                report.inspectedIlBytes += il.Length;
                using (var hash = SHA256.Create()) item.bodySha256 = BitConverter.ToString(hash.ComputeHash(il)).Replace("-", "").ToLowerInvariant();
                int offset = 0;
                while (offset < il.Length && report.instructionCount < MaxInstructions)
                {
                    report.instructionCount++;
                    short code = il[offset++] == 0xfe ? (short)(0xfe00 | il[offset++]) : (short)il[offset - 1];
                    OpCode op;
                    if (!Codes.TryGetValue(code, out op)) throw new InvalidOperationException("Unknown opcode");
                    if ((op.Name.StartsWith("stind", StringComparison.Ordinal) || op.Name.StartsWith("stelem", StringComparison.Ordinal) ||
                        new[] { "calli", "stobj", "cpobj", "initobj", "cpblk", "initblk" }.Contains(op.Name)) && !item.indirectOperations.Contains(op.Name))
                        item.indirectOperations.Add(op.Name);
                    int size = OperandSize(op.OperandType, il, offset);
                    if (size < 0 || size > il.Length - offset) throw new InvalidOperationException("Invalid operand size");
                    if (op.OperandType == OperandType.InlineMethod || op.OperandType == OperandType.InlineField || op.OperandType == OperandType.InlineTok || op.OperandType == OperandType.InlineType)
                    {
                        if (item.references.Count >= MaxReferences) { report.truncated = true; item.status = "reference-limit"; return; }
                        var reference = new NativeExportReference { operation = op.Name };
                        item.references.Add(reference);
                        try
                        {
                            var member = method.Module.ResolveMember(BitConverter.ToInt32(il, offset), method.DeclaringType.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                            reference.member = Name(member); reference.assembly = member.Module.Assembly.GetName().Name; reference.status = "metadata";
                            if (member is MethodBase called) Enqueue(called, depth + 1);
                        }
                        catch (Exception e) { reference.status = "unresolved:" + e.GetType().Name; }
                    }
                    // String literals and raw IL are deliberately never included in the report.
                    offset += size;
                }
                if (offset != il.Length) { item.status = "instruction-limit"; report.truncated = true; }
                else item.status = "metadata";
            }
            catch (Exception e) { item.status = "unavailable:" + e.GetType().Name; }
        }
        private static int OperandSize(OperandType kind, byte[] il, int offset)
        {
            switch (kind)
            {
                case OperandType.InlineNone: return 0;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: return 1;
                case OperandType.InlineVar: return 2;
                case OperandType.InlineI8: case OperandType.InlineR: return 8;
                case OperandType.InlineSwitch:
                    if (il.Length - offset < 4) return -1;
                    int count = BitConverter.ToInt32(il, offset);
                    return count < 0 || count > (il.Length - offset - 4) / 4 ? -1 : 4 + count * 4;
                default: return 4;
            }
        }
    }
}
