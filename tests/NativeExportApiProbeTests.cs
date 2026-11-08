using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using DressmakerCritic.Diagnostics;

static class NativeExportApiProbeTests
{
    [AttributeUsage(AttributeTargets.All)] sealed class TrapAttribute : Attribute
    { public TrapAttribute(Type type) { throw new Exception("Attribute constructor invoked"); } }
    [Trap(typeof(State))] sealed class State
    {
        [Trap(typeof(State))] public int value;
        public string Trap => throw new Exception("Getter invoked");
    }
    sealed class UI
    {
        static UI() { throw new Exception("Game type initializer invoked"); }
        public static void ExportDress()
        {
            Action callback = () => File.WriteAllText("NEVER_EXPORT_THIS_LITERAL_3901", Serialize(new State()));
            callback();
        }
        private static string Serialize(State state) { state.value = 42; return state.Trap; }
    }
    sealed class AsyncUI
    {
        public async void ExportDress() { await Task.Yield(); File.WriteAllText("NEVER_EXPORT_THIS_LITERAL_3901", "unused"); }
    }
    sealed class RecursiveUI { public static void ExportDress() { ExportDress(); } }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    public static void Run()
    {
        var report = NativeExportApiProbe.Capture(typeof(UI), typeof(AsyncUI), typeof(State), null);
        Check(report.nativeSerializationStatus == "not-invoked-unverified" && !report.isNativeGarmentPayload,
            "export discovery never claims a native payload or invokes an export entrypoint");
        Check(report.methods.Any(m => m.signature.Contains("UI.ExportDress(") && m.status == "metadata"),
            "export metadata inspection does not initialize game type or execute UI methods");
        Check(report.methods.SelectMany(m => m.references).Any(r => r.member?.Contains("System.IO.File.WriteAllText") == true),
            "export discovery records file-write calls without executing them");
        Check(report.methods.SelectMany(m => m.references).Any(r => r.operation == "stfld" && r.member?.EndsWith("State.value") == true),
            "mutating serializer candidate field writes are visible as metadata");
        Check(report.methods.SelectMany(m => m.references).Any(r => r.member?.Contains("get_Trap") == true),
            "possible serializer getters are reported without invocation");
        Check(report.methods.Any(m => m.signature.Contains("MoveNext")), "async export state machine is inspected without starting a task");
        string json = DiagnosticJson.Serialize(report);
        Check(!json.Contains("NEVER_EXPORT_THIS_LITERAL_3901") && !File.Exists("NEVER_EXPORT_THIS_LITERAL_3901") &&
            report.methods.Where(m => m.status == "metadata").All(m => m.bodySha256?.Length == 64),
            "report includes body hashes, not literals, raw bytecode or file side effects");
        Check(report.types.Single(t => t.members.type.Contains("+State,")).attributes.Any(a => a.Contains("TrapAttribute")),
            "serializer attribute and converter type metadata require no attribute construction");
        Check(report.methods.Count <= NativeExportApiProbe.MaxMethods && report.instructionCount <= NativeExportApiProbe.MaxInstructions &&
            report.inspectedIlBytes <= NativeExportApiProbe.MaxTotalBytes, "export discovery respects global budgets");
        var missing = NativeExportApiProbe.Capture(null, null, null, null);
        Check(missing.status == "entrypoints-unavailable" && missing.unavailable.Count == 4, "missing game types produce explicit unavailable export discovery");
        var recursive = NativeExportApiProbe.Capture(typeof(RecursiveUI), null, null, null);
        Check(recursive.methods.Count == 1 && recursive.methods[0].status == "metadata", "recursive export references terminate without executing the recursion");

        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("ExportProbeBudgetFixture"), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Fixture");
        var type = module.DefineType("LargeExportFixture", TypeAttributes.Public);
        var method = type.DefineMethod("ExportDress", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
        var il = method.GetILGenerator();
        for (int i = 0; i < NativeExportApiProbe.MaxBodyBytes + 1; i++) il.Emit(OpCodes.Nop);
        il.Emit(OpCodes.Ret);
        var large = NativeExportApiProbe.Capture(type.CreateType(), null, null, null);
        Check(large.truncated && large.methods.Single().status == "byte-limit" && large.inspectedIlBytes == 0,
            "oversized export method is bounded before instruction decoding");
        var chain = module.DefineType("ExportDepthFixture", TypeAttributes.Public);
        var methods = Enumerable.Range(0, NativeExportApiProbe.MaxDepth + 3).Select(i => chain.DefineMethod(i == 0 ? "ExportDress" : "Step" + i,
            MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes)).ToArray();
        for (int i = 0; i < methods.Length; i++)
        {
            var body = methods[i].GetILGenerator();
            if (i + 1 < methods.Length) body.Emit(OpCodes.Call, methods[i + 1]);
            body.Emit(OpCodes.Ret);
        }
        var deep = NativeExportApiProbe.Capture(chain.CreateType(), null, null, null);
        Check(deep.truncated && deep.methods.Count == NativeExportApiProbe.MaxDepth + 1 && deep.methods.All(m => m.depth <= NativeExportApiProbe.MaxDepth),
            "export reference traversal stops at the explicit depth cap");
    }
}
