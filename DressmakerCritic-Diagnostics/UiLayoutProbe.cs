using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class UiLayoutReport
    {
        public string schemaVersion = "1.0.0", status, placement;
        public int screenWidth, screenHeight, nodeLimit = 192, depthLimit = 12;
        public bool truncated;
        public List<UiLayoutBinding> bindings = new List<UiLayoutBinding>();
        public List<UiLayoutNode> nodes = new List<UiLayoutNode>();
    }
    [Serializable] public sealed class UiLayoutBinding
    {
        public string field, status;
        public int? transformId;
    }
    [Serializable] public sealed class UiLayoutNode
    {
        public int id, parentId, childCount;
        public string name;
        public bool active, activeSelf;
        public List<string> components = new List<string>();
        // Pairs: x/y; rect: x/y/width/height; corners: bottom-left, top-left, top-right, bottom-right.
        public List<double> rect, anchorMin, anchorMax, pivot, sizeDelta, anchoredPosition, localScale, screenCorners;
        public double? canvasScale, fontSize, preferredHeight, minHeight;
        public bool? raycastTarget, mask, interactable;
    }
    internal static class UiLayoutProbe
    {
        private static readonly string[] fields = { "leftPage", "oldDressViewHolder", "oldQuestTitle", "oldQuestText",
            "oldQuestGiver", "oldQuestScoreDisplay", "comissionScoreDisplay" };
        private static List<double> XY(Vector2 v) => new List<double> { v.x, v.y };
        private static Transform TransformOf(object value) => value is GameObject go ? go.transform : (value as Component)?.transform;
        private static string Short(string value) => value == null ? null : value.Substring(0, Math.Min(128, value.Length));

        // On-demand only. Reads explicit game fields and known Unity UI APIs; no arbitrary
        // getters, text contents, game methods, scene-wide traversal or reflection graph.
        public static UiLayoutReport Capture(Component book, string placement, params RectTransform[] resolved)
        {
            var report = new UiLayoutReport { screenWidth = Screen.width, screenHeight = Screen.height,
                status = book == null ? "history-unavailable" : "captured", placement = placement };
            if (book == null) return report;
            var pending = new Queue<Tuple<Transform, int>>();
            Action<string, Transform> bind = (name, t) =>
            {
                report.bindings.Add(new UiLayoutBinding { field = name, status = t == null ? "unavailable" : "resolved", transformId = t == null ? (int?)null : t.GetInstanceID() });
                if (t != null) pending.Enqueue(Tuple.Create(t, 0));
            };
            foreach (string field in fields) bind(field, TransformOf(FieldSnapshot.ResolveFields(book, field)));
            var score = FieldSnapshot.ResolveFields(book, "comissionScoreDisplay");
            bind("comissionScoreDisplay.inventoryRect", TransformOf(FieldSnapshot.ResolveFields(score, "inventoryRect")));
            string[] resolvedNames = { "jury", "resolvedBrief", "resolvedScore", "resolvedPhoto" };
            for (int i = 0; i < resolved.Length && i < resolvedNames.Length; i++) bind(resolvedNames[i], resolved[i]);
            var visited = new HashSet<int>();
            while (pending.Count > 0 && report.nodes.Count < report.nodeLimit)
            {
                var next = pending.Dequeue(); Transform t = next.Item1;
                if (t == null || !visited.Add(t.GetInstanceID())) continue;
                var node = new UiLayoutNode { id = t.GetInstanceID(), parentId = t.parent == null ? 0 : t.parent.GetInstanceID(),
                    name = Short(t.name), active = t.gameObject.activeInHierarchy, activeSelf = t.gameObject.activeSelf, childCount = t.childCount,
                    localScale = new List<double> { t.localScale.x, t.localScale.y, t.localScale.z } };
                var components = t.GetComponents<Component>();
                for (int i = 0; i < components.Length && i < 16; i++)
                    if (components[i] != null) node.components.Add(Short(components[i].GetType().FullName));
                var canvas = t.GetComponentInParent<Canvas>();
                if (canvas != null) node.canvasScale = canvas.scaleFactor;
                if (t is RectTransform rt)
                {
                    node.rect = new List<double> { rt.rect.x, rt.rect.y, rt.rect.width, rt.rect.height };
                    node.anchorMin = XY(rt.anchorMin); node.anchorMax = XY(rt.anchorMax); node.pivot = XY(rt.pivot);
                    node.sizeDelta = XY(rt.sizeDelta); node.anchoredPosition = XY(rt.anchoredPosition);
                    var corners = new Vector3[4]; rt.GetWorldCorners(corners); node.screenCorners = new List<double>();
                    Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    foreach (var corner in corners) node.screenCorners.AddRange(XY(RectTransformUtility.WorldToScreenPoint(camera, corner)));
                }
                var text = t.GetComponent<TextMeshProUGUI>(); if (text != null) node.fontSize = text.fontSize;
                var graphic = t.GetComponent<Graphic>(); if (graphic != null) node.raycastTarget = graphic.raycastTarget;
                node.mask = t.GetComponent<RectMask2D>() != null || t.GetComponent<Mask>() != null;
                var selectable = t.GetComponent<Selectable>(); if (selectable != null) node.interactable = selectable.interactable;
                var layout = t.GetComponent<LayoutElement>();
                if (layout != null) { node.preferredHeight = layout.preferredHeight; node.minHeight = layout.minHeight; }
                report.nodes.Add(node);
                if (next.Item2 >= report.depthLimit) { if (t.childCount > 0) report.truncated = true; continue; }
                int children = Math.Min(t.childCount, report.nodeLimit - pending.Count);
                if (children < t.childCount) report.truncated = true;
                for (int i = 0; i < children; i++) pending.Enqueue(Tuple.Create(t.GetChild(i), next.Item2 + 1));
            }
            report.truncated |= pending.Count > 0;
            return report;
        }
    }
}
