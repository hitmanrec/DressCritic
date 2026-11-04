using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace DressmakerCritic.Diagnostics
{
    public sealed class CapturedVisuals { public ImageEvidence metadata; public bool directActiveBinding, exactHistoryTexture; }
    public static class GameImageCapture
    {
        public static CapturedVisuals Capture(string directory, Component manager, object selectedState, bool saved, Texture expectedHistoryTexture = null)
        {
            RenderTexture temporary = null;
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            GameObject cameraObject = null;
            try
            {
                var viewers = GameRuntimeReader.Find("OldDressView").Where(c => c.gameObject.activeInHierarchy).Take(2).ToArray();
                Texture source = null;
                string sourceName = null;
                if (viewers.Length == 1)
                {
                    // RawImage.m_Texture is an instance field of Unity UI, not a game getter.
                    source = FieldSnapshot.ResolveFields(FieldSnapshot.ResolveFields(viewers[0], "renderImage"), "m_Texture") as Texture;
                    if (source != null) sourceName = "OldDressView.renderImage texture; viewer identity unverified";
                }
                object currentScreen = FieldSnapshot.ResolveFields(manager, "<CurrentScene>k__BackingField");
                string currentScreenName = currentScreen != null && currentScreen.GetType().IsEnum ? currentScreen.ToString() : "";
                if (!saved && (currentScreenName == "Mannequin" || currentScreenName == "Photo") && GraphicsSettings.currentRenderPipeline == null)
                { source = null; sourceName = null; } // Prefer a verifiable active-state image over an unrelated viewer.
                var metadata = new ImageEvidence { capturedAtUtc = DateTime.UtcNow.ToString("O") };
                if (source != null)
                {
                    if (!(source is Texture2D) && !(source is RenderTexture)) { throw new InvalidOperationException("Формат текстуры просмотра пока не поддерживается."); }
                    if (source.width < 1 || source.height < 1) { throw new InvalidOperationException("Изображение просмотра пусто."); }
                    int[] size = ImageEvidence.Fit(source.width, source.height, 2048);
                    metadata.width = size[0]; metadata.height = size[1];
                    metadata.source = sourceName; metadata.uiStatus = "viewer texture only; final content needs inspection";
                    temporary = RenderTexture.GetTemporary(metadata.width, metadata.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(source, temporary);
                }
                else
                {
                    if (saved) { throw new InvalidOperationException("Откройте сохранённое платье в игровом просмотре: его изображение не найдено. Текущее платье подставлять не будем."); }
                    var managers = GameRuntimeReader.Find("GameManager");
                    if (managers.Length != 1) { throw new InvalidOperationException("Камера платья недоступна."); }
                    object screen = FieldSnapshot.ResolveFields(managers[0], "<CurrentScene>k__BackingField");
                    string screenName = screen != null && screen.GetType().IsEnum ? screen.ToString() : "";
                    if (screenName != "Mannequin" && screenName != "Photo") { throw new InvalidOperationException("Для изображения текущего платья откройте манекен или фотоэкран."); }
                    if (FieldSnapshot.ResolveFields(managers[0], "activeDress", "state") == null) { throw new InvalidOperationException("Нет активного платья для изображения."); }
                    if (GraphicsSettings.currentRenderPipeline != null) { throw new InvalidOperationException("Этот графический конвейер пока не поддерживается. Используйте игровой просмотр сохранённого платья."); }
                    var original = FieldSnapshot.ResolveFields(managers[0], "mainCamera") as Camera;
                    if (original == null) { throw new InvalidOperationException("Основная камера не найдена."); }
                    cameraObject = new GameObject("DressCriticTemporaryCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
                    var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                    camera.CopyFrom(original); camera.enabled = false;
                    camera.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                    int uiLayer = LayerMask.NameToLayer("UI");
                    if (uiLayer >= 0) camera.cullingMask &= ~(1 << uiLayer);
                    int[] size = ImageEvidence.Fit(original.pixelWidth, original.pixelHeight, 1024);
                    metadata.width = size[0]; metadata.height = size[1];
                    camera.rect = new Rect(0, 0, 1, 1); camera.aspect = (float)metadata.width / metadata.height;
                    metadata.source = "temporary copy of GameManager.mainCamera: " + screenName;
                    metadata.uiStatus = "overlay UI not rendered; UI layer excluded; other UI layers unverified";
                    temporary = RenderTexture.GetTemporary(metadata.width, metadata.height, 24, RenderTextureFormat.ARGB32);
                    camera.targetTexture = temporary; camera.Render(); camera.targetTexture = null;
                }
                RenderTexture.active = temporary;
                pixels = new Texture2D(metadata.width, metadata.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, metadata.width, metadata.height), 0, 0); pixels.Apply();
                byte[] png = ImageConversion.EncodeToPNG(pixels);
                if (png == null || png.Length == 0) throw new InvalidOperationException("PNG encoding failed");
                File.WriteAllBytes(Path.Combine(directory, "image.png"), png);
                if (cameraObject != null)
                {
                    var managers = GameRuntimeReader.Find("GameManager");
                    var dress = managers.Length == 1 ? FieldSnapshot.ResolveFields(managers[0], "activeDress") as Component : null;
                    var renderers = dress == null ? new Renderer[0] : dress.GetComponentsInChildren<Renderer>(false).Where(r => r != null && r.enabled).Take(256).ToArray();
                    if (renderers.Length > 0)
                    {
                        Bounds bounds = renderers[0].bounds;
                        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                        var camera = cameraObject.GetComponent<Camera>();
                        Vector3 originalPosition = camera.transform.position;
                        foreach (int yaw in new[] { -35, 35 })
                        {
                            camera.transform.position = bounds.center + Quaternion.AngleAxis(yaw, Vector3.up) * (originalPosition - bounds.center);
                            camera.transform.LookAt(bounds.center);
                            camera.targetTexture = temporary; camera.Render(); camera.targetTexture = null;
                            RenderTexture.active = temporary;
                            pixels.ReadPixels(new Rect(0, 0, metadata.width, metadata.height), 0, 0); pixels.Apply();
                            string name = yaw < 0 ? "angle-left.png" : "angle-right.png";
                            File.WriteAllBytes(Path.Combine(directory, name), ImageConversion.EncodeToPNG(pixels));
                            metadata.files.Add(name);
                        }
                        metadata.viewsStatus = "three candidate views; framing and garment identity require visual verification";
                    }
                }
                File.WriteAllText(Path.Combine(directory, "image-evidence.json"), DiagnosticJson.Serialize(metadata), System.Text.Encoding.UTF8);
                return new CapturedVisuals { metadata = metadata, exactHistoryTexture = source != null && expectedHistoryTexture != null && ReferenceEquals(source, expectedHistoryTexture), directActiveBinding = source == null && !saved &&
                    ReferenceEquals(FieldSnapshot.ResolveFields(manager, "activeDress", "state"), selectedState) };
            }
            finally
            {
                RenderTexture.active = previous;
                if (cameraObject != null) { var camera = cameraObject.GetComponent<Camera>(); if (camera != null) camera.targetTexture = null; UnityEngine.Object.Destroy(cameraObject); }
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            }
        }
    }
}
