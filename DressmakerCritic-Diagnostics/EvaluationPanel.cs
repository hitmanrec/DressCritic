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
        public string subject, message;
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
        public void Dispose() { if (inputShield != null) UnityEngine.Object.Destroy(inputShield); }
        private static string Number(double? value) => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) + " / 10" : "Недостаточно данных";
        private static string House(string id) => id == "harmony" ? "Дом Гармонии" : id == "character" ? "Дом Характера" : id == "avant_garde" ? "Дом Авангарда" : "Дом Ателье";
        private static string Verdict(string value) => value == "exceptional" ? "Выдающаяся работа" : value == "strong" ? "Сильная работа" : value == "competent" ? "Уверенная работа" : value == "uneven" ? "Неравномерная работа" : value == "weak" ? "Слабая работа" : "Недостаточно данных";
        public void Begin() { open = true; result = null; subject = null; message = "Подготовка изображения и данных платья…"; scroll = Vector2.zero; showLimitations = false; }
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
            label = Style(18, new Color(0.94f, 0.95f, 0.97f));
            heading = Style(21, new Color(0.92f, 0.77f, 0.49f), FontStyle.Bold);
            title = Style(26, Color.white, FontStyle.Bold);
            score = Style(28, Color.white, FontStyle.Bold);
            muted = Style(15, new Color(0.74f, 0.79f, 0.85f));
            button = new GUIStyle(GUI.skin.button) { fontSize = 17, padding = new RectOffset(12, 12, 8, 8) };
            area = new GUIStyle { padding = new RectOffset(24, 24, 18, 18) };
        }
        public void Draw()
        {
            if (!open) { if (inputShield != null) inputShield.SetActive(false); return; }
            InitStyles();
            float width = Math.Min(780, Math.Max(200, Screen.width - 32));
            float height = Math.Min(860, Math.Max(180, Screen.height - 72));
            Shield(width, height);
            var rect = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            Color oldColor = GUI.color, oldContent = GUI.contentColor, oldBackground = GUI.backgroundColor;
            try
            {
                // Do not inherit the game's transparent skin or tint for the result surface.
                GUI.color = new Color(0.055f, 0.075f, 0.105f, 1f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white; GUI.contentColor = Color.white; GUI.backgroundColor = Color.white;
                GUILayout.BeginArea(rect, area);
                try
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Жюри домов мод", title);
                    if (GUILayout.Button("Закрыть", button, GUILayout.Width(110))) open = false;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(8);
                    scroll = GUILayout.BeginScrollView(scroll);
                    try
                    {
                        if (!string.IsNullOrEmpty(subject)) GUILayout.Label(subject, muted);
                        GUILayout.Label("Снимок на момент запуска. Для другого или изменённого платья нужна новая оценка.", muted);
                        if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, muted);
                        if (result == null) return;
                        GUILayout.Space(12);
                        GUILayout.Label("Среднее жюри: " + Number(result.summary.average), score);
                        GUILayout.Label(Verdict(result.summary.verdict), heading);
                        if (result.summary.divided == true) Text("Мнения жюри разделились.");
                        foreach (var vote in result.jury)
                        {
                            GUILayout.Space(20);
                            GUILayout.Label(House(vote.houseId) + " — " + Number(vote.score), heading);
                            foreach (string remark in vote.remarks) Text(remark);
                        }
                        GUILayout.Space(24);
                        GUILayout.Label("Контекст заказа", heading);
                        Context("Соответствие заказу", result.orderCompliance);
                        Context("Предпочтения заказчицы", result.customerCompatibility);
                        GUILayout.Label("Игровые требования не оценивались. Контекст не входит в среднее жюри.", muted);
                        if (result.limitations.Count > 0)
                        {
                            GUILayout.Space(16);
                            if (GUILayout.Button((showLimitations ? "Скрыть" : "Показать") + " ограничения оценки", button)) showLimitations = !showLimitations;
                            if (showLimitations) foreach (string limit in result.limitations) GUILayout.Label(limit, muted);
                        }
                    }
                    finally { GUILayout.EndScrollView(); }
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
