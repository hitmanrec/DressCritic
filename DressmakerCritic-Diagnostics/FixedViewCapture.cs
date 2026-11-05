using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class FixedViewRecord
    {
        public string file, axis;
        public double x, y, z;
    }
    [Serializable] public sealed class LightEvidence
    {
        public string name, scene, type;
        public bool enabled;
        public int mask;
        public double intensity, forwardX, forwardY, forwardZ;
    }
    [Serializable] public sealed class FixedViewEvidence
    {
        public string status = "not-started", reason;
        public string lighting = "isolated-scene-view-key-v3";
        public int copiedMeshes, copiedBatchedAccessories, copiedDisabledRenderers;
        public string previewScene, cameraScene, globalSun, ambientMode;
        public double ambientIntensity;
        public List<LightEvidence> lights = new List<LightEvidence>();
        public int sourceCameraMask;
        public bool approvedForInference = true;
        public int rendererCount, cameraMask, width = 768, height = 768;
        public double centerX, centerY, centerZ, extentX, extentY, extentZ, orthographicSize;
        public List<string> materials = new List<string>();
        public List<string> renderers = new List<string>();
        public int skippedEmptyMeshes, batchGroups, batchEntries;
        public List<FixedViewRecord> views = new List<FixedViewRecord>();
        public string limitation = "Runtime verification required: framing, renderer coverage, lighting and mannequin axis semantics. Axes are not yet named front/back. No live camera pose is used.";
    }
    public static class FixedViewCapture
    {
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void NoteMaterial(FixedViewEvidence report, Material material)
        {
            string description = material == null ? "null material" : material.name + ":" + (material.shader == null ? "no shader" : material.shader.name);
            if (!report.materials.Contains(description)) report.materials.Add(description);
        }
        // AccessoryBatchRenderer keeps 1x1x1 pick proxies (24 verts, extent 0.5) disabled after register.
        // Including them or inactive worldPattern panels recreates the tiny-dress framing bug.
        private static bool IsFramingPolluter(string name, Mesh mesh)
        {
            if (mesh == null) return true;
            if (!string.IsNullOrEmpty(name) && name.IndexOf("worldPattern", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            Bounds geometry = mesh.bounds;
            return mesh.vertexCount > 0 && mesh.vertexCount <= 32
                && geometry.center.sqrMagnitude < 0.0001f
                && Math.Abs(geometry.extents.x - 0.5f) < 0.01f
                && Math.Abs(geometry.extents.y - 0.5f) < 0.01f
                && Math.Abs(geometry.extents.z - 0.5f) < 0.01f;
        }
        private static bool TryCopyMesh(GameObject previewRoot, Transform mannequin, Mesh mesh, Material[] materials, Matrix4x4 worldMatrix,
            FixedViewEvidence report, ref Bounds localBounds, ref bool hasBounds, string label, bool fromBatch, bool wasDisabled, bool expandFraming, MeshRenderer source)
        {
            if (mesh == null || mesh.vertexCount == 0) { report.skippedEmptyMeshes++; return false; }
            Bounds geometry = mesh.bounds;
            if (geometry.extents.sqrMagnitude < 0.00000001f) { report.skippedEmptyMeshes++; return false; }
            if (IsFramingPolluter(label, mesh)) { report.skippedEmptyMeshes++; return false; }
            if (report.copiedMeshes >= 256) throw new InvalidOperationException("Combined garment/accessory mesh limit exceeded");
            if (materials != null && materials.Length > 64) throw new InvalidOperationException("Material slot limit exceeded");
            var item = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(previewRoot.transform, false); item.layer = 30;
            var relative = mannequin.worldToLocalMatrix * worldMatrix;
            item.transform.localPosition = relative.GetColumn(3);
            item.transform.localRotation = relative.rotation; item.transform.localScale = relative.lossyScale;
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            var copy = item.GetComponent<MeshRenderer>();
            copy.sharedMaterials = materials ?? new Material[0];
            // Accessory colors can be instance overrides, just like fabric panels.
            var properties = new MaterialPropertyBlock(); source.GetPropertyBlock(properties); copy.SetPropertyBlock(properties);
            for (int slot = 0; slot < (materials?.Length ?? 0); slot++)
            { source.GetPropertyBlock(properties, slot); if (!properties.isEmpty) copy.SetPropertyBlock(properties, slot); }
            copy.shadowCastingMode = ShadowCastingMode.Off; copy.receiveShadows = false;
            copy.lightProbeUsage = LightProbeUsage.Off; copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (materials != null) foreach (var material in materials) NoteMaterial(report, material);
            report.renderers.Add("MeshRenderer:" + label + ":vertices=" + mesh.vertexCount +
                ":localCenter=" + geometry.center + ":localExtent=" + geometry.extents +
                (fromBatch ? ":batched" : "") + (wasDisabled ? ":was-disabled" : "") + (expandFraming ? "" : ":no-frame"));
            report.copiedMeshes++;
            if (fromBatch) report.copiedBatchedAccessories++;
            if (wasDisabled) report.copiedDisabledRenderers++;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = geometry.center + Vector3.Scale(geometry.extents, new Vector3(x, y, z));
                        Vector3 local = relative.MultiplyPoint3x4(corner);
                        if (!Finite(local.x) || !Finite(local.y) || !Finite(local.z)) throw new InvalidOperationException("Non-finite mesh bounds");
                        if (Vector3.Distance(item.transform.TransformPoint(corner), local) > 0.0001f)
                            throw new InvalidOperationException("Accessory transform has unsupported shear or mirrored decomposition");
                        if (!expandFraming) continue;
                        if (!hasBounds) { localBounds = new Bounds(local, Vector3.zero); hasBounds = true; }
                        else localBounds.Encapsulate(local);
                    }
            return true;
        }
        private static IEnumerable<object> DictionaryValues(object dictionary)
        {
            if (dictionary == null) yield break;
            var type = dictionary.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Dictionary<,>))
                throw new InvalidOperationException("Unsupported accessory dictionary type");
            // Exact BCL Dictionary layout: Entry[] _entries with fields hashCode/next/key/value.
            var entries = FieldSnapshot.ResolveFields(dictionary, "_entries") as Array;
            if (entries == null) yield break;
            if (entries.Length > 1024) throw new InvalidOperationException("Accessory dictionary capacity limit exceeded");
            for (int i = 0; i < entries.Length; i++)
            {
                object entry = entries.GetValue(i);
                if (entry == null) continue;
                object hash = FieldSnapshot.ResolveFields(entry, "hashCode");
                if (!(hash is int) || (int)hash < 0) continue;
                object value = FieldSnapshot.ResolveFields(entry, "value");
                if (!ReferenceEquals(value, null)) yield return value;
            }
        }
        private static void CopyBatchedAccessories(Component dress, Transform mannequin, GameObject previewRoot, FixedViewEvidence report,
            HashSet<int> seenRendererIds, ref Bounds localBounds, ref bool hasBounds)
        {
            // AccessoryBatchRenderer disables MeshRenderers and draws via Graphics.RenderMeshInstanced.
            // Prefer the still-present (disabled) MeshRenderers; fall back to batch Entry.local matrices
            // only when a registration no longer exposes a copyable renderer.
            object batcher = FieldSnapshot.ResolveFields(dress, "<AccessoryBatcher>k__BackingField");
            if (batcher != null)
            {
                object groups = FieldSnapshot.ResolveFields(batcher, "groups");
                foreach (var group in DictionaryValues(groups))
                {
                    report.batchGroups++;
                    var entries = FieldSnapshot.ResolveFields(group, "entries");
                    var items = FieldSnapshot.ResolveFields(entries, "_items") as Array;
                    var sizeObj = FieldSnapshot.ResolveFields(entries, "_size");
                    int size = sizeObj is int ? Math.Max(0, Math.Min((int)sizeObj, items == null ? 0 : items.Length)) : 0;
                    report.batchEntries += size;
                }
                object registered = FieldSnapshot.ResolveFields(batcher, "registered");
                foreach (var registration in DictionaryValues(registered))
                {
                    var renderers = FieldSnapshot.ListItems(FieldSnapshot.ResolveFields(registration, "renderers"));
                    if (FieldSnapshot.ListItemAt(FieldSnapshot.ResolveFields(registration, "renderers"), 32) != null)
                        throw new InvalidOperationException("Accessory registration renderer limit exceeded");
                    foreach (var obj in renderers)
                    {
                        var renderer = obj as MeshRenderer;
                        if (renderer == null || !seenRendererIds.Add(renderer.GetInstanceID())) continue;
                        var filter = renderer.GetComponent<MeshFilter>();
                        // Accessories must not reopen the tiny-dress framing regression; garment bounds stay authoritative.
                        TryCopyMesh(previewRoot, mannequin, filter == null ? null : filter.sharedMesh, renderer.sharedMaterials,
                            renderer.localToWorldMatrix, report, ref localBounds, ref hasBounds,
                            "batched-registration:" + renderer.name, true, !renderer.enabled, false, renderer);
                    }
                }
            }
            // Paths (AccessoryItemPath) are not registered for instancing; copy any remaining accessory meshes.
            var accessoryItems = FieldSnapshot.ResolveFields(dress, "<AccessoryItems>k__BackingField");
            var storage = FieldSnapshot.ResolveFields(accessoryItems, "_items") as Array;
            var countObj = FieldSnapshot.ResolveFields(accessoryItems, "_size");
            int count = countObj is int ? Math.Max(0, Math.Min((int)countObj, storage == null ? 0 : storage.Length)) : 0;
            if (count > 256) throw new InvalidOperationException("Accessory item limit exceeded");
            for (int i = 0; i < count; i++)
            {
                var item = storage.GetValue(i) as Component;
                if (item == null) continue;
                var children = item.GetComponentsInChildren<MeshRenderer>(true);
                if (children.Length > 256) throw new InvalidOperationException("Accessory renderer limit exceeded");
                foreach (var renderer in children)
                {
                    if (renderer == null || !seenRendererIds.Add(renderer.GetInstanceID())) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    TryCopyMesh(previewRoot, mannequin, filter == null ? null : filter.sharedMesh, renderer.sharedMaterials,
                        renderer.localToWorldMatrix, report, ref localBounds, ref hasBounds,
                        "accessory:" + renderer.name, false, !renderer.enabled, false, renderer);
                }
            }
        }
        // Only mod-owned scene objects are modified. Original meshes/materials are shared read-only.
        public static void Capture(string directory, object selectedState, object progress)
        {
            var report = new FixedViewEvidence();
            Scene previewScene = default(Scene); GameObject previewRoot = null;
            GameObject cameraObject = null; RenderTexture target = null; Texture2D pixels = null;
            var previous = RenderTexture.active;
            var previousAmbientMode = RenderSettings.ambientMode;
            var previousAmbient = RenderSettings.ambientLight;
            var previousAmbientIntensity = RenderSettings.ambientIntensity;
            var previousSun = RenderSettings.sun;
            try
            {
                var binding = GameRuntimeReader.DisplayBinding(progress, selectedState);
                if (!binding.verified) throw new InvalidOperationException("Verified historical preview required");
                var book = GameRuntimeReader.Find("Sketchbook").Single(c => c.gameObject.activeInHierarchy);
                var dress = FieldSnapshot.ResolveFields(book, "_displayDress") as Component;
                var mannequin = FieldSnapshot.ResolveFields(book, "previewMannequin") as Component;
                var original = FieldSnapshot.ResolveFields(book, "dressPreviewCamera") as Camera;
                if (dress == null || mannequin == null || original == null || !ReferenceEquals(FieldSnapshot.ResolveFields(dress, "state"), selectedState))
                    throw new InvalidOperationException("Preview changed before fixed capture");
                // Active hierarchy + enabled renderers only for garment framing. Inactive worldPattern
                // panels and disabled pick proxies must not enter this list (tiny-dress regression).
                var renderers = mannequin.GetComponentsInChildren<Renderer>(false).Concat(dress.GetComponentsInChildren<Renderer>(false))
                    .Where(r => r != null && r.enabled && (original.cullingMask & (1 << r.gameObject.layer)) != 0
                        && (r.name == null || r.name.IndexOf("worldPattern", StringComparison.OrdinalIgnoreCase) < 0))
                    .Distinct().Take(257).ToArray();
                if (renderers.Length == 0 || renderers.Length > 256) throw new InvalidOperationException("Preview renderer count outside 1-256");
                previewScene = SceneManager.CreateScene("DressCriticPreview-" + Guid.NewGuid().ToString("N"));
                previewRoot = new GameObject("DressCriticIsolatedPreview");
                SceneManager.MoveGameObjectToScene(previewRoot, previewScene);
                Bounds localBounds = new Bounds(); bool hasBounds = false;
                var seen = new HashSet<int>();
                foreach (var renderer in renderers)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    var skin = renderer as SkinnedMeshRenderer;
                    var mesh = skin != null ? skin.sharedMesh : filter == null ? null : filter.sharedMesh;
                    if (mesh == null || mesh.vertexCount == 0 || IsFramingPolluter(renderer.name, mesh)) { report.skippedEmptyMeshes++; continue; }
                    Bounds geometry = skin != null ? skin.localBounds : mesh.bounds;
                    report.renderers.Add(renderer.GetType().FullName + ":" + renderer.name + ":vertices=" + mesh.vertexCount +
                        ":localCenter=" + geometry.center + ":localExtent=" + geometry.extents + ":position=" + renderer.transform.position);
                    if (geometry.extents.sqrMagnitude < 0.00000001f) { report.skippedEmptyMeshes++; continue; }
                    if (skin != null) throw new InvalidOperationException("Skinned geometry requires explicit pose baking; not copied as a static mesh");
                    seen.Add(renderer.GetInstanceID());
                    var item = new GameObject("PreviewMesh", typeof(MeshFilter), typeof(MeshRenderer));
                    item.transform.SetParent(previewRoot.transform, false); item.layer = 30;
                    var relative = mannequin.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    item.transform.localPosition = relative.GetColumn(3);
                    item.transform.localRotation = relative.rotation; item.transform.localScale = relative.lossyScale;
                    item.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var copy = item.GetComponent<MeshRenderer>();
                    var materials = renderer.sharedMaterials;
                    if (materials.Length > 64) throw new InvalidOperationException("Material slot limit exceeded");
                    foreach (var material in materials) NoteMaterial(report, material);
                    copy.sharedMaterials = materials;
                    copy.shadowCastingMode = ShadowCastingMode.Off; copy.receiveShadows = false;
                    copy.lightProbeUsage = LightProbeUsage.Off; copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties); copy.SetPropertyBlock(properties);
                    for (int material = 0; material < copy.sharedMaterials.Length; material++)
                    { renderer.GetPropertyBlock(properties, material); if (!properties.isEmpty) copy.SetPropertyBlock(properties, material); }
                    report.copiedMeshes++;
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                            for (int z = -1; z <= 1; z += 2)
                            {
                                Vector3 corner = geometry.center + Vector3.Scale(geometry.extents, new Vector3(x, y, z));
                                Vector3 world = renderer.transform.TransformPoint(corner);
                                Vector3 local = mannequin.transform.InverseTransformPoint(world);
                                if (Vector3.Distance(item.transform.TransformPoint(corner), local) > 0.0001f)
                                    throw new InvalidOperationException("Renderer transform has unsupported shear or mirrored decomposition");
                                if (!Finite(local.x) || !Finite(local.y) || !Finite(local.z)) throw new InvalidOperationException("Non-finite mesh bounds");
                                if (!hasBounds) { localBounds = new Bounds(local, Vector3.zero); hasBounds = true; }
                                else localBounds.Encapsulate(local);
                            }
                }
                // Copy batched/path accessories after framing is locked to garment+mannequin bounds.
                CopyBatchedAccessories(dress, mannequin.transform, previewRoot, report, seen, ref localBounds, ref hasBounds);
                if (!hasBounds) throw new InvalidOperationException("No nonempty mesh geometry for framing");
                Vector3 center = localBounds.center;
                float radius = localBounds.extents.magnitude;
                if (!Finite(radius) || radius < 0.01f || radius > 100f) throw new InvalidOperationException("Invalid mesh framing radius");
                report.rendererCount = renderers.Length; report.sourceCameraMask = original.cullingMask; report.cameraMask = 1 << 30;
                report.centerX = center.x; report.centerY = center.y; report.centerZ = center.z;
                report.extentX = localBounds.extents.x; report.extentY = localBounds.extents.y; report.extentZ = localBounds.extents.z;
                cameraObject = new GameObject("DressCriticFixedViewCamera") { hideFlags = HideFlags.HideAndDontSave };
                cameraObject.transform.SetParent(previewRoot.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.enabled = false; camera.scene = previewScene;
                camera.cullingMask = 1 << 30;
                // MannequinFabric/URP break under several conflicting directionals. Prefer strong flat
                // ambient plus one camera-aligned key so every canonical view is front-lit the same way.
                RenderSettings.sun = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.62f, 0.60f, 0.56f, 1f);
                RenderSettings.ambientIntensity = 1f;
                var keyObject = new GameObject("DressCriticViewKey"); keyObject.transform.SetParent(previewRoot.transform, false);
                var keyLight = keyObject.AddComponent<Light>();
                keyLight.type = LightType.Directional; keyLight.color = Color.white; keyLight.intensity = 0.75f;
                keyLight.shadows = LightShadows.None; keyLight.cullingMask = 1 << 30;
                var fillObject = new GameObject("DressCriticViewFill"); fillObject.transform.SetParent(previewRoot.transform, false);
                var fillLight = fillObject.AddComponent<Light>();
                fillLight.type = LightType.Directional; fillLight.color = Color.white; fillLight.intensity = 0.28f;
                fillLight.shadows = LightShadows.None; fillLight.cullingMask = 1 << 30;
                report.previewScene = previewScene.name; report.cameraScene = camera.scene.name;
                report.globalSun = previousSun == null ? null : previousSun.name;
                report.ambientMode = RenderSettings.ambientMode.ToString(); report.ambientIntensity = RenderSettings.ambientIntensity;
                camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1;
                camera.orthographic = true; camera.orthographicSize = radius * 1.12f;
                camera.nearClipPlane = 0.01f; camera.farClipPlane = radius * 8 + 1;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.72f, 0.70f, 0.65f, 1);
                camera.useOcclusionCulling = false;
                report.orthographicSize = camera.orthographicSize;
                target = RenderTexture.GetTemporary(768, 768, 24, RenderTextureFormat.ARGB32);
                pixels = new Texture2D(768, 768, TextureFormat.RGB24, false);
                var directions = new[] { Vector3.forward, -Vector3.forward, Vector3.right, -Vector3.right };
                var axes = new[] { "plus-z", "minus-z", "plus-x", "minus-x" };
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector3 viewDir = directions[i].normalized;
                    camera.transform.position = center + viewDir * (radius * 3 + 0.1f);
                    camera.transform.LookAt(center, Vector3.up);
                    // Key from slightly above the camera; soft fill from the opposite side.
                    keyObject.transform.position = camera.transform.position + Vector3.up * (radius * 0.35f);
                    keyObject.transform.rotation = Quaternion.LookRotation((center - keyObject.transform.position).normalized, Vector3.up);
                    fillObject.transform.position = center - viewDir * (radius * 2f) + Vector3.up * (radius * 0.2f);
                    fillObject.transform.rotation = Quaternion.LookRotation((center - fillObject.transform.position).normalized, Vector3.up);
                    if (GraphicsSettings.currentRenderPipeline == null)
                    { camera.targetTexture = target; camera.Render(); camera.targetTexture = null; }
                    else
                    {
                        var request = new RenderPipeline.StandardRequest { destination = target };
                        if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("Render pipeline does not support StandardRequest");
                        RenderPipeline.SubmitRenderRequest(camera, request);
                    }
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); pixels.Apply();
                    string file = "fixed-axis-" + axes[i] + ".png";
                    File.WriteAllBytes(Path.Combine(directory, file), ImageConversion.EncodeToPNG(pixels));
                    report.views.Add(new FixedViewRecord { file = file, axis = axes[i], x = camera.transform.position.x, y = camera.transform.position.y, z = camera.transform.position.z });
                }
                foreach (var light in UnityEngine.Object.FindObjectsOfType<Light>(true).Take(64))
                    report.lights.Add(new LightEvidence { name = light.name, scene = light.gameObject.scene.name,
                        type = light.type.ToString(), enabled = light.isActiveAndEnabled, mask = light.cullingMask,
                        intensity = light.intensity, forwardX = light.transform.forward.x, forwardY = light.transform.forward.y, forwardZ = light.transform.forward.z });
                if (!ReferenceEquals(FieldSnapshot.ResolveFields(dress, "state"), selectedState) || !GameRuntimeReader.DisplayBinding(progress, selectedState).verified)
                    throw new InvalidOperationException("Preview identity changed during fixed capture");
                report.status = "accepted-fixed-views";
                report.approvedForInference = true;
                report.limitation = "Four mannequin-local orthographic views. +Z front, -Z back; +X/-X labeled side A/B. Accessories copied without expanding garment framing. Physical construction remains unknown.";
            }
            catch (Exception e) { report.status = "failed"; report.reason = e.ToString(); throw; }
            finally
            {
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbient;
                RenderSettings.ambientIntensity = previousAmbientIntensity;
                RenderSettings.sun = previousSun;
                RenderTexture.active = previous;
                if (cameraObject != null) { cameraObject.SetActive(false); var camera = cameraObject.GetComponent<Camera>(); camera.targetTexture = null; UnityEngine.Object.Destroy(cameraObject); }
                if (previewRoot != null) { previewRoot.SetActive(false); UnityEngine.Object.Destroy(previewRoot); }
                if (previewScene.IsValid() && previewScene.isLoaded) SceneManager.UnloadSceneAsync(previewScene);
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                File.WriteAllText(Path.Combine(directory, "fixed-views.json"), DiagnosticJson.Serialize(report));
            }
        }
    }
}
