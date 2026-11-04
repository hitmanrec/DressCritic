using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using DressmakerMods;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DressmakerCritic.Diagnostics
{
    [BepInPlugin("community.dressmakercritic.diagnostics", "Dressmaker Critic Diagnostics", "0.3.0")]
    [BepInDependency("yuu.dressmaker.mods", "0.7.8")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ModContext context;
        private ConfigEntry<bool> showExportButton;
        private string exportButtonStatus = "";
        private float nextButtonExport;

        private readonly Queue<EventObservation> recentEvents = new Queue<EventObservation>();
        private object lastGarment;
        private object lastFabric;
        private string lastEventUtc;
        private string lastEvent;
        private int lastVariant;
        private readonly List<IDisposable> registrations = new List<IDisposable>();

        private void Awake()
        {
            showExportButton = Config.Bind("Diagnostics", "ShowExportButton", true,
                "Show the diagnostic export overlay in every scene. Writes diagnostic files only.");
            Logger.LogInfo("Dressmaker Critic Diagnostics 0.3.0 loaded (read-only).");
            try
            {
                context = ModLoaderApi.Register(this);
                registrations.Add(ModLifecycleApi.WhenContentReady(context, () =>
                    Logger.LogInfo("ModLoader content ready; use critic-status or critic-export in the console.")));
                registrations.Add(ModLifecycleApi.Subscribe(context, ModLifecycleEvent.SceneLoaded, ev =>
                    ClearObservation(ev.SceneName)));
                registrations.Add(ModLifecycleApi.Subscribe(context, ModLifecycleEvent.SaveLoaded, ev =>
                    ClearObservation("save loaded (slot suppressed)")));

                foreach (GarmentEvent kind in new[] { GarmentEvent.Selected, GarmentEvent.PanelCreated, GarmentEvent.FabricChanged })
                {
                    registrations.Add(GarmentEventsApi.Subscribe(context, kind, ev =>
                    {
                        lastGarment = ev.Garment;
                        lastFabric = ev.Fabric;
                        lastVariant = ev.Variant;
                        lastEvent = ev.Event.ToString();
                        lastEventUtc = DateTime.UtcNow.ToString("O");
                        recentEvents.Enqueue(new EventObservation { timeUtc = lastEventUtc, kind = lastEvent,
                            componentName = ev.Garment == null ? null : ev.Garment.name,
                            componentType = ev.Garment == null ? null : ev.Garment.GetType().AssemblyQualifiedName,
                            variant = ev.Variant, fabricName = ev.Fabric == null ? null : ev.Fabric.name });
                        while (recentEvents.Count > 32) recentEvents.Dequeue();
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
                registrations.Add(DebugConsoleApi.Register("critic-types", "Export candidate runtime type/member metadata (no getters)", _ => ExportExploration(false)));
                registrations.Add(DebugConsoleApi.Register("critic-editor", "Export bounded loaded-scene component fields", _ => ExportExploration(true)));
                Logger.LogInfo("Commands registered: critic-status, critic-export, critic-types, critic-editor (console: Ctrl+`).");
            }
            catch (Exception ex)
            {
                Logger.LogError("Could not register diagnostic console commands: " + ex);
            }
        }

        // BepInEx keeps this plugin alive across scenes. IMGUI draws only;
        // all reflection/file work is gated behind a deliberate button click.
        private void OnGUI()
        {
            if (showExportButton == null || !showExportButton.Value) return;
            float width = Math.Min(300f, Screen.width);
            float x = Math.Max(0f, Screen.width - width - 12f);
            bool previousEnabled = GUI.enabled;
            try
            {
                GUI.enabled = previousEnabled && Time.realtimeSinceStartup >= nextButtonExport;
                if (GUI.Button(new Rect(x, 12f, width, 36f), "Export diagnostics"))
                {
                    nextButtonExport = Time.realtimeSinceStartup + 2f;
                    ExportFromButton();
                }
                GUI.enabled = previousEnabled;
                if (!string.IsNullOrEmpty(exportButtonStatus))
                    GUI.Box(new Rect(x, 52f, width, 76f), exportButtonStatus);
            }
            finally { GUI.enabled = previousEnabled; }
        }

        private void ExportFromButton()
        {
            try
            {
                // Each click has its own directory: comparisons across screens are retained.
                string name = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                string dir = Path.Combine(Paths.ConfigPath, "DressmakerCritic", "captures", name);
                Directory.CreateDirectory(dir);
                string snapshot = ExportSnapshot(dir);
                string editor = ExportExploration(true, dir);
                string types = ExportExploration(false, dir);
                bool complete = File.Exists(Path.Combine(dir, "diagnostics.json")) &&
                    File.Exists(Path.Combine(dir, "editor-runtime.json")) &&
                    File.Exists(Path.Combine(dir, "runtime-types.json")) &&
                    File.Exists(Path.Combine(dir, "garment-snapshot.json"));
                exportButtonStatus = (complete ? "Export complete" : "Partial export - see log") +
                    "\nDressmakerCritic/captures/\n" + name;
                Logger.LogInfo("Button export: " + dir + "\n" + snapshot + "\n" + editor + "\n" + types);
            }
            catch (Exception e)
            {
                exportButtonStatus = "Export failed - see BepInEx log";
                Logger.LogError(e);
            }
        }

        private static string GarmentCatalogPath() =>
            Path.Combine(Paths.ConfigPath, "DressmakerMods", "garment-reference.json");

        private string ExportSnapshot(string directory = null)
        {
            try
            {
                var report = new Snapshot
                {
                    schemaVersion = "0.2.0",
                    runtime = CaptureRuntime(),
                    timeUtc = DateTime.UtcNow.ToString("O"),
                    scene = SceneManager.GetActiveScene().name,
                    unityVersion = Application.unityVersion,
                    gameVersion = Application.version,
                    garmentReferenceFound = File.Exists(GarmentCatalogPath()),
                    questDefinitions = ReadQuestNames(),
                    activeCommissionSummary = ReadActiveCommissions(),
                    gameManagerSummary = ReadGameManager()
                };
                string dir = directory ?? Path.Combine(Paths.ConfigPath, "DressmakerCritic");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "diagnostics.json");
                File.WriteAllText(file, DiagnosticJson.Serialize(report));
                Logger.LogInfo("Read-only diagnostic snapshot written: " + file);
                return "Exported to BepInEx/config/DressmakerCritic/diagnostics.json";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return "Export failed; see BepInEx/LogOutput.log: " + ex.GetType().Name;
            }
        }

        private void ClearObservation(string scene)
        {
            recentEvents.Clear();
            lastGarment = null; lastFabric = null; lastEventUtc = null; lastEvent = null; lastVariant = 0;
            Logger.LogInfo("Scene loaded: " + scene + "; event observation cleared.");
        }

        private RuntimeReport CaptureRuntime()
        {
            var graph = new FieldSnapshot();
            return new RuntimeReport {
                recentEvents = recentEvents.ToList(),
                lastEventUtc = lastEventUtc, lastEvent = lastEvent, lastVariant = lastVariant,
                garmentObservation = graph.Read(lastGarment), fabricObservation = graph.Read(lastFabric)
            };
        }

        private string ExportExploration(bool editor, string directory = null)
        {
            try
            {
                var report = new Exploration { timeUtc = DateTime.UtcNow.ToString("O"), scene = SceneManager.GetActiveScene().name };
                if (editor)
                {
                    var graph = new FieldSnapshot(4096);
                    var candidates = new List<Component>();
                    var queue = new Queue<Transform>();
                    // Unity returns the roots as an array; traversal after this allocation is bounded.
                    foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects().Take(128)) queue.Enqueue(root.transform);
                    int visited = 0;
                    while (queue.Count > 0 && visited++ < 128)
                    {
                        var t = queue.Dequeue();
                        foreach (var component in t.gameObject.GetComponents<Component>().Take(32))
                            if (component != null && component.GetType().Assembly.GetName().Name == "Assembly-CSharp")
                                if (candidates.Count < 256) candidates.Add(component);
                        for (int i = 0; i < t.childCount && i < 64 && queue.Count < 128; i++) queue.Enqueue(t.GetChild(i));
                    }
                    // Capture proven paths before generic graphs can consume the budget.
                    var manager = candidates.FirstOrDefault(c => c.GetType().FullName == "GameManager");
                    if (manager != null)
                    {
                        AddFocused(report, graph, manager, "GameManager.<CurrentScene>k__BackingField", "<CurrentScene>k__BackingField");
                        AddFocused(report, graph, manager, "GameManager.<LastScene>k__BackingField", "<LastScene>k__BackingField");
                        var state = FieldSnapshot.ResolveFields(manager, "activeDress", "state");
                        var panels = FieldSnapshot.ListItems(FieldSnapshot.ResolveFields(state, "panelStates"));
                        for (int i = 0; i < panels.Length && !graph.Exhausted; i++)
                            AddFocused(report, graph, panels[i], "GameManager.activeDress.state.panelStates[" + i + "].fabric", "fabric");
                        AddFocused(report, graph, manager, "GameManager.activeDress.state", "activeDress", "state");
                        AddFocused(report, graph, manager, "GameManager.proposedQuest", "proposedQuest");
                    }
                    // These types/instance fields are proven by the uploaded metadata.
                    // Native search excludes assets and includes inactive loaded scene objects;
                    // array allocation is not bounded, but inspection is capped on explicit export.
                    foreach (string typeName in new[] { "Dress", "PatternPanel" })
                    {
                        Type gameType = AppDomain.CurrentDomain.GetAssemblies()
                            .Where(a => a.GetName().Name == "Assembly-CSharp")
                            .Select(a => a.GetType(typeName, false)).FirstOrDefault(t => t != null);
                        if (gameType == null || !typeof(Component).IsAssignableFrom(gameType)) continue;
                        var loaded = UnityEngine.Object.FindObjectsOfType(gameType, true);
                        int count = Math.Min(loaded.Length, typeName == "Dress" ? 8 : 32);
                        for (int i = 0; i < count && !graph.Exhausted; i++)
                        {
                            var component = loaded[i] as Component;
                            if (component == null) continue;
                            string label = "loaded." + typeName + "[" + i + "]";
                            report.objects.Add(new ValueNode { member = label + ".scene", type = "System.String",
                                value = component.gameObject.scene.name, status = "scalar" });
                            AddFocused(report, graph, component, label + ".state", "state");
                            if (typeName == "PatternPanel")
                            {
                                AddFocused(report, graph, component, label + ".state.fabric", "state", "fabric");
                                AddFocused(report, graph, component, label + ".definition", "definition");
                            }
                        }
                        report.note += typeName + " loaded=" + loaded.Length + ", inspected up to " + count + ". ";
                    }
                    foreach (var component in candidates.OrderBy(c => c.GetType().FullName == "GameManager" ? 0 : Candidate(c.GetType().Name) ? 1 : 2))
                    {
                        if (graph.Exhausted) break;
                        report.objects.Add(graph.Read(component, FieldSnapshot.MaxDepth - 1));
                    }
                    report.note += "Focused field paths from real v0.2.1 metadata precede candidate components; 4096 shared nodes. " +
                        "Scene traversal capped at 128 objects/32 components/64 children/256 candidates; list focus capped at 32 panels. " +
                        "Native result arrays are allocated before caps. Missing focused paths yield null; null does not prove absence. " +
                        (graph.Exhausted ? "Graph budget exhausted." : "Graph budget available.");
                }
                else
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "Assembly-CSharp" || a.GetName().Name == "DressmakerMods"))
                    {
                        Type[] types;
                        try { types = assembly.GetTypes(); }
                        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); report.note = "Partial type load."; }
                        foreach (var type in types.OrderBy(t => t.FullName).Where(t => Candidate(t.Name)).Take(128))
                            report.types.Add(FieldSnapshot.Describe(type));
                    }
                    report.note += " Metadata only; at most 128 candidate types per assembly. Static values and property getters are never read.";
                }
                string dir = directory ?? Path.Combine(Paths.ConfigPath, "DressmakerCritic");
                Directory.CreateDirectory(dir);
                string name = editor ? "editor-runtime.json" : "runtime-types.json";
                File.WriteAllText(Path.Combine(dir, name), DiagnosticJson.Serialize(report));
                if (editor)
                {
                    var garment = new GarmentSnapshotBuilder(report.objects).Build(report.timeUtc, report.scene);
                    File.WriteAllText(Path.Combine(dir, "garment-snapshot.json"), DiagnosticJson.Serialize(garment));
                }
                return "Exported " + name + (editor ? " and garment-snapshot.json" : "");
            }
            catch (Exception e) { Logger.LogError(e); return "Exploration failed: " + e.GetType().Name; }
        }

        private static void AddFocused(Exploration report, FieldSnapshot graph, object root, string label, params string[] path)
        {
            if (graph.Exhausted) return;
            var node = graph.Read(FieldSnapshot.ResolveFields(root, path));
            node.member = label;
            report.objects.Add(node);
        }

        private static bool Candidate(string name)
        {
            return new[] { "garment", "pattern", "panel", "fabric", "color", "colour", "accessor", "quest", "commission", "progress", "unlock", "editor", "dress", "manager", "cuttingroom", "frontdesk", "mannequin" }
                .Any(term => name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // Legacy keys are retained, but speculative getters are intentionally no longer executed.
        private static List<string> ReadQuestNames() => new List<string>();
        private static List<string> ReadActiveCommissions() => new List<string> { "Unresolved: inspect scene candidate fields; no commission getter invoked" };
        private static List<string> ReadGameManager() => new List<string> { "Unresolved: static singleton getters disabled" };

        [Serializable] public sealed class EventObservation
        {
            public string timeUtc, kind, componentName, componentType, fabricName;
            public int variant;
        }
        [Serializable] public sealed class RuntimeReport
        {
            public string status = "exploratory; latest event is not the complete current design";
            public string lastEventUtc, lastEvent;
            public int lastVariant;
            public List<EventObservation> recentEvents;
            public ValueNode garmentObservation, fabricObservation;
            public string commissionStatus = "unknown";
            public string unlockedPatternsStatus = "unknown; catalog membership does not imply unlocked";
            public string editorConstraintsStatus = "unknown; inspect runtime fields and catalog compatibility";
        }
        [Serializable] public sealed class Exploration
        {
            public string schemaVersion = "0.2.0";
            public string timeUtc, scene, note;
            public List<ValueNode> objects = new List<ValueNode>();
            public List<TypeDescription> types = new List<TypeDescription>();
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
            public string schemaVersion;
            public RuntimeReport runtime;
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
