using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressmakerCritic.Diagnostics
{
    // Native border plus reversible horizontal row insets for the WIP tag list.
    // Tag values, vertical layout and the shared history inventory are untouched.
    internal sealed class TagListPresentation : IDisposable
    {
        private RectTransform target;
        private RectMask2D clipping;
        private Mask stencil;
        private bool stencilEnabled, clippingEnabled, addedClipping;
        private Vector4 originalPadding;
        private GameObject frame;
        private Transform content;
        private int rowCount;
        private readonly List<NativeUiRect> rows = new List<NativeUiRect>();
        public RectTransform Frame => frame == null ? null : frame.GetComponent<RectTransform>();
        public void Tick(Component book)
        {
            var holder = FieldSnapshot.ResolveFields(book, "draftViewHolder") as GameObject;
            var mask = FieldSnapshot.ResolveFields(book, "tagMask") as RectTransform;
            if (holder == null || !holder.activeInHierarchy || mask == null || !mask.gameObject.activeInHierarchy)
            { Dispose(); return; }
            if (target == mask)
            {
                if (content != null && content.childCount != rowCount) InsetRows();
                PlaceFrame(); return;
            }
            Dispose(); target = mask;
            content = FieldSnapshot.ResolveFields(book, "tagBarParent") as Transform;
            InsetRows();
            clipping = mask.GetComponent<RectMask2D>();
            addedClipping = clipping == null;
            if (addedClipping) clipping = mask.gameObject.AddComponent<RectMask2D>();
            clippingEnabled = clipping.enabled;
            clipping.enabled = true;
            originalPadding = clipping.padding;
            // Negative padding expands clipping, leaving row and slider geometry intact.
            clipping.padding = originalPadding + new Vector4(-16, -12, -16, -12);
            stencil = mask.GetComponent<Mask>();
            if (stencil != null) { stencilEnabled = stencil.enabled; stencil.enabled = false; }

            // Reuse a sliced native border, preferring the history card's decoration.
            var history = FieldSnapshot.ResolveFields(book, "oldQuestScoreDisplay") as GameObject;
            Image source = history == null ? null : history.GetComponentsInChildren<Image>(true).Take(32)
                .FirstOrDefault(i => i.sprite != null && i.sprite.border.sqrMagnitude > 0);
            if (source == null)
                source = book.GetComponentsInChildren<Button>(true).Take(128)
                    .SelectMany(b => b.GetComponentsInChildren<Image>(true).Take(8))
                    .FirstOrDefault(i => i.sprite != null && i.sprite.border.sqrMagnitude > 0);
            if (source == null) return; // Preserve clipping repair; do not manufacture a game frame.
            frame = new GameObject("DressCriticTagListFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frame.transform.SetParent(mask.parent, false);
            frame.transform.SetSiblingIndex(mask.GetSiblingIndex());
            var image = frame.GetComponent<Image>();
            EvaluationPanel.CopyImage(source, image);
            image.type = Image.Type.Sliced; image.raycastTarget = false;
            PlaceFrame();
        }
        private void PlaceFrame()
        {
            if (target == null || frame == null) return;
            Rect area = NativeUiRect.Bounds(target, target.parent);
            area.xMin -= 24; area.xMax += 24; area.yMin -= 20; area.yMax += 20;
            NativeUiRect.Place(frame.GetComponent<RectTransform>(), target.parent, area);
        }
        private void InsetRows()
        {
            foreach (var row in rows) row.Restore();
            rows.Clear();
            if (content == null || target == null) return;
            if (content is RectTransform rect) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            rowCount = content.childCount;
            for (int i = 0; i < Math.Min(rowCount, 256); i++)
            {
                var row = content.GetChild(i) as RectTransform;
                if (row == null || !row.gameObject.activeInHierarchy) continue;
                rows.Add(new NativeUiRect(row));
                foreach (var child in row.GetComponentsInChildren<RectTransform>(true).Take(128))
                    if (child != row) rows.Add(new NativeUiRect(child));
                // Modest 12-unit margins; glyph scale and vertical scroll are preserved.
                NativeUiRect.InsetHorizontal(row, target, 12);
            }
        }
        public void Dispose()
        {
            foreach (var row in rows) row.Restore();
            rows.Clear(); content = null; rowCount = 0;
            if (clipping != null)
            {
                clipping.padding = originalPadding;
                clipping.enabled = clippingEnabled;
                if (addedClipping) { clipping.enabled = false; UnityEngine.Object.Destroy(clipping); }
            }
            if (stencil != null) stencil.enabled = stencilEnabled;
            if (frame != null) { frame.SetActive(false); UnityEngine.Object.Destroy(frame); }
            target = null; clipping = null; stencil = null; frame = null; addedClipping = false;
        }
    }
}
