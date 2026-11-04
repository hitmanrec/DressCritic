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
        Check(new FieldSnapshot().Read(null).status == "null", "unavailable object");
    }
}
