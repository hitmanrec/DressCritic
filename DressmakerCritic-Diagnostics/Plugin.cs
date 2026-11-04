using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using DressmakerMods;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DressmakerCritic.Diagnostics
{
    [BepInPlugin("community.dressmakercritic.diagnostics", "Dressmaker Critic Diagnostics", "0.1.0")]
    [BepInDependency("yuu.dressmaker.mods", "0.7.8")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ModContext context;
        private readonly List<IDisposable> registrations = new List<IDisposable>();

        private void Awake()
        {
            Logger.LogInfo("Dressmaker Critic Diagnostics 0.1.0 loaded (read-only).");
            try
            {
                context = ModLoaderApi.Register(this);
                registrations.Add(ModLifecycleApi.WhenContentReady(context, () =>
                    Logger.LogInfo("ModLoader content ready; use critic-status or critic-export in the console.")));
                registrations.Add(ModLifecycleApi.Subscribe(context, ModLifecycleEvent.SceneLoaded, ev =>
                    Logger.LogInfo("Scene loaded: " + ev.SceneName)));
                registrations.Add(ModLifecycleApi.Subscribe(context, ModLifecycleEvent.SaveLoaded, ev =>
                    Logger.LogInfo("Game save loaded (slot number suppressed in diagnostics).")));

                foreach (GarmentEvent kind in new[] { GarmentEvent.Selected, GarmentEvent.PanelCreated, GarmentEvent.FabricChanged })
                {
                    registrations.Add(GarmentEventsApi.Subscribe(context, kind, ev =>
                    {
                        string garmentName = ev.Garment == null ? "(none)" : ev.Garment.name;
                        Logger.LogInfo("Garment event: " + ev.Event +
                                       "; component=" + garmentName +
                                       "; variant=" + ev.Variant +
                                       "; fabric=" + (ev.Fabric == null ? "(none)" : ev.Fabric.name));
                    }));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin initialization error: " + ex);
            }
        }

        private void Start()
        {
            try
            {
                registrations.Add(DebugConsoleApi.Register("critic-status", "Show Dressmaker Critic diagnostic status", _ =>
                {
                    string s = "Critic Diagnostics OK | scene=" + SceneManager.GetActiveScene().name +
                        " | garment catalog=" + (File.Exists(GarmentCatalogPath()) ? "found" : "missing");
                    Logger.LogInfo(s);
                    return s;
                }));
                registrations.Add(DebugConsoleApi.Register("critic-export", "Export read-only diagnostic data to JSON", _ => ExportSnapshot()));
                Logger.LogInfo("Commands registered: critic-status, critic-export (console: Ctrl+`).");
            }
            catch (Exception ex)
            {
                Logger.LogError("Could not register diagnostic console commands: " + ex);
            }
        }

        private static string GarmentCatalogPath() =>
            Path.Combine(Paths.ConfigPath, "DressmakerMods", "garment-reference.json");

        private string ExportSnapshot()
        {
            try
            {
                var report = new Snapshot
                {
                    timeUtc = DateTime.UtcNow.ToString("O"),
                    scene = SceneManager.GetActiveScene().name,
                    unityVersion = Application.unityVersion,
                    gameVersion = Application.version,
                    garmentReferenceFound = File.Exists(GarmentCatalogPath()),
                    questDefinitions = ReadQuestNames(),
                    activeCommissionSummary = ReadActiveCommissions(),
                    gameManagerSummary = ReadGameManager()
                };
                string dir = Path.Combine(Paths.ConfigPath, "DressmakerCritic");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "diagnostics.json");
                File.WriteAllText(file, JsonUtility.ToJson(report, true));
                Logger.LogInfo("Read-only diagnostic snapshot written: " + file);
                return "Exported to BepInEx/config/DressmakerCritic/diagnostics.json";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return "Export failed; see BepInEx/LogOutput.log: " + ex.GetType().Name;
            }
        }

        private static List<string> ReadQuestNames()
        {
            var result = new List<string>();
            object raw = ReadStaticMember("QuestDefinition", "AllQuestDefinitions");
            if (raw is IEnumerable items)
            {
                foreach (object item in items)
                {
                    if (item == null) continue;
                    result.Add(DisplayName(item));
                }
            }
            return result;
        }

        private static List<string> ReadActiveCommissions()
        {
            var result = new List<string>();
            object progress = ReadStaticMember("PlayerProgress", "Current");
            if (progress == null) { result.Add("PlayerProgress.Current unavailable"); return result; }
            object raw = ReadMember(progress, "activeCommissions");
            if (!(raw is IEnumerable items)) { result.Add("activeCommissions unavailable"); return result; }
            foreach (object item in items)
            {
                if (item == null) continue;
                object quest = ReadMember(item, "questDefinition") ?? ReadMember(item, "quest") ??
                               ReadMember(item, "definition") ?? ReadMember(item, "questDef");
                result.Add(quest != null ? DisplayName(quest) : "runtime type: " + item.GetType().FullName);
            }
            return result;
        }

        private static List<string> ReadGameManager()
        {
            var result = new List<string>();
            object game = ReadStaticMember("GameManager", "Instance");
            if (game == null) { result.Add("GameManager.Instance unavailable"); return result; }
            object scene = ReadMember(game, "CurrentScene");
            if (scene != null) result.Add("CurrentScene: " + scene);
            object proposed = ReadMember(game, "proposedQuest");
            if (proposed != null) result.Add("proposedQuest: " + DisplayName(proposed));
            return result;
        }

        private static string DisplayName(object item)
        {
            if (item == null) return "(none)";
            if (item is UnityEngine.Object unityObject) return unityObject.name;
            object value = ReadMember(item, "name") ?? ReadMember(item, "id") ?? ReadMember(item, "Id");
            return value == null ? item.GetType().FullName : Convert.ToString(value);
        }

        private static object ReadStaticMember(string typeName, string memberName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = asm.GetType(typeName, false);
                if (type == null) continue;
                return ReadMember(type, memberName);
            }
            return null;
        }

        private static object ReadMember(object instanceOrType, string name)
        {
            if (instanceOrType == null) return null;
            bool isStatic = instanceOrType is Type;
            Type type = isStatic ? (Type)instanceOrType : instanceOrType.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                 (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            try
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null) return field.GetValue(isStatic ? null : instanceOrType);
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(isStatic ? null : instanceOrType, null);
            }
            catch { /* Diagnostic mode should not alter gameplay if a getter fails. */ }
            return null;
        }

        private void OnDestroy()
        {
            foreach (IDisposable registration in registrations)
            {
                try { registration.Dispose(); } catch { }
            }
            registrations.Clear();
            if (context != null) context.Dispose();
        }

        [Serializable]
        public sealed class Snapshot
        {
            public string timeUtc;
            public string scene;
            public string gameVersion;
            public string unityVersion;
            public bool garmentReferenceFound;
            public List<string> questDefinitions;
            public List<string> activeCommissionSummary;
            public List<string> gameManagerSummary;
        }
    }
}
