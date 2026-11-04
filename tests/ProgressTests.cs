using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DressmakerCritic.Diagnostics;

static class ProgressTests
{
    // Synthetic fixtures use only the known name/layout; no proprietary API is emulated.
    static Type Fixture(bool initializer)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("ProgressFixture" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("Fixture").DefineType("PlayerProgress", TypeAttributes.Public);
        type.DefineDefaultConstructor(MethodAttributes.Public);
        type.DefineField("_current", type, FieldAttributes.Private | FieldAttributes.Static);
        if (initializer)
        {
            var il = type.DefineTypeInitializer().GetILGenerator();
            il.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Throw); // Any accidental initialization makes this test fail.
        }
        return type.CreateType();
    }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static void Run()
    {
        string status;
        Check(ProgressAccess.TryRead(null, out status) == null && status == "type-unavailable", "missing progress type is explicit");
        Check(ProgressAccess.TryRead(typeof(ProgressTests), out status) == null && status == "type-not-allowed", "progress access refuses unrelated types");
        Check(ProgressAccess.TryRead(Fixture(true), out status) == null && status.StartsWith("blocked:"), "static initializer is not executed");
        Type safe = Fixture(false);
        Check(ProgressAccess.TryRead(safe, out status) == null && status == "no-current-progress", "null backing field is not fabricated progress");
        object current = Activator.CreateInstance(safe);
        safe.GetField("_current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, current);
        Check(ReferenceEquals(ProgressAccess.TryRead(safe, out status), current) && status == "available", "initializer-free backing field can be observed");
        var list = new List<int>{3, 8};
        Check((int)FieldSnapshot.ListItemAt(list, 1) == 8 && FieldSnapshot.ListItemAt(list, -1) == null && FieldSnapshot.ListItemAt(list, 2) == null, "selected commission index bounds");
        Check(FieldSnapshot.ListItemAt(new object(), 0) == null, "selected access refuses custom collections");
        var json = DiagnosticJson.Serialize(new ContextRuntime { progressAccessStatus = "blocked: static initializer has not been audited" });
        using (var doc = System.Text.Json.JsonDocument.Parse(json))
            Check(doc.RootElement.GetProperty("objects").GetArrayLength() == 0, "blocked context exports explicit status and no invented values");
    }
}
