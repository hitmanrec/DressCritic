using System;
using System.Reflection;

namespace DressmakerCritic.Diagnostics
{
    // Static field reads can execute a .cctor. Refuse them when one exists,
    // rather than invoking Current or forcing save/progress initialization.
    public static class ProgressAccess
    {
        public static object TryRead(Type type, out string status)
        {
            status = "type-unavailable";
            if (type == null) return null;
            if (type.FullName != "PlayerProgress") { status = "type-not-allowed"; return null; }
            var field = type.GetField("_current", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field == null || field.FieldType != type) { status = "backing-field-unavailable"; return null; }
            if (type.TypeInitializer != null) { status = "blocked: static initializer has not been audited"; return null; }
            try
            {
                object value = field.GetValue(null);
                status = ReferenceEquals(value, null) ? "no-current-progress" : "available";
                return value;
            }
            catch (Exception e) { status = "unavailable: " + e.GetType().Name; return null; }
        }
    }
    [Serializable] public sealed class ContextRuntime
    {
        public string schemaVersion = "1.0.0";
        public string capturedAtUtc, scene, progressAccessStatus;
        public UnlockedInventory unlockedInventory;
        public string source = "PlayerProgress._current (only if no type initializer exists)";
        public string note = "Exploratory evidence only. Unlock membership is not ownership or proof an editor action is possible. Candidate quest fields may include localization identifiers, not resolved brief text.";
        public System.Collections.Generic.List<ValueNode> objects = new System.Collections.Generic.List<ValueNode>();
    }
}
