using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DressmakerCritic.Diagnostics
{
    // Own border and clipping allowance for the WIP tag list. No rows, values or
    // native layout dimensions are changed; no shared history inventory is resized.
    internal sealed class TagListPresentation : IDisposable
    {
        private RectTransform target;
        private RectMask2D clipping;
        private Mask stencil;
        private bool stencilEnabled, clippingEnabled, addedClipping;
        private Vector4 originalPadding;
        private GameObject frame;
        public RectTransform Frame => frame == null ? null : frame.GetComponent<RectTransform>();
        public void Tick(Component book)
        {
            var holder = FieldSnapshot.ResolveFields(book, "draftViewHolder") as GameObject;
            var mask = FieldSnapshot.ResolveFields(book, "tagMask") as RectTransform;
            if (holder == null || !holder.activeInHierarchy || mask == null || !mask.gameObject.activeInHierarchy)
            { Dispose(); return; }
            if (target == mask) { PlaceFrame(); return; }
            Dispose(); target = mask;
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
        public void Dispose()
        {
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
