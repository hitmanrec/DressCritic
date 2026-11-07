using UnityEngine;

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
    }
}
