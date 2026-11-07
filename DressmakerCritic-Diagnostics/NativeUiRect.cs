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
            Rect visual = CardBounds(target, space);
            if (visual.width <= 0 || visual.height <= 0) return;
            float heightScale = Mathf.Min(1, area.height / visual.height);
            if (target.rect.width > 1)
            {
                float width = target.rect.width * area.width / (visual.width * heightScale);
                target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                LayoutRebuilder.ForceRebuildLayoutImmediate(target);
                visual = CardBounds(target, space);
            }
            float scale = Mathf.Min(1, Mathf.Min(area.width / visual.width, area.height / visual.height));
            target.localScale *= scale;
            visual = CardBounds(target, space);
            Vector2 delta = area.center - visual.center;
            target.position += space.TransformVector(new Vector3(delta.x, delta.y, 0));
        }
        private static Rect CardBounds(RectTransform target, Transform space)
        {
            // Empty/stretch TMP rectangles are not the paper's visual bounds. Using
            // their union can translate the actual card behind the book spine.
            Image paper = null; float largest = 0;
            foreach (var image in target.GetComponentsInChildren<Image>(false).Take(128))
            {
                if (image.sprite == null || image.color.a <= 0) continue;
                Rect bounds = Bounds(image.rectTransform, space);
                float area = bounds.width * bounds.height;
                if (area > largest) { largest = area; paper = image; }
            }
            return paper == null ? VisualBounds(target, space) : Bounds(paper.rectTransform, space);
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
            // Resize layout rectangles rather than scaling glyphs along X.
            float excess = Mathf.Max(0, visual.width - width);
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1, target.rect.width - excess));
            LayoutRebuilder.ForceRebuildLayoutImmediate(target);
            visual = VisualBounds(target, viewport);
            float delta = viewport.rect.xMin + padding - visual.xMin;
            target.position += viewport.TransformVector(new Vector3(delta, 0, 0));
            foreach (var image in target.GetComponentsInChildren<Image>(false).Take(128))
            {
                var rect = image.rectTransform;
                Rect bounds = Bounds(rect, viewport);
                float overflow = bounds.xMax - (viewport.rect.xMax - padding);
                if (overflow <= 0 || bounds.width <= overflow) continue;
                float local = overflow * rect.rect.width / bounds.width;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rect.rect.width - local);
                rect.anchoredPosition -= new Vector2(local * rect.pivot.x, 0);
            }
        }
    }
}
