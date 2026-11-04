using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DressmakerCritic.Diagnostics
{
    public sealed partial class Plugin
    {
        private static Component[] FindGameComponents(string name)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "Assembly-CSharp")
                .Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null);
            if (type == null || !typeof(Component).IsAssignableFrom(type)) return new Component[0];
            return UnityEngine.Object.FindObjectsOfType(type, true).OfType<Component>().Where(c => c != null).ToArray();
        }
        private static string EvidenceDirectory(string kind)
        {
            string path = Path.Combine(BepInEx.Paths.ConfigPath, "DressmakerCritic", "captures", kind + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(path); return path;
        }
        private void CaptureBriefEvidence()
        {
            try
            {
                var desks = FindGameComponents("FrontDesk");
                if (desks.Length != 1) { reportStatus = "Не удалось однозначно найти окно диалога."; return; }
                var evidence = new BriefEvidence { capturedAtUtc = DateTime.UtcNow.ToString("O") };
                evidence.ReadTextFields(FieldSnapshot.ResolveFields(desks[0], "dialogueText"), "FrontDesk.dialogueText");
                evidence.ReadTextFields(FieldSnapshot.ResolveFields(desks[0], "nameText"), "FrontDesk.nameText");
                if (evidence.observations.Count == 0) { reportStatus = "Текстовых полей не найдено. Откройте диалог и повторите."; return; }
                string directory = EvidenceDirectory("brief");
                File.WriteAllText(Path.Combine(directory, "brief-evidence.json"), DiagnosticJson.Serialize(evidence), System.Text.Encoding.UTF8);
                File.WriteAllText(Path.Combine(directory, "brief-evidence.txt"), "Текстовые свидетельства; привязка к заказу не подтверждена.\n\n" + string.Join("\n\n", evidence.observations.Select(o => o.source + "\n" + o.text)), System.Text.Encoding.UTF8);
                reportStatus = "Текст сохранён: " + directory + ". Может содержать предыдущий диалог; это не подтверждённое задание.";
            }
            catch (Exception e) { reportStatus = "Ошибка чтения текста — см. журнал."; Logger.LogError(e); }
        }
        private void CaptureGarmentImage()
        {
            RenderTexture temporary = null;
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            GameObject cameraObject = null;
            try
            {
                var viewers = FindGameComponents("OldDressView").Where(c => c.gameObject.activeInHierarchy).Take(2).ToArray();
                Texture source = null;
                string sourceName = null;
                if (viewers.Length == 1)
                {
                    // RawImage.m_Texture is an instance field of Unity UI, not a game getter.
                    source = FieldSnapshot.ResolveFields(FieldSnapshot.ResolveFields(viewers[0], "renderImage"), "m_Texture") as Texture;
                    if (source != null) sourceName = "OldDressView.renderImage texture; viewer identity unverified";
                }
                var metadata = new ImageEvidence { capturedAtUtc = DateTime.UtcNow.ToString("O") };
                if (source != null)
                {
                    if (!(source is Texture2D) && !(source is RenderTexture)) { reportStatus = "Формат текстуры просмотра пока не поддерживается."; return; }
                    if (source.width < 1 || source.height < 1) { reportStatus = "Изображение просмотра пусто."; return; }
                    int[] size = ImageEvidence.Fit(source.width, source.height, 2048);
                    metadata.width = size[0]; metadata.height = size[1];
                    metadata.source = sourceName; metadata.uiStatus = "viewer texture only; final content needs inspection";
                    temporary = RenderTexture.GetTemporary(metadata.width, metadata.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(source, temporary);
                }
                else
                {
                    if (savedChoice != null) { reportStatus = "Откройте сохранённое платье в игровом просмотре: его изображение не найдено. Текущее платье подставлять не будем."; return; }
                    var managers = FindGameComponents("GameManager");
                    if (managers.Length != 1) { reportStatus = "Камера платья недоступна."; return; }
                    object screen = FieldSnapshot.ResolveFields(managers[0], "<CurrentScene>k__BackingField");
                    string screenName = screen != null && screen.GetType().IsEnum ? screen.ToString() : "";
                    if (screenName != "Mannequin" && screenName != "Photo") { reportStatus = "Для изображения текущего платья откройте манекен или фотоэкран."; return; }
                    if (FieldSnapshot.ResolveFields(managers[0], "activeDress", "state") == null) { reportStatus = "Нет активного платья для изображения."; return; }
                    if (GraphicsSettings.currentRenderPipeline != null) { reportStatus = "Этот графический конвейер пока не поддерживается. Используйте игровой просмотр сохранённого платья."; return; }
                    var original = FieldSnapshot.ResolveFields(managers[0], "mainCamera") as Camera;
                    if (original == null) { reportStatus = "Основная камера не найдена."; return; }
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
                string directory = EvidenceDirectory("image");
                File.WriteAllBytes(Path.Combine(directory, "image.png"), png);
                if (cameraObject != null)
                {
                    var managers = FindGameComponents("GameManager");
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
                reportStatus = "Изображение сохранено: " + directory + ". Проверьте ракурс и содержимое; привязка к платью не подтверждена.";
            }
            catch (Exception e) { reportStatus = "Не удалось сохранить изображение — см. журнал."; Logger.LogError(e); }
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
