using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace DressmakerCritic.Diagnostics
{
    // Closed, reviewed DressState field contract. No game constructors, enumerators or
    // property getters: serialize an owned deep copy, retaining only read-only asset leaves.
    internal sealed class NativeStateContract
    {
        internal const int MaxNodes = 8192, MaxDepth = 10;
        internal readonly Dictionary<string, Type> Types;
        private readonly Type assetBase;
        private readonly Func<object, string> assetName;
        private readonly Dictionary<Type, FieldInfo[]> fields = new Dictionary<Type, FieldInfo[]>();
        private readonly Dictionary<Type, string> roles = new Dictionary<Type, string>();
        private static readonly Dictionary<string, string> Layouts = new Dictionary<string, string> {
            ["DressState"] = "collarDefinition:CollarDefinition bodiceDefinition:BodiceDefinition sleeveDefinition:SleeveDefinition skirtDefinition:SkirtDefinition bodiceVariant:int skirtVariant:int collarVariant:int sleeveVariant:int mannequinDisplayColor:MannequinDisplayColor panelStates:List/PanelState joinStates:List/PanelJoinState accessoryStates:List/AccessoryItemState measurements:Measurements sewingComplete:bool finished:bool",
            ["PanelState"] = "hasBeenCut:bool placedOnMannequin:bool hasBeenStitched:bool grainQuality:float cutPos:Vector3 cutRotation:Quaternion fabric:Fabric cutoutActive:bool",
            ["PanelJoinState"] = "hasBeenStitched:bool accuracy:float",
            ["AccessoryItemState"] = "definition:AccessoryDefinition placed:bool placedPos:Vector3 placedRot:Quaternion scale:float surfaceNormal:Vector3 normalOffset:float nodes:List/Pair beadCount:int heightOffset:float flushPath:bool widthOffset:float tiltAngleOffset:float pivotOffset:float",
            ["Measurements"] = "bust:float waist:float hips:float",
            ["Vector3"] = "x:float y:float z:float",
            ["Quaternion"] = "x:float y:float z:float w:float",
            ["Pair"] = "Item1:Vector3 Item2:Vector3"
        };
        internal NativeStateContract(Dictionary<string, Type> types, Type assetBase, Func<object, string> assetName)
        {
            Types = types; this.assetBase = assetBase; this.assetName = assetName;
            foreach (var layout in Layouts)
            {
                Type type = types[layout.Key]; roles.Add(type, layout.Key);
                bool math = layout.Key == "Vector3" || layout.Key == "Quaternion";
                if (!math && layout.Key != "Pair" && type.TypeInitializer != null) throw new InvalidDataException("State type initializer is unreviewed");
                if (type.GetInterfaces().Any(i => i.FullName == "Framework.ICustomJSONSerializable")) throw new InvalidDataException("Custom JSON callback interface");
                if (!math && layout.Key != "Pair") CheckObjectMethods(type);
                if (!math) CheckAttributes(type.GetCustomAttributesData());
                var declared = layout.Value.Split(' ').ToDictionary(s => s.Split(':')[0], s => Resolve(s.Split(':')[1]));
                var actual = FieldSnapshot.Hierarchy(type).SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)).ToArray();
                if (actual.Length != declared.Count || actual.Any(f => !f.IsPublic || f.IsInitOnly || !declared.ContainsKey(f.Name) || declared[f.Name] != f.FieldType))
                    throw new InvalidDataException("Unreviewed native layout: " + layout.Key);
                if (!math) foreach (var field in actual) CheckAttributes(field.GetCustomAttributesData());
                fields.Add(type, actual.OrderBy(f => f.Name, StringComparer.Ordinal).ToArray());
            }
        }
        private Type Resolve(string name) => name == "float" ? typeof(float) : name == "int" ? typeof(int) : name == "bool" ? typeof(bool) :
            name.StartsWith("List/", StringComparison.Ordinal) ? typeof(List<>).MakeGenericType(Types[name.Substring(5)]) : Types[name];
        private static void CheckAttributes(IList<CustomAttributeData> attributes)
        {
            foreach (var attribute in attributes)
                if (attribute.AttributeType.Assembly != typeof(object).Assembly &&
                    attribute.AttributeType.FullName != "System.Runtime.CompilerServices.TupleElementNamesAttribute")
                    throw new InvalidDataException("Unreviewed serialization attribute: " + attribute.AttributeType.FullName);
        }
        private static void CheckObjectMethods(Type type)
        {
            foreach (string name in new[] { "Equals", "ToString" })
            {
                var method = type.GetMethod(name, name == "Equals" ? new[] { typeof(object) } : Type.EmptyTypes);
                if (method != null && method.DeclaringType != typeof(object) && method.DeclaringType != typeof(ValueType) && method.DeclaringType.FullName != "UnityEngine.Object")
                    throw new InvalidDataException("Unreviewed object override: " + type.FullName + "." + name);
            }
        }
        internal NativeStateTree Snapshot(object state)
        {
            if (state == null || state.GetType() != Types["DressState"]) throw new InvalidDataException("Native state type mismatch");
            int budget = 0;
            var path = new HashSet<object>(new ReferenceIdentity());
            var tree = Copy(state, Types["DressState"], "root", 0, ref budget, path);
            return tree;
        }
        private NativeStateTree Copy(object value, Type declared, string fieldName, int depth, ref int budget, HashSet<object> path)
        {
            if (++budget > MaxNodes || depth > MaxDepth) throw new InvalidDataException("Native input graph budget");
            if (value == null)
            {
                if (fieldName == "item") throw new InvalidDataException("Null native collection item");
                return new NativeStateTree { Kind = "null" };
            }
            Type type = value.GetType();
            if (assetBase.IsAssignableFrom(declared))
            {
                if (!declared.IsAssignableFrom(type)) throw new InvalidDataException("Asset type mismatch");
                CheckObjectMethods(type);
                string guid = assetBase.GetField("_guid", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.GetValue(value) as string;
                string name = assetName(value);
                if (guid == null || guid.Length != 32 || guid.Any(c => !Uri.IsHexDigit(c)) || name == null || name.Length > 512) throw new InvalidDataException("Native asset identity unavailable");
                return new NativeStateTree { Kind = "asset", Value = guid + "," + name, Copy = value };
            }
            if (type != declared) throw new InvalidDataException("Derived native payload type");
            if (type == typeof(float))
            {
                float number = (float)value;
                if (float.IsNaN(number) || float.IsInfinity(number) || Math.Abs(number) > 1000000) throw new InvalidDataException("Native float budget");
                return new NativeStateTree { Kind = "float", Value = number.ToString("R", CultureInfo.InvariantCulture), Copy = value };
            }
            if (type == typeof(int) || type == typeof(bool)) return new NativeStateTree { Kind = type == typeof(int) ? "int" : "bool", Value = Convert.ToString(value, CultureInfo.InvariantCulture), Copy = value };
            if (!type.IsValueType && !path.Add(value)) throw new InvalidDataException("Native input cycle");
            try
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    object rawCount = FieldSnapshot.ResolveFields(value, "_size");
                    var array = FieldSnapshot.ResolveFields(value, "_items") as Array;
                    int cap = fieldName == "joinStates" ? 512 : fieldName == "nodes" ? 256 : 128;
                    if (!(rawCount is int count) || count < 0 || count > cap || array == null || count > array.Length) throw new InvalidDataException("Native collection budget");
                    var list = (IList)Activator.CreateInstance(type); // exact BCL List<T>, never a game constructor.
                    var result = new NativeStateTree { Kind = "list", Copy = list };
                    for (int i = 0; i < count; i++)
                    {
                        var child = Copy(array.GetValue(i), type.GetGenericArguments()[0], "item", depth + 1, ref budget, path);
                        result.Children.Add(child); list.Add(child.Copy);
                    }
                    return result;
                }
                if (!fields.ContainsKey(type)) throw new InvalidDataException("Unreviewed native type: " + type.FullName);
                object copy;
#pragma warning disable SYSLIB0050
                copy = FormatterServices.GetUninitializedObject(type); // owned snapshot; no game instance constructor.
#pragma warning restore SYSLIB0050
                string role = roles[type];
                var node = new NativeStateTree { Kind = role == "Vector3" || role == "Quaternion" ? "vector" : "object", Copy = copy, PayloadType = type };
                foreach (var field in fields[type])
                {
                    var child = Copy(field.GetValue(value), field.FieldType, field.Name, depth + 1, ref budget, path);
                    child.Name = field.Name; node.Children.Add(child); field.SetValue(copy, child.Copy); // writes only the detached copy.
                }
                return node;
            }
            finally { if (!type.IsValueType) path.Remove(value); }
        }
        private sealed class ReferenceIdentity : IEqualityComparer<object>
        { public new bool Equals(object a, object b) => ReferenceEquals(a, b); public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value); }
    }
    internal sealed class NativeStateTree
    {
        internal string Name, Kind, Value;
        internal object Copy;
        internal Type PayloadType;
        internal List<NativeStateTree> Children = new List<NativeStateTree>();
        internal string Fingerprint()
        {
            var text = new StringBuilder(); Append(text);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        private void Append(StringBuilder text)
        { foreach (string value in new[] { Name, Kind, Value }) text.Append(value?.Length ?? -1).Append(':').Append(value); text.Append('['); foreach (var child in Children) child.Append(text); text.Append(']'); }
        internal IEnumerable<Type> ObjectTypes() => (Kind == "object" ? new[] { PayloadType } : new Type[0]).Concat(Children.SelectMany(c => c.ObjectTypes())).Distinct();
    }
}
