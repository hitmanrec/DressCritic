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
    [Serializable] public sealed class FixedViewEvidence
    {
        public string status = "not-started", reason;
        public string lighting = "isolated-scene-fixed-directional-rig-v1";
        public int copiedMeshes;
        public int sourceCameraMask;
        public bool approvedForInference = false;
        public int rendererCount, cameraMask, width = 768, height = 768;
        public double centerX, centerY, centerZ, extentX, extentY, extentZ, orthographicSize;
        public List<string> materials = new List<string>();
        public List<string> renderers = new List<string>();
        public int skippedEmptyMeshes;
        public List<FixedViewRecord> views = new List<FixedViewRecord>();
        public string limitation = "Runtime verification required: framing, renderer coverage, lighting and mannequin axis semantics. Axes are not yet named front/back. No live camera pose is used.";
    }
    public static class FixedViewCapture
    {
        private static void AddLight(GameObject root, string name, Vector3 direction, float intensity)
        {
            var obj = new GameObject("DressCritic" + name); obj.transform.SetParent(root.transform, false);
            obj.transform.rotation = Quaternion.LookRotation(-direction.normalized, Vector3.up);
            var light = obj.AddComponent<Light>(); light.type = LightType.Directional;
            light.color = Color.white; light.intensity = intensity; light.shadows = LightShadows.None; light.cullingMask = 1 << 30;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        // Only mod-owned scene objects are modified. Original meshes/materials are shared read-only.
        public static void Capture(string directory, object selectedState, object progress)
        {
            var report = new FixedViewEvidence();
            Scene previewScene = default(Scene); GameObject previewRoot = null;
            GameObject cameraObject = null; RenderTexture target = null; Texture2D pixels = null;
            var previous = RenderTexture.active;
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
                // Include the mannequin as framing evidence; actual dress coverage needs image verification.
                var renderers = mannequin.GetComponentsInChildren<Renderer>(false).Concat(dress.GetComponentsInChildren<Renderer>(false))
                    .Where(r => r != null && r.enabled && (original.cullingMask & (1 << r.gameObject.layer)) != 0).Distinct().Take(257).ToArray();
                if (renderers.Length == 0 || renderers.Length > 256) throw new InvalidOperationException("Preview renderer count outside 1-256");
                previewScene = SceneManager.CreateScene("DressCriticPreview-" + Guid.NewGuid().ToString("N"));
                previewRoot = new GameObject("DressCriticIsolatedPreview");
                SceneManager.MoveGameObjectToScene(previewRoot, previewScene);
                // Empty animation renderers can report world-origin bounds. Derive
                // bounds from actual mesh geometry in mannequin space, not world AABBs.
                Bounds localBounds = new Bounds(); bool hasBounds = false;
                foreach (var renderer in renderers)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    var skin = renderer as SkinnedMeshRenderer;
                    var mesh = skin != null ? skin.sharedMesh : filter == null ? null : filter.sharedMesh;
                    if (mesh == null || mesh.vertexCount == 0) { report.skippedEmptyMeshes++; continue; }
                    Bounds geometry = skin != null ? skin.localBounds : mesh.bounds;
                    report.renderers.Add(renderer.GetType().FullName + ":" + renderer.name + ":vertices=" + mesh.vertexCount +
                        ":localCenter=" + geometry.center + ":localExtent=" + geometry.extents + ":position=" + renderer.transform.position);
                    if (geometry.extents.sqrMagnitude < 0.00000001f) { report.skippedEmptyMeshes++; continue; }
                    if (skin != null) throw new InvalidOperationException("Skinned geometry requires explicit pose baking; not copied as a static mesh");
                    var item = new GameObject("PreviewMesh", typeof(MeshFilter), typeof(MeshRenderer));
                    item.transform.SetParent(previewRoot.transform, false); item.layer = 30;
                    var relative = mannequin.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    item.transform.localPosition = relative.GetColumn(3);
                    item.transform.localRotation = relative.rotation; item.transform.localScale = relative.lossyScale;
                    item.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var copy = item.GetComponent<MeshRenderer>();
                    var materials = renderer.sharedMaterials;
                    if (materials.Length > 64) throw new InvalidOperationException("Material slot limit exceeded");
                    foreach (var material in materials)
                    {
                        string description = material == null ? "null material" : material.name + ":" + (material.shader == null ? "no shader" : material.shader.name);
                        if (!report.materials.Contains(description)) report.materials.Add(description);
                    }
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
                if (!hasBounds) throw new InvalidOperationException("No nonempty mesh geometry for framing");
                Vector3 center = localBounds.center;
                float radius = localBounds.extents.magnitude;
                if (!Finite(radius) || radius < 0.01f || radius > 100f) throw new InvalidOperationException("Invalid mesh framing radius");
                report.rendererCount = renderers.Length; report.sourceCameraMask = original.cullingMask; report.cameraMask = 1 << 30;
                report.centerX = center.x; report.centerY = center.y; report.centerZ = center.z;
                // Extents are mannequin-local, explicitly independent of viewer yaw.
                report.extentX = localBounds.extents.x; report.extentY = localBounds.extents.y; report.extentZ = localBounds.extents.z;
                cameraObject = new GameObject("DressCriticFixedViewCamera") { hideFlags = HideFlags.HideAndDontSave };
                cameraObject.transform.SetParent(previewRoot.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.enabled = false; camera.scene = previewScene;
                camera.cullingMask = 1 << 30;
                AddLight(previewRoot, "Key", new Vector3(-3, 4, 4), 0.9f);
                AddLight(previewRoot, "Fill", new Vector3(3, 2, 2), 0.5f);
                AddLight(previewRoot, "Back", new Vector3(0, 3, -4), 0.7f);
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
                    camera.transform.position = center + directions[i].normalized * (radius * 3 + 0.1f);
                    camera.transform.LookAt(center, Vector3.up);
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
                if (!ReferenceEquals(FieldSnapshot.ResolveFields(dress, "state"), selectedState) || !GameRuntimeReader.DisplayBinding(progress, selectedState).verified)
                    throw new InvalidOperationException("Preview identity changed during fixed capture");
                report.status = "candidate-views-awaiting-runtime-review";
            }
            catch (Exception e) { report.status = "failed"; report.reason = e.ToString(); throw; }
            finally
            {
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
