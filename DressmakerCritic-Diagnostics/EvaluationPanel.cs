using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressmakerCritic.Diagnostics
{
    // History-only inline jury on the left notebook page. No tab, no paper overlay:
    // compresses the commission score card, shrinks Photo Mode, and fills the freed space.
    internal sealed class EvaluationPanel
    {
        public JuryAssessment result;
        private string message;
        private string originLabel;
        private bool contextTab;
        private bool busy;
        private bool disposed;
        private Action evaluate;
        private Component boundBook;
        private Component cachedBook;
        private float nextBookSearch;
        private readonly EvaluationSelection selection = new EvaluationSelection();
        private readonly TagListPresentation tagList = new TagListPresentation();
        private GameObject root;
        private RectTransform rootRt, scrollContent, housesContent, orderContent;
        private TextMeshProUGUI evaluateLabel, housesTabLabel, orderTabLabel, meanLabel;
        private Image evaluateImage, housesTabImage, orderTabImage;
        private ScrollRect scroll;
        private readonly List<GameObject> rows = new List<GameObject>();
        private TMP_FontAsset font;
        private Material fontMaterial;
        private Sprite buttonBorder;
        private JuryAssessment paintedResult;
        private string paintedMessage;
        private Rect placedRect;
        private bool hasPlacedRect;
        private string layoutStatus = "not-bound";
        private UiLayoutReport originalLayout;
        private bool paintedContext, paintedBusy;
        private bool layoutApplied;
        private RectTransform pageRt; // left notebook page when available
        private RectTransform scoreRt, photoRt, briefRt;
        private readonly List<NativeUiRect> nativeRects = new List<NativeUiRect>();
        private readonly List<GameObject> nativeSlots = new List<GameObject>();
        private readonly List<Vector4> briefMargins = new List<Vector4>();
        private Rect nativePage;
        private RectTransform photoTemplate;
        private TextMeshProUGUI photoText;
        private Color nativeButtonColor = Color.white, nativeButtonText = Color.white;
        private readonly List<TextMeshProUGUI> briefTexts = new List<TextMeshProUGUI>();
        private readonly List<float> briefFontSizes = new List<float>();
        private static Sprite whiteSprite;
        private static Texture2D whitePixel;
        private static readonly Color Ink = new Color32(67, 46, 43, 255);
        private static readonly Color Muted = new Color32(108, 86, 77, 255);
        private static readonly Color Cocoa = new Color32(75, 48, 43, 255);

        public long Begin()
        {
            ObserveHistory(out _, out _, out _, true);
            result = null;
            message = UiStrings.Get("status.preparing");
            contextTab = false;
            RefreshContent();
            return selection.Revision;
        }

        public void Complete(long ticket, object evaluatedState, JuryAssessment assessment, string statusKey)
        {
            if (disposed) return;
            // Resolve again on completion: Update may not yet have seen a page change.
            ObserveHistory(out _, out _, out _, true);
            if (!selection.IsCurrent(ticket) || (assessment != null && !selection.Accepts(ticket, evaluatedState))) return;
            result = assessment;
            message = assessment == null || statusKey == "status.done.context-failed" ? UiStrings.Get(statusKey) : null;
            RefreshContent();
        }

        public UiLayoutReport CaptureLayout() => UiLayoutProbe.Capture(cachedBook, layoutStatus, rootRt, briefRt, scoreRt, photoRt, tagList.Frame);
        public void SetHistoryOrigin(long ticket, object state, HistoryOrderEvidence evidence)
        {
            if (disposed || !selection.Accepts(ticket, state)) return;
            originLabel = evidence.origin == "off-rack" ? UiStrings.Get("origin.off-rack") :
                evidence.origin == "commission" ? UiStrings.Get("origin.commission") + ": " + evidence.definition?.prettyName : UiStrings.Get("origin.unknown");
            RefreshContent();
        }
        public UiLayoutReport OriginalLayout() => originalLayout ?? new UiLayoutReport { status = "not-bound" };

        public void Dispose()
        {
            disposed = true;
            tagList.Dispose();
            RestoreLayout();
            TearDown();
            HouseEmblems.Release();
            if (whiteSprite != null) UnityEngine.Object.Destroy(whiteSprite);
            if (whitePixel != null) { UnityEngine.Object.Destroy(whitePixel); whitePixel = null; }
            whiteSprite = null;
        }

        public void Tick(Action evaluateAction, bool isBusy)
        {
            if (disposed) return;
            evaluate = evaluateAction;
            busy = isBusy;
            bool history = ObserveHistory(out Component book, out RectTransform spread, out _);
            tagList.Tick(history ? null : book);
            if (!history)
            {
                RestoreLayout();
                TearDown();
                return;
            }
            if (!ReferenceEquals(boundBook, book) || root == null || !ReferenceEquals(pageRt, spread))
            {
                RestoreLayout();
                TearDown();
                Build(book, spread);
            }
            bool geometryChanged = PlaceJury(book, spread);
            if (geometryChanged && layoutApplied) RestoreLayout();
            if (hasPlacedRect) EnsureLayout(book, spread);
            else RestoreLayout();
            bool dirty = geometryChanged || !ReferenceEquals(paintedResult, result) || paintedContext != contextTab || paintedBusy != busy || paintedMessage != message;
            if (dirty) RefreshContent();
            else SyncChrome();
        }

        private bool ObserveHistory(out Component book, out RectTransform spread, out int questIndex, bool refresh = false)
        {
            bool visible = TryHistory(out book, out spread, out questIndex, refresh);
            object state = visible ? FieldSnapshot.ResolveFields(book, "_displayDress", "state") : null;
            if (selection.Observe(visible ? book : null, state, visible ? questIndex : -1))
            {
                RestoreLayout();
                result = null;
                message = null;
                originLabel = null;
                contextTab = false;
            }
            return visible;
        }

        // Cache discovery; active status and field identity are cheap to check each frame.
        private bool TryHistory(out Component book, out RectTransform spread, out int questIndex, bool refresh)
        {
            book = null; spread = null; questIndex = -1;
            if (refresh || Time.unscaledTime >= nextBookSearch)
            {
                nextBookSearch = Time.unscaledTime + 0.5f;
                var books = GameRuntimeReader.Find("Sketchbook").Where(c => c != null && c.gameObject.activeInHierarchy).Take(2).ToArray();
                cachedBook = books.Length == 1 ? books[0] : null;
            }
            if (cachedBook == null || !cachedBook.gameObject.activeInHierarchy) return false;
            book = cachedBook;
            var holder = FieldSnapshot.ResolveFields(book, "oldDressViewHolder") as GameObject;
            if (holder == null || !holder.activeInHierarchy) return false;
            spread = holder.GetComponent<RectTransform>() ?? holder.transform as RectTransform;
            if (spread == null) return false;
            object raw = FieldSnapshot.ResolveFields(book, "_oldQuestIndex");
            questIndex = raw is int i ? i : -1;
            return true;
        }

        private void TearDown()
        {
            boundBook = null;
            pageRt = null;
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            rootRt = scrollContent = housesContent = orderContent = null;
            evaluateLabel = housesTabLabel = orderTabLabel = meanLabel = null;
            evaluateImage = housesTabImage = orderTabImage = null;
            scroll = null;
            rows.Clear();
            paintedResult = null;
            hasPlacedRect = false;
            layoutStatus = "not-bound";
            originalLayout = null;
        }

        private void Build(Component book, RectTransform spread)
        {
            boundBook = book;
            pageRt = spread;
            BorrowLook(book, spread);
            root = new GameObject("DressCriticJuryInline", typeof(RectTransform), typeof(VerticalLayoutGroup));
            root.transform.SetParent(spread, false);
            root.transform.SetAsLastSibling();
            rootRt = root.GetComponent<RectTransform>();
            var vertical = root.GetComponent<VerticalLayoutGroup>();
            vertical.spacing = 6f;
            vertical.padding = new RectOffset(4, 4, 2, 2);
            vertical.childAlignment = TextAnchor.UpperLeft;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            // Top toolbar — layout group keeps buttons on the left page width.
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            bar.transform.SetParent(root.transform, false);
            var barLe = bar.GetComponent<LayoutElement>();
            barLe.preferredHeight = 32f;
            barLe.minHeight = 32f;
            barLe.flexibleHeight = 0f;
            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(0, 0, 0, 0);

            evaluateImage = MakeButton(bar.transform, "Evaluate", UiStrings.Get("evaluate"), 130, 30, () =>
            {
                if (!busy) evaluate?.Invoke();
            }, out evaluateLabel);
            housesTabImage = MakeButton(bar.transform, "Houses", UiStrings.Get("houses"), 88, 28, () =>
            {
                contextTab = false; RefreshContent();
            }, out housesTabLabel);
            orderTabImage = MakeButton(bar.transform, "Order", UiStrings.Get("order"), 72, 28, () =>
            {
                contextTab = true; RefreshContent();
            }, out orderTabLabel);

            meanLabel = MakeLabel(root.transform, "Mean", "", 16, Cocoa, TextAlignmentOptions.Left);
            var meanLe = meanLabel.gameObject.AddComponent<LayoutElement>();
            meanLe.preferredHeight = 22f;
            meanLe.minHeight = 20f;
            meanLe.flexibleHeight = 0f;

            var scrollHost = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
            scrollHost.transform.SetParent(root.transform, false);
            var scrollLe = scrollHost.GetComponent<LayoutElement>();
            scrollLe.minHeight = 120f;
            scrollLe.preferredHeight = 220f;
            scrollLe.flexibleHeight = 1f;
            var scrollRt = scrollHost.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollHost.transform, false);
            // Transparent graphic is the scroll target; TMP labels deliberately do not raycast.
            var scrollSurface = viewport.GetComponent<Image>();
            scrollSurface.color = Color.clear;
            scrollSurface.raycastTarget = true;
            Stretch(viewport.GetComponent<RectTransform>(), 0, 0, 0, 0);
            scrollContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            scrollContent.SetParent(viewport.transform, false);
            scrollContent.anchorMin = new Vector2(0, 1);
            scrollContent.anchorMax = new Vector2(1, 1);
            scrollContent.pivot = new Vector2(0.5f, 1);
            scrollContent.anchoredPosition = Vector2.zero;
            scrollContent.sizeDelta = new Vector2(0, 40);
            scroll = scrollHost.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = scrollContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            housesContent = new GameObject("Houses", typeof(RectTransform)).GetComponent<RectTransform>();
            housesContent.SetParent(scrollContent, false);
            TopContent(housesContent);
            orderContent = new GameObject("Order", typeof(RectTransform)).GetComponent<RectTransform>();
            orderContent.SetParent(scrollContent, false);
            TopContent(orderContent);

            StripLocalization(root);
            RefreshContent();
        }

        private void BorrowLook(Component book, RectTransform host)
        {
            font = null; fontMaterial = null;
            foreach (string field in new[] { "oldQuestText", "oldQuestGiver", "questText" })
            {
                var text = FieldSnapshot.ResolveFields(book, field) as TextMeshProUGUI;
                if (text == null || text.font == null) continue;
                font = text.font; fontMaterial = text.fontSharedMaterial; break;
            }
            photoTemplate = FindPhotoButton(book.transform as RectTransform, true);
            photoText = photoTemplate == null ? null : photoTemplate.GetComponentInChildren<TextMeshProUGUI>(true);
            var image = photoTemplate == null ? null : photoTemplate.GetComponent<Image>();
            buttonBorder = image != null ? image.sprite : FindSprite("roundedButtonBorder");
            nativeButtonColor = image == null ? Color.white : image.color;
            nativeButtonText = photoText == null ? Color.white : photoText.color;
        }

        private static Sprite FindSprite(string name)
        {
            try { return Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s != null && s.name == name); }
            catch { return null; }
        }

        private void EnsureLayout(Component book, RectTransform page)
        {
            if (layoutApplied) return;
            RestoreLayout();
            briefRt = ResolveBriefCard(book, page);
            scoreRt = ResolveScoreCard(book);
            photoRt = FindPhotoButton(book.transform as RectTransform);
            originalLayout = UiLayoutProbe.Capture(book, "before-native-layout-changes", rootRt, briefRt, scoreRt, photoRt);
            if (briefRt != null)
            {
                nativeRects.Add(new NativeUiRect(briefRt));
                IsolateCard(briefRt, page, Region(nativePage, 0.06f, 0.83f, 0.88f, 0.14f));
                foreach (string field in new[] { "oldQuestTitle", "oldQuestText", "oldQuestGiver" })
                {
                    var text = FieldSnapshot.ResolveFields(book, field) as TextMeshProUGUI;
                    if (text == null || !text.transform.IsChildOf(briefRt) || text.rectTransform == briefRt) continue;
                    briefTexts.Add(text); briefFontSizes.Add(text.fontSize); briefMargins.Add(text.margin);
                    nativeRects.Add(new NativeUiRect(text.rectTransform));
                    // Widen the paper but reserve the upper right for the game's hand clip.
                    var rt = text.rectTransform;
                    rt.anchorMin = new Vector2(0, rt.anchorMin.y);
                    rt.anchorMax = new Vector2(1, rt.anchorMax.y);
                    rt.offsetMin = new Vector2(briefRt.rect.width * 0.05f, rt.offsetMin.y);
                    rt.offsetMax = new Vector2(-briefRt.rect.width * 0.23f, rt.offsetMax.y);
                    text.margin = new Vector4(0, text.margin.y, 0, text.margin.w);
                    text.fontSize = briefFontSizes[briefFontSizes.Count - 1] * 0.9f;
                }
                NativeUiRect.FitVisuals(briefRt, page, Region(nativePage, 0.06f, 0.83f, 0.88f, 0.14f));
            }
            if (scoreRt != null)
            {
                nativeRects.Add(new NativeUiRect(scoreRt));
                var scoreArea = Region(nativePage, 0.05f, 0.60f, 0.90f, 0.21f);
                IsolateCard(scoreRt, page, scoreArea);
                NativeUiRect.FitVisuals(scoreRt, page, scoreArea);
            }
            if (photoRt != null)
            {
                nativeRects.Add(new NativeUiRect(photoRt));
                NativeUiRect.FitVisuals(photoRt, page, Region(nativePage, 0.37f, 0.045f, 0.26f, 0.035f));
            }
            layoutApplied = true;
        }

        private static Rect Region(Rect page, float x, float y, float width, float height) =>
            new Rect(page.x + page.width * x, page.y + page.height * y, page.width * width, page.height * height);

        private void IsolateCard(RectTransform card, RectTransform page, Rect area)
        {
            // The original parent can drive its child's anchors after our placement.
            // Own the layout parent, while retaining the actual game graphics and text.
            var slot = new GameObject("DressCriticNativeCardSlot", typeof(RectTransform));
            slot.transform.SetParent(page, false);
            nativeSlots.Add(slot);
            var rect = slot.GetComponent<RectTransform>();
            NativeUiRect.Place(rect, page, area);
            card.SetParent(rect, true);
            Vector2 size = card.rect.size; Vector3 position = card.position;
            card.anchorMin = card.anchorMax = rect.pivot;
            card.sizeDelta = size; card.position = position;
        }

        private static RectTransform ResolveBriefCard(Component book, RectTransform spread)
        {
            var title = FieldSnapshot.ResolveFields(book, "oldQuestTitle") as TextMeshProUGUI;
            var body = FieldSnapshot.ResolveFields(book, "oldQuestText") as TextMeshProUGUI;
            if (title == null && body == null) return null;
            // Walk up from the title to the parchment Image card. Never return the spread,
            // leftPage, backing or canvas — scaling those collapses the whole notebook.
            Transform walk = title != null ? title.transform : body.transform;
            RectTransform fallback = title != null ? title.rectTransform : body.rectTransform;
            while (walk != null)
            {
                if (IsLayoutForbidden(walk, spread)) break;
                var rt = walk as RectTransform;
                if (rt != null && walk.GetComponent<Image>() != null)
                {
                    float h = Math.Abs(rt.rect.height);
                    float w = Math.Abs(rt.rect.width);
                    if (h >= 48f && h <= 420f && w >= 120f) return rt;
                }
                walk = walk.parent;
            }
            // Shared ancestor of title+body, still excluding forbidden roots.
            if (title != null && body != null)
            {
                var shared = CommonAncestor(title.rectTransform, body.rectTransform, spread);
                if (shared != null && !IsLayoutForbidden(shared, spread))
                {
                    float h = Math.Abs(shared.rect.height);
                    if (h >= 48f && h <= 520f) return shared;
                }
            }
            return fallback;
        }

        private static bool IsLayoutForbidden(Transform t, RectTransform spread)
        {
            if (t == null) return true;
            if (spread != null && (t == spread || t == spread.transform)) return true;
            if (t.GetComponent<Canvas>() != null) return true;
            string n = t.name ?? "";
            return n.IndexOf("leftPage", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("backing", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("oldDressViewHolder", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("DraftView", StringComparison.OrdinalIgnoreCase) >= 0
                || n.Equals("Canvas", StringComparison.OrdinalIgnoreCase);
        }

        private static RectTransform CommonAncestor(RectTransform a, RectTransform b, RectTransform stop)
        {
            if (a == null) return b;
            if (b == null) return a;
            var seen = new HashSet<Transform>();
            for (Transform t = a; t != null && t != stop; t = t.parent) seen.Add(t);
            for (Transform t = b; t != null && t != stop; t = t.parent)
                if (seen.Contains(t) && t is RectTransform rt) return rt;
            return null;
        }

        private static RectTransform ResolveScoreCard(Component book)
        {
            var wrapper = FieldSnapshot.ResolveFields(book, "oldQuestScoreDisplay") as GameObject;
            // Only the exact history wrapper: inventoryRect may be shared with WIP.
            return wrapper != null && wrapper.activeInHierarchy ? wrapper.GetComponent<RectTransform>() : null;
        }

        private bool PlaceJury(Component book, RectTransform spread)
        {
            if (rootRt == null || spread == null) return false;
            // Holder may be a zero-sized grouping transform. Measure the actual page
            // in holder-local space instead of treating holder anchors as page geometry.
            var leaf = FieldSnapshot.ResolveFields(book, "leftPage") as RectTransform;
            if (leaf == null || leaf.rect.width < 80 || leaf.rect.height < 120)
            {
                layoutStatus = "left-page-geometry-unavailable";
                hasPlacedRect = false;
                root.SetActive(false);
                return false;
            }
            var corners = new Vector3[4];
            leaf.GetWorldCorners(corners);
            Vector2 min = spread.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = spread.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            var candidate = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            if (!layoutApplied || candidate.size != nativePage.size)
            {
                var marker = FindPhotoButton(book.transform as RectTransform);
                var brief = ResolveBriefCard(book, spread);
                if (marker == null && brief == null)
                {
                    layoutStatus = "native-left-page-marker-unavailable";
                    hasPlacedRect = false; root.SetActive(false); return false;
                }
                float markerX = NativeUiRect.Bounds(marker != null ? marker : brief, spread).center.x;
                double left = NotebookPageGeometry.LeftForMarker(candidate.x, candidate.width, markerX);
                if (double.IsNaN(left))
                {
                    layoutStatus = "native-left-page-marker-conflict";
                    hasPlacedRect = false; root.SetActive(false); return false;
                }
                nativePage = new Rect((float)left, candidate.y, candidate.width, candidate.height);
            }
            var area = Region(nativePage, 0.06f, 0.12f, 0.88f, 0.40f);
            if (float.IsNaN(area.width) || float.IsInfinity(area.width) || float.IsNaN(area.height) ||
                float.IsInfinity(area.height) || area.width < 80 || area.height < 80)
            {
                layoutStatus = "left-page-transform-invalid";
                hasPlacedRect = false;
                root.SetActive(false);
                return false;
            }
            root.SetActive(true);
            if (hasPlacedRect && placedRect == area) return false;
            hasPlacedRect = true;
            placedRect = area;
            layoutStatus = "native-marker-left-page-runtime-review-required";
            rootRt.anchorMin = rootRt.anchorMax = spread.pivot;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            // Fit the existing toolbar into small notebook coordinate systems.
            float scale = Math.Min(1f, Math.Min(area.width / 330f, area.height / 190f));
            rootRt.anchoredPosition = area.center;
            rootRt.sizeDelta = area.size / scale;
            rootRt.localScale = Vector3.one * scale;
            rootRt.localRotation = Quaternion.identity;
            root.transform.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
            return true;
        }

        private void RestoreLayout()
        {
            for (int i = nativeRects.Count - 1; i >= 0; i--) nativeRects[i].Restore();
            nativeRects.Clear();
            foreach (var slot in nativeSlots)
                if (slot != null) { slot.SetActive(false); UnityEngine.Object.Destroy(slot); }
            nativeSlots.Clear();
            for (int i = 0; i < briefTexts.Count; i++)
            {
                if (briefTexts[i] == null) continue;
                briefTexts[i].fontSize = briefFontSizes[i];
                briefTexts[i].margin = briefMargins[i];
            }
            briefTexts.Clear(); briefFontSizes.Clear(); briefMargins.Clear();
            briefRt = scoreRt = photoRt = null;
            layoutApplied = false;
        }

        private static RectTransform FindPhotoButton(RectTransform page, bool includeInactive = false)
        {
            if (page == null) return null;
            RectTransform best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var tmp in page.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp == null || (!includeInactive && !tmp.gameObject.activeInHierarchy)) continue;
                string text = tmp.text ?? "";
                if (text.IndexOf("Photo", StringComparison.OrdinalIgnoreCase) < 0
                    && text.IndexOf("Фото", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var button = tmp.GetComponentInParent<Button>();
                var rt = button != null ? button.GetComponent<RectTransform>() : tmp.transform.parent as RectTransform;
                if (rt == null) continue;
                float area = Math.Abs(rt.rect.width * rt.rect.height);
                float score = 100000f + area;
                if (score > bestScore) { bestScore = score; best = rt; }
            }
            if (best != null) return best;
            // Do not shrink an unrelated button when the localized Photo label is unknown.
            return null;
        }

        private void SyncChrome()
        {
            if (evaluateLabel != null) evaluateLabel.text = busy ? UiStrings.Get("evaluating") : UiStrings.Get("evaluate");
            if (evaluateImage != null)
            {
                evaluateImage.color = busy ? nativeButtonColor * 0.7f : nativeButtonColor;
                var button = evaluateImage.GetComponent<Button>();
                if (button != null) button.interactable = !busy;
            }
            if (housesTabImage != null) housesTabImage.color = !contextTab ? nativeButtonColor : nativeButtonColor * 0.85f;
            if (orderTabImage != null) orderTabImage.color = contextTab ? nativeButtonColor : nativeButtonColor * 0.85f;
            if (housesTabLabel != null) housesTabLabel.color = nativeButtonText;
            if (orderTabLabel != null) orderTabLabel.color = nativeButtonText;
            if (housesContent != null) housesContent.gameObject.SetActive(!contextTab);
            if (orderContent != null) orderContent.gameObject.SetActive(contextTab);
        }

        private void RefreshContent()
        {
            paintedResult = result;
            paintedContext = contextTab;
            paintedBusy = busy;
            paintedMessage = message;
            if (root == null) return;
            SyncChrome();
            if (meanLabel != null)
            {
                if (result == null) meanLabel.text = busy ? UiStrings.Get("evaluating") : "";
                else
                {
                    string mean = Number(result.summary.average);
                    string verdict = UiStrings.Verdict(result.summary.verdict);
                    meanLabel.text = mean + (result.summary.divided == true ? " · " + UiStrings.Get("divided") : "") + " · " + verdict;
                }
            }
            foreach (var go in rows) if (go != null) UnityEngine.Object.Destroy(go);
            rows.Clear();
            // Resolve final viewport width before measuring wrapped TMP text.
            if (rootRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
            float y = 0;
            if (!string.IsNullOrEmpty(message))
                y = AddLine(contextTab ? orderContent : housesContent, message, 14, Muted, y);
            if (!contextTab)
            {
                if (result != null)
                    foreach (var vote in result.jury) y = AddHouseRow(housesContent, vote, y);
                Layout(housesContent, y);
            }
            else
            {
                if (!string.IsNullOrEmpty(originLabel)) y = AddLine(orderContent, originLabel, 15, Cocoa, y);
                if (result != null)
                {
                    y = AddLine(orderContent, UiStrings.Get("order.compliance") + ": " + Number(result.orderCompliance.score), 15, Cocoa, y);
                    foreach (string remark in result.orderCompliance.remarks ?? Enumerable.Empty<string>())
                        if (result.orderCompliance.status != "unknown") y = AddLine(orderContent, remark, 14, Ink, y);
                    y = AddLine(orderContent, UiStrings.Get("customer.prefs") + ": " + Number(result.customerCompatibility.score), 15, Cocoa, y);
                    foreach (string remark in result.customerCompatibility.remarks ?? Enumerable.Empty<string>())
                        if (result.customerCompatibility.status != "unknown") y = AddLine(orderContent, remark, 14, Ink, y);
                }
                Layout(orderContent, y);
            }
            if (scrollContent != null)
            {
                scrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Math.Max(40, y + 8));
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
            }
            if (rootRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
        }

        private float AddHouseRow(RectTransform parent, CriticVote vote, float y)
        {
            var row = new GameObject("House-" + vote.houseId, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0, 1);
            rowRt.anchorMax = new Vector2(1, 1);
            rowRt.pivot = new Vector2(0.5f, 1);
            rowRt.sizeDelta = Vector2.zero;
            rowRt.anchoredPosition = new Vector2(0, -y);
            rows.Add(row);

            var emblem = new GameObject("Emblem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            emblem.transform.SetParent(row.transform, false);
            var emblemImage = emblem.GetComponent<Image>();
            emblemImage.sprite = HouseEmblems.For(vote.houseId);
            emblemImage.preserveAspect = true;
            emblemImage.raycastTarget = false;
            var emblemRt = emblem.GetComponent<RectTransform>();
            float emblemWidth = Mathf.Clamp(parent.rect.width * 0.18f, 64, 96);
            float textLeft = emblemWidth + 12;
            emblemRt.anchorMin = Vector2.zero;
            emblemRt.anchorMax = new Vector2(0, 1);
            emblemRt.pivot = new Vector2(0, 0.5f);
            emblemRt.anchoredPosition = Vector2.zero;
            emblemRt.sizeDelta = new Vector2(emblemWidth, -8);

            var name = MakeLabel(row.transform, "Name", UiStrings.House(vote.houseId) + " — " + Number(vote.score), 15, Cocoa, TextAlignmentOptions.Left);
            var nameRt = name.rectTransform;
            nameRt.anchorMin = new Vector2(0, 1);
            nameRt.anchorMax = new Vector2(1, 1);
            nameRt.pivot = new Vector2(0, 1);
            nameRt.anchoredPosition = new Vector2(textLeft, -2);
            nameRt.sizeDelta = new Vector2(-textLeft - 6, 20);

            float body = 38;
            float width = Math.Max(1f, parent.rect.width - textLeft - 6);
            foreach (string remark in vote.remarks ?? Enumerable.Empty<string>())
            {
                var line = MakeLabel(row.transform, "Remark", remark, 14, Ink, TextAlignmentOptions.Left);
                float h = Math.Max(18, line.GetPreferredValues(remark, width, float.PositiveInfinity).y + 2);
                var lineRt = line.rectTransform;
                lineRt.anchorMin = new Vector2(0, 1);
                lineRt.anchorMax = new Vector2(1, 1);
                lineRt.pivot = new Vector2(0, 1);
                lineRt.anchoredPosition = new Vector2(textLeft, -body);
                lineRt.sizeDelta = new Vector2(-textLeft - 6, h);
                body += h + 2;
            }
            rowRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Math.Max(40, body + 6));
            return y + rowRt.sizeDelta.y + 8;
        }

        private float AddLine(RectTransform parent, string text, float size, Color color, float y)
        {
            var label = MakeLabel(parent, "Line", text, size, color, TextAlignmentOptions.Left);
            float width = Math.Max(120, parent.rect.width - 8);
            float h = Math.Max(size + 4, label.GetPreferredValues(text, width, float.PositiveInfinity).y + 2);
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -y);
            rt.sizeDelta = new Vector2(0, h);
            rows.Add(label.gameObject);
            return y + h + 4;
        }

        private static void Layout(RectTransform content, float height)
        {
            if (content != null) content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Math.Max(40, height));
        }

        private static string Number(double? value) => value.HasValue
            ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) + " / 10"
            : UiStrings.Get("insufficient");

        private static void TopContent(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private Image MakeButton(Transform parent, string name, string text, float w, float h, Action click, out TextMeshProUGUI label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = buttonBorder != null ? buttonBorder : WhiteSprite();
            image.type = buttonBorder != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = nativeButtonColor;
            if (photoTemplate != null)
            {
                var source = photoTemplate.GetComponent<Image>();
                if (source != null) CopyImage(source, image);
                CopyPhotoDecorations(go.transform);
            }
            image.raycastTarget = true;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
            le.minWidth = w;
            le.minHeight = h;
            label = MakeLabel(go.transform, "Label", text, 14, nativeButtonText, TextAlignmentOptions.Center);
            if (photoText != null)
            {
                label.font = photoText.font; label.fontSharedMaterial = photoText.fontSharedMaterial;
                label.fontStyle = photoText.fontStyle;
            }
            label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = 14;
            Stretch(label.rectTransform, 4, 2, -4, -2);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var native = photoTemplate == null ? null : photoTemplate.GetComponent<Button>();
            if (native != null)
            {
                button.transition = native.transition == Selectable.Transition.Animation ? Selectable.Transition.ColorTint : native.transition;
                button.colors = native.colors; button.spriteState = native.spriteState;
            }
            button.onClick.AddListener(new UnityAction(click));
            return image;
        }

        internal static void CopyImage(Image source, Image target)
        {
            target.sprite = source.sprite; target.type = source.type;
            target.material = source.material; target.color = source.color;
            target.preserveAspect = source.preserveAspect; target.fillCenter = source.fillCenter;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        }

        private void CopyPhotoDecorations(Transform target)
        {
            Rect bounds = photoTemplate.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            // Copy visual Image data only. Never clone Photo Mode scripts/listeners.
            foreach (var image in photoTemplate.GetComponentsInChildren<Image>(true).Take(16))
            {
                if (image.transform == photoTemplate || !image.gameObject.activeSelf) continue;
                var go = new GameObject("PhotoModeDecoration", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(target, false);
                var copy = go.GetComponent<Image>(); CopyImage(image, copy); copy.raycastTarget = false;
                Rect area = NativeUiRect.Bounds(image.rectTransform, photoTemplate);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2((area.xMin - bounds.xMin) / bounds.width, (area.yMin - bounds.yMin) / bounds.height);
                rt.anchorMax = new Vector2((area.xMax - bounds.xMin) / bounds.width, (area.yMax - bounds.yMin) / bounds.height);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }

        private TextMeshProUGUI MakeLabel(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            if (fontMaterial != null) tmp.fontSharedMaterial = fontMaterial;
            tmp.text = text ?? "";
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.richText = false;
            tmp.fontStyle = FontStyles.Normal;
            StripLocalization(go);
            return tmp;
        }

        private static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        private static Sprite WhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            if (whitePixel == null)
            {
                whitePixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                whitePixel.SetPixel(0, 0, Color.white);
                whitePixel.Apply(false, true);
            }
            whiteSprite = Sprite.Create(whitePixel, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return whiteSprite;
        }

        private static void StripLocalization(GameObject root)
        {
            if (root == null) return;
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                string type = behaviour.GetType().Name;
                if (type == "LocaleFontApplier" || type == "LocalizedTextFont" || type == "LocalizeStringEvent")
                    UnityEngine.Object.Destroy(behaviour);
            }
        }
    }
}
