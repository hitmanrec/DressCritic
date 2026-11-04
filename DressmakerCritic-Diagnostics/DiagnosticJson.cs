using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace DressmakerCritic.Diagnostics
{
    // Serialize our DTOs only. Unity's native serializer omitted nested DTO fields
    // in the real v0.2 captures. Never pass live game objects to this writer.
    public static class DiagnosticJson
    {
        public static string Serialize(object report)
        {
            var output = new StringBuilder();
            Write(report, output, 0, new HashSet<object>());
            return output.ToString();
        }
        private static void Write(object value, StringBuilder output, int depth, HashSet<object> path)
        {
            if (depth > 32 || output.Length > 8 * 1024 * 1024) throw new InvalidOperationException("Diagnostic JSON limit exceeded");
            if (value == null) { output.Append("null"); return; }
            if (value is string text) { Quote(text, output); return; }
            if (value is bool flag) { output.Append(flag ? "true" : "false"); return; }
            if (value is int integer) { output.Append(integer.ToString(CultureInfo.InvariantCulture)); return; }
            Type type = value.GetType();
            bool list = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
            if (!list && (type.Assembly != typeof(DiagnosticJson).Assembly ||
                type.Namespace != typeof(DiagnosticJson).Namespace || !type.IsDefined(typeof(SerializableAttribute), false)))
                throw new InvalidOperationException("Only diagnostic DTOs may be serialized");
            if (!path.Add(value)) throw new InvalidOperationException("Cycle in diagnostic DTOs");
            try
            {
                if (list)
                {
                    output.Append('['); var items = (IList)value;
                    for (int i = 0; i < items.Count; i++)
                    { if (i > 0) output.Append(','); Write(items[i], output, depth + 1, path); }
                    output.Append(']');
                }
                else
                {
                    output.Append('{'); bool first = true;
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (!first) output.Append(','); first = false;
                        Quote(field.Name, output); output.Append(':');
                        Write(field.GetValue(value), output, depth + 1, path);
                    }
                    output.Append('}');
                }
            }
            finally { path.Remove(value); }
        }
        private static void Quote(string value, StringBuilder output)
        {
            output.Append('"');
            foreach (char c in value)
            {
                if (output.Length > 8 * 1024 * 1024) throw new InvalidOperationException("Diagnostic JSON size limit exceeded");
                switch (c)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (c < 32 || char.IsSurrogate(c)) output.Append("\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else output.Append(c);
                        break;
                }
            }
            output.Append('"');
        }
    }
}
