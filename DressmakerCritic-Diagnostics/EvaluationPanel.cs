using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace DressmakerCritic.Diagnostics
{
    // Displays a captured result, never claims to describe the currently open garment.
    internal sealed class EvaluationPanel
    {
        public bool open;
        public JuryAssessment result;
        public string subject, message, imagePath;
        private string loadedImagePath;
        private Texture2D preview;
        private bool contextTab;
        private Vector2 scroll;
        private bool showLimitations;
        private GameObject inputShield;
        private RectTransform shieldRect;
        private void Shield(float width, float height)
        {
            if (inputShield == null)
            {
                inputShield = new GameObject("DressCriticResultInput", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                UnityEngine.Object.DontDestroyOnLoad(inputShield);
                var canvas = inputShield.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
                var surface = new GameObject("PanelInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                surface.transform.SetParent(inputShield.transform, false);
                var image = surface.GetComponent<Image>(); image.color = Color.clear; image.raycastTarget = true;
                image.canvasRenderer.cullTransparentMesh = false;
                shieldRect = surface.GetComponent<RectTransform>();
                shieldRect.anchorMin = shieldRect.anchorMax = new Vector2(0.5f, 0.5f);
                shieldRect.anchoredPosition = Vector2.zero;
            }
            inputShield.SetActive(true);
            shieldRect.sizeDelta = new Vector2(width, height);
        }
        public void Dispose() { if (inputShield != null) UnityEngine.Object.Destroy(inputShield); if (preview != null) UnityEngine.Object.Destroy(preview); }
        private static string Number(double? value) => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) + " / 10" : "Недостаточно данных";
        private static string House(string id) => id == "harmony" ? "Maison Orvelle" : id == "character" ? "Bellombre" : id == "avant_garde" ? "Virelai" : "Maison Aveline";
        private static string Motto(string id) => id == "harmony" ? "Каждая деталь на своём месте" : id == "character" ? "Наряд должен оставлять впечатление" : id == "avant_garde" ? "Привычное — лишь отправная точка" : "Убедительность в каждой линии";
        private void LoadPreview()
        {
            if (loadedImagePath == imagePath) return;
            if (preview != null) UnityEngine.Object.Destroy(preview);
            preview = null; loadedImagePath = imagePath;
            if (string.IsNullOrEmpty(imagePath)) return;
            try
            {
                var bytes = System.IO.File.ReadAllBytes(imagePath);
                if (bytes.Length > 4 * 1024 * 1024) return;
                preview = new Texture2D(2, 2, TextureFormat.RGB24, false);
                // Unity 6 also exposes Span-based overloads. Resolve only the byte[]
                // API so the net472 compiler does not bind netstandard 2.1 signatures.
                var conversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", false);
                var load = conversion?.GetMethod("LoadImage", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);
                if (load == null || !(bool)load.Invoke(null, new object[] { preview, bytes, true }))
                { UnityEngine.Object.Destroy(preview); preview = null; }
            }
            catch { if (preview != null) UnityEngine.Object.Destroy(preview); preview = null; }
        }
        private static string Verdict(string value) => value == "exceptional" ? "Выдающаяся работа" : value == "strong" ? "Сильная работа" : value == "competent" ? "Уверенная работа" : value == "uneven" ? "Неравномерная работа" : value == "weak" ? "Слабая работа" : "Недостаточно данных";
        public void Begin() { open = true; result = null; subject = null; imagePath = null; contextTab = false; message = "Подготовка изображения и данных платья…"; scroll = Vector2.zero; showLimitations = false; }
        private static GUIStyle label, heading, title, score, muted, button, area;
        private static GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight, wordWrap = true, richText = false };
            style.normal.textColor = color;
            style.padding = new RectOffset(0, 0, 3, 3);
            return style;
        }
        private static void Text(string value) { GUILayout.Label(value, label); }
        private static void InitStyles()
        {
            if (label != null) return;
            label = Style(18, new Color(0.16f, 0.12f, 0.10f));
            heading = Style(21, new Color(0.40f, 0.18f, 0.15f), FontStyle.Bold);
            title = Style(26, new Color(0.19f, 0.12f, 0.10f), FontStyle.Bold);
            score = Style(28, new Color(0.19f, 0.12f, 0.10f), FontStyle.Bold);
            muted = Style(15, new Color(0.35f, 0.29f, 0.24f));
            button = new GUIStyle(GUI.skin.button) { fontSize = 17, padding = new RectOffset(12, 12, 8, 8) };
            area = new GUIStyle { padding = new RectOffset(24, 24, 18, 18) };
        }
        public void Draw(Action evaluate, bool busy)
        {
            if (!open) { if (inputShield != null) inputShield.SetActive(false); return; }
            InitStyles();
            LoadPreview();
            float width = Math.Min(1100, Math.Max(200, Screen.width - 32));
            float height = Math.Min(860, Math.Max(180, Screen.height - 72));
            Shield(width, height);
            var rect = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            Color oldColor = GUI.color, oldContent = GUI.contentColor, oldBackground = GUI.backgroundColor;
            try
            {
                // Do not inherit the game's transparent skin or tint for the result surface.
                GUI.color = new Color(0.91f, 0.85f, 0.74f, 1f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                // Paper margins and the central fold are drawn from mod-owned primitives.
                GUI.color = new Color(0.62f, 0.48f, 0.34f, 1f);
                GUI.DrawTexture(new Rect(rect.x + 8, rect.y + 8, 2, rect.height - 16), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.xMax - 10, rect.y + 8, 2, rect.height - 16), Texture2D.whiteTexture);
                if (width >= 850)
                    GUI.DrawTexture(new Rect(rect.x + 276, rect.y + 80, 1, Math.Max(0, rect.height - 104)), Texture2D.whiteTexture);
                GUI.color = Color.white; GUI.contentColor = Color.white; GUI.backgroundColor = Color.white;
                GUILayout.BeginArea(rect, area);
                try
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Салон · Жюри домов мод", title);
                    bool enabled = GUI.enabled;
                    GUI.enabled = enabled && !busy;
                    if (GUILayout.Button(busy ? "Рассматриваем…" : "Оценить платье", button, GUILayout.Width(180))) evaluate();
                    GUI.enabled = enabled;
                    if (GUILayout.Button("Закрыть", button, GUILayout.Width(110))) open = false;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(8);
                    bool spread = width >= 850;
                    GUILayout.BeginHorizontal();
                    if (spread)
                    {
                        GUILayout.BeginVertical(GUILayout.Width(240));
                        if (preview != null)
                        {
                            var photoRect = GUILayoutUtility.GetRect(220, 330, GUILayout.ExpandWidth(false));
                            GUI.DrawTexture(photoRect, preview, ScaleMode.ScaleToFit);
                        }
                        GUILayout.Label("Снимок оцениваемого платья", muted);
                        if (result != null)
                        {
                            GUILayout.Space(12);
                            GUILayout.Label(Number(result.summary.average), score);
                            GUILayout.Label(Verdict(result.summary.verdict), heading);
                        }
                        GUILayout.EndVertical();
                        GUILayout.Space(24);
                    }
                    GUILayout.BeginVertical();
                    GUILayout.BeginHorizontal();
                    GUI.enabled = contextTab;
                    if (GUILayout.Button("Дома мод", button)) { contextTab = false; scroll = Vector2.zero; }
                    GUI.enabled = !contextTab;
                    if (GUILayout.Button("Заказ", button)) { contextTab = true; scroll = Vector2.zero; }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    scroll = GUILayout.BeginScrollView(scroll);
                    try
                    {
                        if (!string.IsNullOrEmpty(subject)) GUILayout.Label(subject, muted);
                        GUILayout.Label("Снимок на момент запуска. Для другого или изменённого платья нужна новая оценка.", muted);
                        if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, muted);
                        if (result == null) return;
                        GUILayout.Space(12);
                        if (!spread)
                        {
                            GUILayout.Label("Среднее жюри: " + Number(result.summary.average), score);
                            GUILayout.Label(Verdict(result.summary.verdict), heading);
                        }
                        if (result.summary.divided == true) Text("Мнения жюри разделились.");
                        if (!contextTab) foreach (var vote in result.jury)
                        {
                            GUILayout.Space(20);
                            GUILayout.Label(House(vote.houseId) + " — " + Number(vote.score), heading);
                            GUILayout.Label(Motto(vote.houseId), muted);
                            foreach (string remark in vote.remarks) Text(remark);
                        }
                        if (contextTab)
                        {
                        GUILayout.Space(24);
                        GUILayout.Label("Контекст заказа", heading);
                        Context("Соответствие заказу", result.orderCompliance);
                        Context("Предпочтения заказчицы", result.customerCompatibility);
                        GUILayout.Label("Игровые требования не оценивались. Контекст не входит в среднее жюри.", muted);
                        }
                        if (result.limitations.Count > 0)
                        {
                            GUILayout.Space(16);
                            if (GUILayout.Button((showLimitations ? "Скрыть" : "Показать") + " ограничения оценки", button)) showLimitations = !showLimitations;
                            if (showLimitations) foreach (string limit in result.limitations) GUILayout.Label(limit, muted);
                        }
                    }
                    finally { GUILayout.EndScrollView(); GUILayout.EndVertical(); GUILayout.EndHorizontal(); }
                }
                finally { GUILayout.EndArea(); }
            }
            finally { GUI.color = oldColor; GUI.contentColor = oldContent; GUI.backgroundColor = oldBackground; }
        }
        private static void Context(string label, ContextJudgement value)
        {
            Text(label + ": " + Number(value.score));
            if (value.status != "unknown") foreach (string remark in value.remarks) Text(remark);
        }
    }
}
