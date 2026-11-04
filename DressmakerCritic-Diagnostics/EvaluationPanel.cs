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
        public void Begin() { open = true; result = null; subject = null; message = "Подготовка изображения и данных платья…"; scroll = Vector2.zero; }
        private static GUIStyle label;
        private static void Text(string value) { GUILayout.Label(value, label); }
        public void Draw()
        {
            if (!open) { if (inputShield != null) inputShield.SetActive(false); return; }
            if (label == null) label = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
            float width = Math.Min(660, Math.Max(200, Screen.width - 32));
            float height = Math.Min(740, Math.Max(180, Screen.height - 72));
            Shield(width, height);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height), GUI.skin.box);
            try
            {
                GUILayout.BeginHorizontal();
                Text("Жюри домов мод");
                if (GUILayout.Button("Закрыть", GUILayout.Width(90))) open = false;
                GUILayout.EndHorizontal();
                scroll = GUILayout.BeginScrollView(scroll);
                try
                {
                    if (!string.IsNullOrEmpty(subject)) Text(subject);
                    Text("Оценка относится к снимку на момент запуска. После изменения или выбора другого платья запустите новую оценку.");
                    if (!string.IsNullOrEmpty(message)) Text(message);
                    if (result == null) return;
                    GUILayout.Space(8);
                    Text("Среднее жюри: " + Number(result.summary.average));
                    Text(Verdict(result.summary.verdict));
                    if (result.summary.divided == true) Text("Мнения жюри разделились.");
                    foreach (var vote in result.jury)
                    {
                        GUILayout.Space(10);
                        Text(House(vote.houseId) + " — " + Number(vote.score));
                        foreach (string remark in vote.remarks) Text(remark);
                    }
                    GUILayout.Space(14);
                    Context("Соответствие заказу", result.orderCompliance);
                    Context("Предпочтения заказчицы", result.customerCompatibility);
                    Text("Игровые требования: не оценивались. В среднее жюри не входят.");
                    if (result.limitations.Count > 0)
                    {
                        GUILayout.Space(10); Text("Ограничения оценки");
                        foreach (string limit in result.limitations) Text(limit);
                    }
                }
                finally { GUILayout.EndScrollView(); }
            }
            finally { GUILayout.EndArea(); }
        }
        private static void Context(string label, ContextJudgement value)
        {
            Text(label + ": " + Number(value.score));
            if (value.status != "unknown") foreach (string remark in value.remarks) Text(remark);
        }
    }
}
