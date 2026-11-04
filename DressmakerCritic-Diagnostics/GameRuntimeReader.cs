using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class RuntimeEnvironment
    {
        public string gameVersion, unityVersion, platform, gpu, cpu, renderPipeline, bepinexAssemblyVersion, loaderAssemblyVersion, clrVersion;
        public int gpuMemoryMB, systemMemoryMB;
    }
    [Serializable] public sealed class HistoryProbe
    {
        public int index, panels, accessories;
        public string questGuid, questTitle, thumbnailGuid, thumbnailTextureName;
        public int? thumbnailTextureId;
        public bool? finished, thumbnailDirty;
    }
    [Serializable] public sealed class HistoryCandidates
    {
        public int recordLimit = 16;
        public int? declaredCount;
        public string status;
        public List<GarmentSnapshot> garments = new List<GarmentSnapshot>();
    }
    [Serializable] public sealed class ViewerProbe
    {
        public string objectName, scene, textureName, textureType;
        public int objectId;
        public int? siblingIndex, textureId;
        public bool active;
        public int width, height;
    }
    [Serializable] public sealed class TypeProbe
    {
        public TypeDescription members;
        public List<string> methods = new List<string>();
    }
    [Serializable] public sealed class IdentityEvidence
    {
        public string rule = "Viewer index/name is evidence only; no automatic history binding until semantics verified";
        public List<HistoryProbe> history = new List<HistoryProbe>();
        public List<ViewerProbe> viewers = new List<ViewerProbe>();
        public int? declaredHistoryCount;
        public string progressAccessStatus;
    }
    [Serializable] public sealed class DisplayBindingEvidence
    {
        public int? historyIndex, sketchbookId, displayDressId, assignedMannequinId, previewMannequinId, cameraId, cameraTargetId, viewerTextureId;
        public bool visible, verified;
        public string rule = "Unique exact history state + live display dress assigned to preview mannequin + preview camera target equals visible viewer texture";
    }
    public static class GameRuntimeReader
    {
        public static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "Assembly-CSharp")
            .Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null);
        public static Component[] Find(string name)
        {
            var type = GameType(name);
            if (type == null || !typeof(Component).IsAssignableFrom(type)) return new Component[0];
            return UnityEngine.Object.FindObjectsOfType(type, true).OfType<Component>().Where(c => c != null).ToArray();
        }
        public static DisplayBindingEvidence DisplayBinding(object progress, object selectedState = null)
        {
            var evidence = new DisplayBindingEvidence();
            var books = Find("Sketchbook").Where(c => c.gameObject.activeInHierarchy).Take(2).ToArray();
            var viewers = Find("OldDressView").Where(c => c.gameObject.activeInHierarchy).Take(2).ToArray();
            if (books.Length != 1 || viewers.Length != 1) return evidence;
            var book = books[0];
            var dress = FieldSnapshot.ResolveFields(book, "_displayDress") as Component;
            var mannequin = FieldSnapshot.ResolveFields(book, "previewMannequin") as Component;
            var assigned = FieldSnapshot.ResolveFields(dress, "_assignedMannequin") as Component;
            var camera = FieldSnapshot.ResolveFields(book, "dressPreviewCamera") as Camera;
            var texture = FieldSnapshot.ResolveFields(FieldSnapshot.ResolveFields(viewers[0], "renderImage"), "m_Texture") as Texture;
            var target = camera == null ? null : camera.targetTexture; // Unity camera API; no game property getter.
            object state = FieldSnapshot.ResolveFields(dress, "state");
            evidence.historyIndex = HistoryImageBinding.FindState(progress, state);
            evidence.sketchbookId = book.GetInstanceID(); evidence.displayDressId = dress == null ? (int?)null : dress.GetInstanceID();
            evidence.assignedMannequinId = assigned == null ? (int?)null : assigned.GetInstanceID();
            evidence.previewMannequinId = mannequin == null ? (int?)null : mannequin.GetInstanceID();
            evidence.cameraId = camera == null ? (int?)null : camera.GetInstanceID();
            evidence.cameraTargetId = target == null ? (int?)null : target.GetInstanceID();
            evidence.viewerTextureId = texture == null ? (int?)null : texture.GetInstanceID();
            evidence.visible = dress != null && dress.gameObject.activeInHierarchy && mannequin != null && mannequin.gameObject.activeInHierarchy;
            evidence.verified = evidence.historyIndex.HasValue && HistoryImageBinding.DisplayChain(state, selectedState ?? state, assigned, mannequin, texture, target, evidence.visible);
            return evidence;
        }
        public static RuntimeEnvironment Environment() => new RuntimeEnvironment {
            gameVersion = Application.version, unityVersion = Application.unityVersion, platform = Application.platform.ToString(),
            bepinexAssemblyVersion = typeof(BepInEx.BaseUnityPlugin).Assembly.GetName().Version.ToString(),
            loaderAssemblyVersion = typeof(DressmakerMods.DebugConsoleApi).Assembly.GetName().Version.ToString(), clrVersion = System.Environment.Version.ToString(),
            gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType, gpuMemoryMB = SystemInfo.graphicsMemorySize,
            systemMemoryMB = SystemInfo.systemMemorySize,
            renderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? "built-in" : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().FullName };
        public static List<ValueNode> GarmentGraph(object state, string source, object screen)
        {
            var graph = new FieldSnapshot(4096); var roots = new List<ValueNode>();
            var panelStates = FieldSnapshot.ListItems(FieldSnapshot.ResolveFields(state, "panelStates"));
            for (int i = 0; i < panelStates.Length && !graph.Exhausted; i++)
            {
                var node = graph.Read(FieldSnapshot.ResolveFields(panelStates[i], "fabric")); node.member = source + ".panelStates[" + i + "].fabric"; roots.Add(node);
            }
            FocusedGarmentFields.AddAccessories(roots, graph, state, source);
            var current = graph.Read(state); current.member = source; roots.Add(current);
            var location = graph.Read(screen); location.member = "GameManager.<CurrentScene>k__BackingField"; roots.Add(location);
            return roots;
        }
        public static List<EvaluationAssetName> AssetNames(object state)
        {
            var assets = new List<object>();
            foreach (string field in new[] { "bodiceDefinition", "sleeveDefinition", "collarDefinition", "skirtDefinition" }) assets.Add(FieldSnapshot.ResolveFields(state, field));
            foreach (var panel in FieldSnapshot.ListItems(FieldSnapshot.ResolveFields(state, "panelStates"))) assets.Add(FieldSnapshot.ResolveFields(panel, "fabric"));
            foreach (var item in FieldSnapshot.ListItems(FieldSnapshot.ResolveFields(state, "accessoryStates"))) assets.Add(FieldSnapshot.ResolveFields(item, "definition"));
            var result = new List<EvaluationAssetName>();
            foreach (var value in assets)
            {
                var asset = value as UnityEngine.Object; if (asset == null) continue;
                string guid = FieldSnapshot.ResolveFields(value, "_guid") as string;
                if (guid == null || result.Any(r => r.guid == guid)) continue;
                string name = asset.name;
                result.Add(new EvaluationAssetName { guid = guid, assetName = name == null ? null : name.Substring(0, Math.Min(name.Length, 512)), runtimeType = value.GetType().FullName });
            }
            return result;
        }
        public static IdentityEvidence Identity(object progress, string progressStatus)
        {
            var evidence = new IdentityEvidence { progressAccessStatus = progressStatus };
            var history = FieldSnapshot.ResolveFields(progress, "completedQuests");
            evidence.declaredHistoryCount = FieldSnapshot.ResolveFields(history, "_size") as int?;
            for (int i = 0; i < Math.Min(evidence.declaredHistoryCount ?? 0, 512); i++)
            {
                object record = FieldSnapshot.ListItemAt(history, i);
                var texture = FieldSnapshot.ResolveFields(record, "thumbnailTex") as Texture;
                var state = FieldSnapshot.ResolveFields(record, "dress");
                evidence.history.Add(new HistoryProbe { index = i, questGuid = FieldSnapshot.ResolveFields(record, "questDefinition", "_guid") as string,
                    questTitle = FieldSnapshot.ResolveFields(record, "questDefinition", "prettyName") as string,
                    thumbnailGuid = FieldSnapshot.ResolveFields(record, "thumbnailGuid") as string,
                    thumbnailTextureName = texture == null ? null : texture.name, thumbnailTextureId = texture == null ? (int?)null : texture.GetInstanceID(),
                    finished = FieldSnapshot.ResolveFields(state, "finished") as bool?, thumbnailDirty = FieldSnapshot.ResolveFields(record, "thumbnailDirty") as bool?,
                    panels = FieldSnapshot.ResolveFields(FieldSnapshot.ResolveFields(state, "panelStates"), "_size") as int? ?? -1,
                    accessories = FieldSnapshot.ResolveFields(FieldSnapshot.ResolveFields(state, "accessoryStates"), "_size") as int? ?? -1 });
            }
            foreach (var viewer in Find("OldDressView").Take(32))
            {
                var texture = FieldSnapshot.ResolveFields(FieldSnapshot.ResolveFields(viewer, "renderImage"), "m_Texture") as Texture;
                evidence.viewers.Add(new ViewerProbe { objectName = viewer.name, objectId = viewer.GetInstanceID(), scene = viewer.gameObject.scene.name,
                    active = viewer.gameObject.activeInHierarchy, siblingIndex = FieldSnapshot.ResolveFields(viewer, "siblingIndex") as int?,
                    textureName = texture == null ? null : texture.name, textureType = texture == null ? null : texture.GetType().FullName,
                    textureId = texture == null ? (int?)null : texture.GetInstanceID(), width = texture == null ? 0 : texture.width, height = texture == null ? 0 : texture.height });
            }
            return evidence;
        }
        public static HistoryCandidates HistorySnapshots(object progress, string time, string scene, object screen)
        {
            var result = new HistoryCandidates();
            object history = FieldSnapshot.ResolveFields(progress, "completedQuests");
            result.declaredCount = FieldSnapshot.ResolveFields(history, "_size") as int?;
            result.status = result.declaredCount == null ? "unavailable" : result.declaredCount > 16 ? "partial-first-16-records" : "all-declared-records-inspected";
            for (int i = 0; i < Math.Min(Math.Max(result.declaredCount ?? 0, 0), result.recordLimit); i++)
            {
                object record = FieldSnapshot.ListItemAt(history, i);
                object state = FieldSnapshot.ResolveFields(record, "dress");
                if (state == null) continue;
                string source = "PlayerProgress.completedQuests[" + i + "].dress";
                result.garments.Add(new GarmentSnapshotBuilder(GarmentGraph(state, source, screen)).Build(time, scene, source));
            }
            return result;
        }
        public static List<SelectionOwnerEvidence> SelectionProbe(object progress)
        {
            var result = new List<SelectionOwnerEvidence>();
            foreach (string name in new[] { "Sketchbook", "PhotoScene", "MannequinScene", "Mannequin", "Dress", "OldDressView" })
                foreach (var component in Find(name).Take(8))
                    result.Add(DressmakerCritic.Diagnostics.SelectionProbe.Read(component, progress, name + "#" + component.GetInstanceID() + ":active=" + component.gameObject.activeInHierarchy));
            return result;
        }
        public static List<ValueNode> AccessoryRenderProbe(object progress)
        {
            var result = new List<ValueNode>();
            var books = Find("Sketchbook").Where(c => c.gameObject.activeInHierarchy).Take(2).ToArray();
            if (books.Length != 1) return result;
            object dress = FieldSnapshot.ResolveFields(books[0], "_displayDress");
            var graph = new FieldSnapshot(2048);
            object batcher = FieldSnapshot.ResolveFields(dress, "<AccessoryBatcher>k__BackingField");
            var root = graph.Read(batcher); root.member = "Sketchbook._displayDress.<AccessoryBatcher>k__BackingField"; result.Add(root);
            var items = FieldSnapshot.ResolveFields(dress, "<AccessoryItems>k__BackingField");
            for (int i = 0; i < 4 && !graph.Exhausted; i++)
            {
                object item = FieldSnapshot.ListItemAt(items, i); if (item == null) break;
                var node = graph.Read(item); node.member = "Dress.AccessoryItems[" + i + "]"; result.Add(node);
            }
            return result;
        }
        public static List<TypeProbe> Metadata()
        {
            var result = new List<TypeProbe>();
            foreach (string name in new[] { "OldDressView", "DressHistoryItemUI", "Sketchbook", "MannequinScene", "Mannequin", "Dress", "PhotoScene", "QuestState", "PlayerProgress", "AccessoryItemState", "AccessoryItem", "AccessoryBatchRenderer" })
            {
                var type = GameType(name); if (type == null) continue;
                var probe = new TypeProbe { members = FieldSnapshot.Describe(type) };
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Take(128))
                    probe.methods.Add(method.ReturnType.FullName + " " + method.Name + "(" + string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName)) + ")");
                result.Add(probe);
            }
            result.Add(new TypeProbe { members = FieldSnapshot.Describe(typeof(Vector3)) });
            result.Add(new TypeProbe { members = FieldSnapshot.Describe(typeof(Quaternion)) });
            return result;
        }
    }
}
