using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DressmakerCritic.Diagnostics
{
    public sealed partial class Plugin
    {
        private bool cardVisible;
        private bool historyVisible;
        private bool ordersVisible;
        private List<AcceptedOrderChoice> acceptedOrders = new List<AcceptedOrderChoice>();
        private AcceptedOrderChoice selectedOrder;
        private object selectedOrderProgress;
        private string reportStatus = "";
        private List<SavedDressChoice> savedChoices = new List<SavedDressChoice>();
        private SavedDressChoice savedChoice;
        private object savedProgress;
        private string historyStatus;
        private GarmentSnapshot inspectedGarment;
        private ProgressSnapshot inspectedProgress;
        private string cardText = "Нажмите «Обновить» для проверки выбранного платья.";
        private Vector2 cardScroll;
        private GUIStyle cardLabelStyle;
        private float nextCardRefresh;

        private void RefreshGarmentCard()
        {
            cardVisible = true;
            if (Time.realtimeSinceStartup < nextCardRefresh) return;
            nextCardRefresh = Time.realtimeSinceStartup + 2f;
            inspectedGarment = null; inspectedProgress = null;
            try
            {
                string time = DateTime.UtcNow.ToString("O");
                Type managerType = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "Assembly-CSharp")
                    .Select(a => a.GetType("GameManager", false)).FirstOrDefault(t => t != null);
                if (managerType == null || !typeof(Component).IsAssignableFrom(managerType))
                { cardText = "Данные игры недоступны. Откройте редактор и обновите карточку."; return; }
                var managers = UnityEngine.Object.FindObjectsOfType(managerType, true);
                if (managers.Length != 1)
                { cardText = "Не удалось однозначно определить состояние игры. Обновите карточку после открытия редактора."; return; }
                var roots = new List<ValueNode>();
                var graph = new FieldSnapshot(4096);
                object state;
                string source = "GameManager.activeDress.state";
                if (savedChoice != null)
                {
                    string accessStatus;
                    object progressInstance = ReadProgressForCard(out accessStatus);
                    string selectionStatus;
                    state = SavedDressAccess.Resolve(progressInstance, savedProgress, savedChoice, out selectionStatus);
                    if (selectionStatus != "available") { inspectedGarment = null; inspectedProgress = null; cardText = "Выбор платья устарел или данные недоступны. Откройте список сохранённых заново."; return; }
                    source = "PlayerProgress.completedQuests[" + savedChoice.index + "].dress";
                }
                else state = FieldSnapshot.ResolveFields(managers[0], "activeDress", "state");
                object[] panels = FieldSnapshot.ListItems(FieldSnapshot.ResolveFields(state, "panelStates"));
                for (int i = 0; i < panels.Length && !graph.Exhausted; i++)
                {
                    var fabric = graph.Read(FieldSnapshot.ResolveFields(panels[i], "fabric"));
                    fabric.member = "GameManager.activeDress.state.panelStates[" + i + "].fabric"; roots.Add(fabric);
                }
                var current = graph.Read(state); current.member = source; roots.Add(current);
                var screen = graph.Read(FieldSnapshot.ResolveFields(managers[0], "<CurrentScene>k__BackingField"));
                screen.member = "GameManager.<CurrentScene>k__BackingField"; roots.Add(screen);
                var garment = new GarmentSnapshotBuilder(roots).Build(time, SceneManager.GetActiveScene().name, source);
                // Empty prettyName is common in real assets. Unity's native name is
                // a display fallback only, never a catalog identity or game getter.
                foreach (var selected in garment.components)
                {
                    var asset = FieldSnapshot.ResolveFields(state, selected.slot + "Definition") as UnityEngine.Object;
                    if (asset != null && selected.definition != null && string.IsNullOrEmpty(selected.definition.prettyName))
                        selected.definition.prettyName = asset.name;
                }
                string orderAccessStatus;
                object orderProgress = ReadProgressForCard(out orderAccessStatus);
                if (selectedOrder != null && !AcceptedOrderAccess.IsCurrent(orderProgress, selectedOrderProgress, selectedOrder))
                { selectedOrder = null; selectedOrderProgress = null; reportStatus = "Выбор заказа устарел. Выберите заказ заново."; }
                int? orderIndex = selectedOrder == null ? (int?)null : selectedOrder.index;
                var progress = ProgressSnapshotBuilder.Build(CaptureContext(time, garment.unityScene, orderIndex), orderIndex);
                inspectedGarment = garment; inspectedProgress = selectedOrder == null ? null : progress;
                cardText = "Источник: " + (savedChoice == null ? "Текущее платье" : savedChoice.label + " (история)") + "\n" + GarmentCardText.Build(garment, progress, selectedOrder != null);
                historyVisible = false; ordersVisible = false;
                cardScroll = Vector2.zero;
            }
            catch (Exception e)
            { inspectedGarment = null; inspectedProgress = null; cardText = "Ошибка проверки. См. журнал BepInEx, затем обновите карточку."; Logger.LogError(e); }
        }

        private static object ReadProgressForCard(out string status)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "Assembly-CSharp")
                .Select(a => a.GetType("PlayerProgress", false)).FirstOrDefault(t => t != null);
            return ProgressAccess.TryRead(type, out status);
        }
        private void OpenSavedDresses()
        {
            string status;
            savedProgress = ReadProgressForCard(out status);
            savedChoice = null; selectedOrder = null; selectedOrderProgress = null;
            inspectedGarment = null; inspectedProgress = null;
            savedChoices = SavedDressAccess.List(savedProgress, out historyStatus);
            historyStatus = savedProgress == null ? "История недоступна: загрузите сохранение." : "Платья из завершённых заказов: " + savedChoices.Count + ". Проверено не более 512 записей.";
            historyVisible = true; ordersVisible = false; cardScroll = Vector2.zero;
        }

        private void OpenOrderChoices()
        {
            string status;
            selectedOrderProgress = ReadProgressForCard(out status);
            acceptedOrders = AcceptedOrderAccess.List(selectedOrderProgress);
            ordersVisible = true; historyVisible = false; cardScroll = Vector2.zero;
        }
        private void SaveInspectionReport()
        {
            if (inspectedGarment == null) { reportStatus = "Сначала проверьте платье."; return; }
            try
            {
                string directory = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "DressmakerCritic", "reports");
                System.IO.Directory.CreateDirectory(directory);
                string name = "inspection-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt";
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory, name), cardText, System.Text.Encoding.UTF8);
                reportStatus = "Отчёт сохранён: DressmakerCritic/reports/" + name;
            }
            catch (Exception e) { reportStatus = "Не удалось сохранить отчёт — см. журнал BepInEx."; Logger.LogError(e); }
        }

        private void DrawGarmentCard()
        {
            if (!cardVisible) return;
            float width = Math.Min(680f, Math.Max(1f, Screen.width - 24f));
            float height = Math.Min(720f, Math.Max(1f, Screen.height - 24f));
            float x = Math.Max(0f, (Screen.width - width) / 2f), y = Math.Max(0f, (Screen.height - height) / 2f);
            var area = new Rect(x, y, width, height);
            GUI.Box(area, "Dressmaker Critic — карточка платья");
            if (GUI.Button(new Rect(x + width - 76f, y + 24f, 64f, 28f), "Закрыть")) cardVisible = false;
            bool enabled = GUI.enabled;
            try
            {
                GUI.enabled = enabled && Time.realtimeSinceStartup >= nextCardRefresh;
                if (GUI.Button(new Rect(x + 12f, y + 24f, 100f, 28f), "Обновить")) RefreshGarmentCard();
            }
            finally { GUI.enabled = enabled; }
            if (GUI.Button(new Rect(x + 124f, y + 24f, 100f, 28f), "Текущее"))
            { savedChoice = null; selectedOrder = null; selectedOrderProgress = null; historyVisible = false; nextCardRefresh = 0; RefreshGarmentCard(); }
            if (GUI.Button(new Rect(x + 236f, y + 24f, 132f, 28f), "Сохранённые")) OpenSavedDresses();
            if (GUI.Button(new Rect(x + 12f, y + 60f, 120f, 28f), "Заказ")) OpenOrderChoices();
            if (GUI.Button(new Rect(x + 144f, y + 60f, 180f, 28f), "Сохранить отчёт")) SaveInspectionReport();
            // All game reads are in RefreshGarmentCard; this path renders cached text only.
            GUILayout.BeginArea(new Rect(x + 12f, y + 98f, Math.Max(1f, width - 24f), Math.Max(1f, height - 110f)));
            try
            {
                cardScroll = GUILayout.BeginScrollView(cardScroll);
                try
                {
                    if (cardLabelStyle == null) cardLabelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
                    if (!string.IsNullOrEmpty(reportStatus)) GUILayout.Label(reportStatus, cardLabelStyle);
                    if (ordersVisible)
                    {
                        GUILayout.Label("Выберите принятый заказ. Прочитано не более 512 записей.", cardLabelStyle);
                        if (GUILayout.Button("Без заказа")) { selectedOrder = null; selectedOrderProgress = null; nextCardRefresh = 0; RefreshGarmentCard(); }
                        if (acceptedOrders.Count == 0) GUILayout.Label("Нет доступных принятых заказов или сохранение недоступно.", cardLabelStyle);
                        foreach (var order in acceptedOrders)
                            if (GUILayout.Button(order.label)) { selectedOrder = order; nextCardRefresh = 0; RefreshGarmentCard(); break; }
                    }
                    else if (historyVisible)
                    {
                        GUILayout.Label(historyStatus, cardLabelStyle);
                        GUILayout.Label("Выберите платье. Этот список независим от выбора миниатюры в игре.", cardLabelStyle);
                        foreach (var choice in savedChoices)
                            if (GUILayout.Button(choice.label)) { savedChoice = choice; selectedOrder = null; selectedOrderProgress = null; nextCardRefresh = 0; RefreshGarmentCard(); break; }
                    }
                    else GUILayout.Label(cardText, cardLabelStyle);
                }
                finally { GUILayout.EndScrollView(); }
            }
            finally { GUILayout.EndArea(); }
        }
    }
}
