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
        private string cardText = "Press Refresh to inspect the open garment.";
        private Vector2 cardScroll;
        private GUIStyle cardLabelStyle;
        private float nextCardRefresh;

        private void RefreshGarmentCard()
        {
            cardVisible = true;
            if (Time.realtimeSinceStartup < nextCardRefresh) return;
            nextCardRefresh = Time.realtimeSinceStartup + 2f;
            try
            {
                string time = DateTime.UtcNow.ToString("O");
                Type managerType = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "Assembly-CSharp")
                    .Select(a => a.GetType("GameManager", false)).FirstOrDefault(t => t != null);
                if (managerType == null || !typeof(Component).IsAssignableFrom(managerType))
                { cardText = "Game state unavailable. Open the editor and refresh."; return; }
                var managers = UnityEngine.Object.FindObjectsOfType(managerType, true);
                if (managers.Length != 1)
                { cardText = "Cannot identify a unique game state. No garment was selected. Refresh after opening the editor."; return; }
                var roots = new List<ValueNode>();
                var graph = new FieldSnapshot(4096);
                object state = FieldSnapshot.ResolveFields(managers[0], "activeDress", "state");
                object[] panels = FieldSnapshot.ListItems(FieldSnapshot.ResolveFields(state, "panelStates"));
                for (int i = 0; i < panels.Length && !graph.Exhausted; i++)
                {
                    var fabric = graph.Read(FieldSnapshot.ResolveFields(panels[i], "fabric"));
                    fabric.member = "GameManager.activeDress.state.panelStates[" + i + "].fabric"; roots.Add(fabric);
                }
                var current = graph.Read(state); current.member = "GameManager.activeDress.state"; roots.Add(current);
                var screen = graph.Read(FieldSnapshot.ResolveFields(managers[0], "<CurrentScene>k__BackingField"));
                screen.member = "GameManager.<CurrentScene>k__BackingField"; roots.Add(screen);
                var garment = new GarmentSnapshotBuilder(roots).Build(time, SceneManager.GetActiveScene().name);
                // Empty prettyName is common in real assets. Unity's native name is
                // a display fallback only, never a catalog identity or game getter.
                foreach (var selected in garment.components)
                {
                    var asset = FieldSnapshot.ResolveFields(state, selected.slot + "Definition") as UnityEngine.Object;
                    if (asset != null && selected.definition != null && string.IsNullOrEmpty(selected.definition.prettyName))
                        selected.definition.prettyName = asset.name;
                }
                var progress = ProgressSnapshotBuilder.Build(CaptureContext(time, garment.unityScene));
                cardText = GarmentCardText.Build(garment, progress);
                cardScroll = Vector2.zero;
            }
            catch (Exception e)
            { cardText = "Inspection failed. See BepInEx log, then refresh."; Logger.LogError(e); }
        }

        private void DrawGarmentCard()
        {
            if (!cardVisible) return;
            float width = Math.Min(680f, Math.Max(1f, Screen.width - 24f));
            float height = Math.Min(720f, Math.Max(1f, Screen.height - 24f));
            float x = Math.Max(0f, (Screen.width - width) / 2f), y = Math.Max(0f, (Screen.height - height) / 2f);
            var area = new Rect(x, y, width, height);
            GUI.Box(area, "Dressmaker Critic - garment facts");
            if (GUI.Button(new Rect(x + width - 76f, y + 24f, 64f, 28f), "Close")) cardVisible = false;
            bool enabled = GUI.enabled;
            try
            {
                GUI.enabled = enabled && Time.realtimeSinceStartup >= nextCardRefresh;
                if (GUI.Button(new Rect(x + 12f, y + 24f, 100f, 28f), "Refresh")) RefreshGarmentCard();
            }
            finally { GUI.enabled = enabled; }
            // All game reads are in RefreshGarmentCard; this path renders cached text only.
            GUILayout.BeginArea(new Rect(x + 12f, y + 60f, Math.Max(1f, width - 24f), Math.Max(1f, height - 72f)));
            try
            {
                cardScroll = GUILayout.BeginScrollView(cardScroll);
                try
                {
                    if (cardLabelStyle == null) cardLabelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
                    GUILayout.Label(cardText, cardLabelStyle);
                }
                finally { GUILayout.EndScrollView(); }
            }
            finally { GUILayout.EndArea(); }
        }
    }
}
