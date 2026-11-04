using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class TextEvidence
    {
        public string source, text;
        public bool truncated;
    }
    [Serializable] public sealed class BriefEvidence
    {
        public string schemaVersion = "1.0.0";
        public string capturedAtUtc;
        public string status = "candidate-text-only";
        public string meaning = "Visible/stored UI text fields; not a complete brief and not automatically assigned to any order";
        public List<TextEvidence> observations = new List<TextEvidence>();
        public void ReadTextFields(object component, string source)
        {
            if (component == null) return;
            foreach (var type in FieldSnapshot.Hierarchy(component.GetType()))
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Take(128))
                {
                    if (observations.Count >= 32) { status = "text-limit"; return; }
                    if (field.FieldType != typeof(string) || field.Name.IndexOf("text", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string value;
                    try { value = field.GetValue(component) as string; } catch { continue; }
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    observations.Add(new TextEvidence { source = source + "." + type.FullName + "." + field.Name,
                        text = value.Substring(0, Math.Min(value.Length, 4096)), truncated = value.Length > 4096 });
                }
        }
    }
}
