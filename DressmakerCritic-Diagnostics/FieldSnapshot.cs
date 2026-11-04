using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class ValueNode
    {
        public string type, status, value, member;
        public int id, referenceId;
        public List<ValueNode> children = new List<ValueNode>();
    }
    [Serializable] public sealed class TypeDescription
    {
        public string type;
        public List<string> fields = new List<string>();
        public List<string> properties = new List<string>();
        public string status = "metadata only; members capped at 64 per declaring type, 8 inheritance levels";
    }

    // No arbitrary getters, enumerators, ToString overrides, methods, static reads or writes.
    public sealed class FieldSnapshot
    {
        public const int MaxNodes = 512, MaxDepth = 4, MaxItems = 32, MaxFields = 64, MaxString = 512;
        private int nodes;
        private readonly Dictionary<object, int> seen = new Dictionary<object, int>(new Identity());
        public bool Exhausted => nodes >= MaxNodes;
        private sealed class Identity : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object x) => RuntimeHelpers.GetHashCode(x);
        }
        internal static IEnumerable<Type> Hierarchy(Type type)
        {
            for (int i = 0; type != null && i < 8; i++, type = type.BaseType) yield return type;
        }
        public static TypeDescription Describe(Type type)
        {
            var d = new TypeDescription { type = type.AssemblyQualifiedName };
            foreach (var t in Hierarchy(type))
            {
                foreach (var f in t.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Take(64))
                    d.fields.Add(t.FullName + "." + f.Name + ": " + f.FieldType.FullName + (f.IsStatic ? " [static; not read]" : ""));
                foreach (var p in t.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Take(64))
                    d.properties.Add(t.FullName + "." + p.Name + ": " + p.PropertyType.FullName + " [not invoked]");
            }
            return d;
        }
        public ValueNode Read(object value, int depth = 0)
        {
            var n = new ValueNode();
            if (Exhausted) { n.status = "node-limit"; return n; }
            nodes++;
            if (ReferenceEquals(value, null)) { n.status = "null"; return n; }
            Type type = value.GetType(); n.type = type.AssemblyQualifiedName;
            if (type == typeof(string))
            {
                var s = (string)value; n.value = s.Substring(0, Math.Min(s.Length, MaxString));
                n.status = s.Length > MaxString ? "string-limit" : "scalar"; return n;
            }
            if (type.IsPrimitive || type.IsEnum || type == typeof(decimal))
            { n.value = Convert.ToString(value, CultureInfo.InvariantCulture); n.status = "scalar"; return n; }
            if (!type.IsValueType)
            {
                int id;
                if (seen.TryGetValue(value, out id)) { n.referenceId = id; n.status = "reference"; return n; }
                n.id = nodes; seen.Add(value, n.id);
            }
            if (depth >= MaxDepth) { n.status = "depth-limit"; return n; }
            // Only CLR arrays are indexed. Even IEnumerable/IList implementations can execute game code.
            if (type.IsArray)
            {
                var a = (Array)value;
                if (a.Rank != 1) { n.status = "unsupported-array-rank"; return n; }
                int count = Math.Min(a.Length, MaxItems);
                for (int i = 0; i < count && !Exhausted; i++)
                { var child = Read(a.GetValue(i + a.GetLowerBound(0)), depth + 1); child.member = "[" + i + "]"; n.children.Add(child); }
                n.status = a.Length > count ? "collection-limit" : Exhausted ? "node-limit" : "fields"; return n;
            }
            if (typeof(Delegate).IsAssignableFrom(type) || type == typeof(IntPtr) || type == typeof(UIntPtr) || value is Type || value is MemberInfo)
            { n.status = "opaque"; return n; }
            int fields = 0;
            foreach (var t in Hierarchy(type))
            {
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (fields++ >= MaxFields || Exhausted) { n.status = Exhausted ? "node-limit" : "field-limit"; return n; }
                    ValueNode child;
                    try { child = f.FieldType.IsPointer ? new ValueNode { status = "opaque-pointer" } : Read(f.GetValue(value), depth + 1); }
                    catch (Exception e) { child = new ValueNode { status = "unavailable", value = e.GetType().Name }; }
                    child.member = t.FullName + "." + f.Name; n.children.Add(child);
                }
            }
            n.status = "fields"; return n;
        }
    }
}
