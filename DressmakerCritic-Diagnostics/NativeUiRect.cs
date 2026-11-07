using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace DressmakerCritic.Diagnostics
{
    // Restore all layout inputs, including anchors, after leaving history.
    internal sealed class NativeUiRect
    {
        private readonly RectTransform target;
        private readonly Vector2 min, max, pivot, size, position;
        private readonly Vector3 scale;
        private readonly Quaternion rotation;
        public NativeUiRect(RectTransform target)
        {
            this.target = target;
            min = target.anchorMin; max = target.anchorMax; pivot = target.pivot;
            size = target.sizeDelta; position = target.anchoredPosition;
            scale = target.localScale; rotation = target.localRotation;
        }
        public void Restore()
        {
            if (target == null) return;
            target.anchorMin = min; target.anchorMax = max; target.pivot = pivot;
            target.sizeDelta = size; target.anchoredPosition = position;
            target.localScale = scale; target.localRotation = rotation;
        }
        public static Rect Bounds(RectTransform target, Transform space)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            Vector2 min = space.InverseTransformPoint(corners[0]), max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 p = space.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        public static void Place(RectTransform target, Transform space, Rect area)
        {
            var parent = target.parent as RectTransform;
            if (parent == null) return;
            Vector2 a = parent.InverseTransformPoint(space.TransformPoint(new Vector3(area.xMin, area.yMin)));
            Vector2 b = parent.InverseTransformPoint(space.TransformPoint(new Vector3(area.xMax, area.yMax)));
            target.anchorMin = target.anchorMax = parent.pivot;
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = (a + b) * 0.5f;
            target.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
            target.localScale = Vector3.one; target.localRotation = Quaternion.identity;
        }
        public static void FitVisuals(RectTransform target, Transform space, Rect area)
        {
            // A grouping RectTransform can be much smaller than its fixed-size children.
            // Fit actual graphics, not that bookkeeping rectangle; retain native font aspect.
            Rect visual = VisualBounds(target, space);
            if (visual.width <= 0 || visual.height <= 0) return;
            float heightScale = Mathf.Min(1, area.height / visual.height);
            if (target.rect.width > 1)
            {
                float width = target.rect.width * area.width / (visual.width * heightScale);
                target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                LayoutRebuilder.ForceRebuildLayoutImmediate(target);
                visual = VisualBounds(target, space);
            }
            float scale = Mathf.Min(1, Mathf.Min(area.width / visual.width, area.height / visual.height));
            target.localScale *= scale;
            visual = VisualBounds(target, space);
            Vector2 delta = area.center - visual.center;
            target.position += space.TransformVector(new Vector3(delta.x, delta.y, 0));
        }
        private static Rect VisualBounds(RectTransform target, Transform space)
        {
            bool found = false; Rect result = new Rect();
            foreach (var graphic in target.GetComponentsInChildren<Graphic>(false).Take(128))
            {
                Rect bounds = Bounds(graphic.rectTransform, space);
                if (bounds.width <= 0 || bounds.height <= 0) continue;
                result = found ? Rect.MinMaxRect(Mathf.Min(result.xMin, bounds.xMin), Mathf.Min(result.yMin, bounds.yMin),
                    Mathf.Max(result.xMax, bounds.xMax), Mathf.Max(result.yMax, bounds.yMax)) : bounds;
                found = true;
            }
            return found ? result : Bounds(target, space);
        }
        public static void InsetHorizontal(RectTransform target, RectTransform viewport, float padding)
        {
            Rect visual = VisualBounds(target, viewport);
            float width = viewport.rect.width - padding * 2;
            if (visual.width <= 0 || width <= 0) return;
            var scale = target.localScale;
            scale.x *= Mathf.Min(1, width / visual.width);
            target.localScale = scale;
            visual = VisualBounds(target, viewport);
            float delta = viewport.rect.center.x - visual.center.x;
            target.position += viewport.TransformVector(new Vector3(delta, 0, 0));
        }
    }
}
