using System;
using System.Collections;
using DressmakerCritic.Diagnostics;
class Trap : IEnumerable
{
    public Trap self;
    public int Property => throw new Exception("Getter invoked");
    public IEnumerator GetEnumerator() => throw new Exception("Enumerator invoked");
    public override string ToString() => throw new Exception("ToString invoked");
}
class Program
{
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static void Main()
    {
        var t = new Trap(); t.self = t;
        var n = new FieldSnapshot().Read(t);
        Check(n.children[0].status == "reference" && n.children[0].referenceId == n.id, "cycle identity; no getters, enumeration or ToString");
        Check(FieldSnapshot.Describe(typeof(Trap)).properties.Count > 0, "property metadata only");
        Check(new FieldSnapshot().Read(new int[100]).status == "collection-limit", "array bound");
        Check(new FieldSnapshot().Read(new string('x', 600)).value.Length == 512, "string bound");
        object chain = new object(); for (int i=0;i<10;i++) chain = new object[]{chain};
        n = new FieldSnapshot().Read(chain); for(int i=0;i<4;i++) n=n.children[0];
        Check(n.status == "depth-limit", "depth bound");
        var graph = new FieldSnapshot(); for(int i=0;i<512;i++) graph.Read(i);
        Check(graph.Read(t).status == "node-limit", "shared global node budget");
        var escaped = "quotes\" slash\\ newline\n русский \ud83d\udc57";
        var json = DiagnosticJson.Serialize(new FieldSnapshot().Read(new object[]{escaped, t}));
        using (var document = System.Text.Json.JsonDocument.Parse(json))
        {
            var children = document.RootElement.GetProperty("children");
            Check(children[0].GetProperty("value").GetString() == escaped, "JSON escaping round-trip");
            Check(children[1].GetProperty("children")[0].GetProperty("status").GetString() == "reference", "nested graph fields survive JSON serialization");
        }
        var descriptions = new System.Collections.Generic.List<TypeDescription> { FieldSnapshot.Describe(typeof(Trap)) };
        using (var document = System.Text.Json.JsonDocument.Parse(DiagnosticJson.Serialize(descriptions)))
            Check(document.RootElement[0].GetProperty("properties").GetArrayLength() > 0, "type metadata lists survive JSON serialization");
        bool refused = false;
        try { DiagnosticJson.Serialize(t); } catch (InvalidOperationException) { refused = true; }
        Check(refused, "JSON writer rejects live objects");
        Check(new FieldSnapshot().Read(null).status == "null", "unavailable object");
    }
}
