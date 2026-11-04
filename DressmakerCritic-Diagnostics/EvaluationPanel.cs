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
        public string subject, message; // kept for Plugin; not drawn
        private bool contextTab;
        private bool busy;
        private Action evaluate;
        private Component boundBook;
        private int boundQuestIndex = int.MinValue;
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
        private bool paintedContext, paintedBusy;
        private bool layoutApplied;
        private RectTransform pageRt; // left notebook page when available
        private RectTransform scoreRt, photoRt, briefRt;
        private Vector2 scoreAnchored0, scoreSize0, photoAnchored0, photoSize0, briefAnchored0, briefSize0;
        private Vector2 scoreOffsetMin0, scoreOffsetMax0, photoOffsetMin0, photoOffsetMax0, briefOffsetMin0, briefOffsetMax0;
        private Vector3 scoreScale0, photoScale0, briefScale0;
        private readonly List<TextMeshProUGUI> briefTexts = new List<TextMeshProUGUI>();
        private readonly List<float> briefFontSizes = new List<float>();
        private static Sprite whiteSprite;
        private static Texture2D whitePixel;
        private static readonly Color Ink = new Color32(67, 46, 43, 255);
        private static readonly Color Muted = new Color32(108, 86, 77, 255);
        private static readonly Color Cocoa = new Color32(75, 48, 43, 255);
        private static readonly Color ButtonFace = new Color32(234, 219, 207, 255);
        private static readonly Color ActiveFace = new Color32(91, 65, 58, 255);
        private static readonly Color Paper = new Color32(241, 231, 222, 255);
        private static readonly Color Line = new Color32(176, 155, 142, 255);

        public void Begin()
        {
            result = null;
            subject = null;
            message = null;
            contextTab = false;
            RefreshContent();
        }

        public void Dispose()
        {
            RestoreLayout();
            TearDown();
            HouseEmblems.Release();
            if (whitePixel != null) { UnityEngine.Object.Destroy(whitePixel); whitePixel = null; }
            whiteSprite = null;
        }

        public void Tick(Action evaluateAction, bool isBusy)
        {
            evaluate = evaluateAction;
            busy = isBusy;
            if (!TryHistory(out Component book, out RectTransform spread, out int questIndex))
            {
                RestoreLayout();
                TearDown();
                return;
            }
            if (questIndex != boundQuestIndex)
            {
                RestoreLayout();
                boundQuestIndex = questIndex;
                result = null;
                subject = null;
                message = null;
                contextTab = false;
                layoutApplied = false;
            }
            if (!ReferenceEquals(boundBook, book) || root == null || !ReferenceEquals(pageRt, spread))
            {
                TearDown();
                Build(book, spread);
            }
            EnsureLayout(book, spread);
            PlaceJury(spread);
            bool dirty = !ReferenceEquals(paintedResult, result) || paintedContext != contextTab || paintedBusy != busy;
            if (dirty) RefreshContent();
            else SyncChrome();
        }

        // History only. oldDressViewHolder is the active spread (left brief+stats AND right
        // mannequin). Jury must be locked to the LEFT HALF of that spread — parenting to
        // leftPage / full-width anchors parked controls on the spine with ~zero height.
        private static bool TryHistory(out Component book, out RectTransform spread, out int questIndex)
        {
            book = null; spread = null; questIndex = -1;
            var books = GameRuntimeReader.Find("Sketchbook").Where(c => c != null && c.gameObject.activeInHierarchy).Take(2).ToArray();
            if (books.Length != 1) return false;
            book = books[0];
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
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollHost.transform, false);
            Stretch(viewport.GetComponent<RectTransform>(), 0, 0, 0, 0);
            scrollContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            scrollContent.SetParent(viewport.transform, false);
            scrollContent.anchorMin = new Vector2(0, 1);
            scrollContent.anchorMax = new Vector2(1, 1);
            scrollContent.pivot = new Vector2(0.5f, 1);
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
            Stretch(housesContent, 0, 0, 0, 0);
            orderContent = new GameObject("Order", typeof(RectTransform)).GetComponent<RectTransform>();
            orderContent.SetParent(scrollContent, false);
            Stretch(orderContent, 0, 0, 0, 0);

            StripLocalization(root);
            RefreshContent();
        }

        private void BorrowLook(Component book, RectTransform host)
        {
            font = null;
            fontMaterial = null;
            // Prefer body copy over italic title faces.
            foreach (string field in new[] { "oldQuestText", "oldQuestGiver", "questText", "requirementsText", "fabricUsageText" })
            {
                var tmp = FieldSnapshot.ResolveFields(book, field) as TextMeshProUGUI;
                if (tmp == null || tmp.font == null) continue;
                font = tmp.font;
                fontMaterial = tmp.fontSharedMaterial;
                break;
            }
            if (font == null)
            {
                var score = FieldSnapshot.ResolveFields(book, "comissionScoreDisplay") as Component;
                var label = score != null ? score.GetComponentInChildren<TextMeshProUGUI>(true) : null;
                if (label != null && label.font != null) { font = label.font; fontMaterial = label.fontSharedMaterial; }
            }
            buttonBorder = FindSprite("roundedButtonBorder");
            var draft = FieldSnapshot.ResolveFields(book, "draftButton") as GameObject;
            if (draft != null)
            {
                var img = draft.GetComponent<Image>() ?? draft.GetComponentInChildren<Image>(true);
                if (img != null && img.sprite != null) buttonBorder = img.sprite;
            }
            if (buttonBorder == null)
            {
                var photo = FindPhotoButton(host);
                if (photo != null)
                {
                    var img = photo.GetComponent<Image>();
                    if (img != null && img.sprite != null) buttonBorder = img.sprite;
                }
            }
        }

        private static Sprite FindSprite(string name)
        {
            try { return Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s != null && s.name == name); }
            catch { return null; }
        }

        private void EnsureLayout(Component book, RectTransform page)
        {
            if (layoutApplied && (scoreRt != null || briefRt != null)) return;
            RestoreLayout();
            briefRt = ResolveBriefCard(book, page);
            scoreRt = ResolveScoreCard(book);
            photoRt = FindPhotoButton(page);
            if (briefRt != null)
            {
                CaptureRect(briefRt, out briefAnchored0, out briefSize0, out briefScale0, out briefOffsetMin0, out briefOffsetMax0);
                ShrinkAndLift(briefRt, briefAnchored0, briefSize0, briefScale0, briefOffsetMin0, briefOffsetMax0, 0.62f, 0.35f, 0.18f);
                CaptureBriefFonts(book);
                for (int i = 0; i < briefTexts.Count; i++)
                    if (briefTexts[i] != null) briefTexts[i].fontSize = briefFontSizes[i] * 0.72f;
            }
            if (scoreRt != null)
            {
                CaptureRect(scoreRt, out scoreAnchored0, out scoreSize0, out scoreScale0, out scoreOffsetMin0, out scoreOffsetMax0);
                // Compress and lift hard so the lower-left leaf frees for jury + house rows.
                ShrinkAndLift(scoreRt, scoreAnchored0, scoreSize0, scoreScale0, scoreOffsetMin0, scoreOffsetMax0, 0.58f, 0.40f, 0.20f);
            }
            if (photoRt != null)
            {
                CaptureRect(photoRt, out photoAnchored0, out photoSize0, out photoScale0, out photoOffsetMin0, out photoOffsetMax0);
                photoRt.localScale = photoScale0 * 0.55f;
                bool stretch = photoRt.anchorMin != photoRt.anchorMax;
                if (stretch)
                {
                    photoRt.offsetMin = photoOffsetMin0 + new Vector2(40f, 0f);
                    photoRt.offsetMax = photoOffsetMax0 + new Vector2(-10f, -8f);
                }
                else
                {
                    photoRt.anchoredPosition = new Vector2(photoAnchored0.x + 48f, photoAnchored0.y - 8f);
                    if (photoSize0.x > 10f) photoRt.sizeDelta = photoSize0 * 0.7f;
                }
            }
            layoutApplied = true;
        }

        private static void ShrinkAndLift(RectTransform rt, Vector2 anchored0, Vector2 size0, Vector3 scale0, Vector2 offsetMin0, Vector2 offsetMax0, float scale, float liftFactor, float shrinkFactor)
        {
            rt.localScale = scale0 * scale;
            bool stretch = rt.anchorMin != rt.anchorMax;
            float h = Math.Max(60f, Math.Abs(rt.rect.height));
            if (stretch)
            {
                float lift = Math.Max(24f, h * liftFactor * 0.55f);
                float shrink = Math.Max(28f, h * shrinkFactor);
                rt.offsetMax = new Vector2(offsetMax0.x, offsetMax0.y + lift);
                rt.offsetMin = new Vector2(offsetMin0.x, offsetMin0.y + shrink);
            }
            else
            {
                rt.anchoredPosition = anchored0 + new Vector2(0f, h * liftFactor);
                if (size0.y > 10f) rt.sizeDelta = new Vector2(size0.x, size0.y * Math.Max(0.7f, scale));
            }
        }

        private void CaptureBriefFonts(Component book)
        {
            briefTexts.Clear();
            briefFontSizes.Clear();
            foreach (string field in new[] { "oldQuestTitle", "oldQuestText", "oldQuestGiver" })
            {
                var tmp = FieldSnapshot.ResolveFields(book, field) as TextMeshProUGUI;
                if (tmp == null) continue;
                briefTexts.Add(tmp);
                briefFontSizes.Add(tmp.fontSize);
            }
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
            var scoreComp = FieldSnapshot.ResolveFields(book, "comissionScoreDisplay") as Component;
            if (scoreComp != null)
            {
                var inventory = FieldSnapshot.ResolveFields(scoreComp, "inventoryRect") as RectTransform;
                if (inventory != null && inventory.gameObject.activeInHierarchy && Math.Abs(inventory.rect.height) >= 40f)
                    return inventory;
                var self = scoreComp.GetComponent<RectTransform>();
                if (self != null && Math.Abs(self.rect.height) >= 40f && Math.Abs(self.rect.height) <= 600f)
                    return self;
            }
            var wrapper = FieldSnapshot.ResolveFields(book, "oldQuestScoreDisplay") as GameObject;
            if (wrapper != null && wrapper.activeInHierarchy)
            {
                var rt = wrapper.GetComponent<RectTransform>();
                if (rt != null && Math.Abs(rt.rect.height) <= 600f) return rt;
            }
            return scoreComp != null ? scoreComp.GetComponent<RectTransform>() : null;
        }

        private static void CaptureRect(RectTransform rt, out Vector2 anchored, out Vector2 size, out Vector3 scale, out Vector2 offsetMin, out Vector2 offsetMax)
        {
            anchored = rt.anchoredPosition;
            size = rt.sizeDelta;
            scale = rt.localScale;
            offsetMin = rt.offsetMin;
            offsetMax = rt.offsetMax;
        }

        private void PlaceJury(RectTransform spread)
        {
            if (rootRt == null || spread == null) return;
            // LEFT HALF of the history spread only (right half is the mannequin page).
            rootRt.SetParent(spread, false);
            rootRt.anchorMin = new Vector2(0.045f, 0.06f);
            rootRt.anchorMax = new Vector2(0.455f, 0.56f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = new Vector2(8f, 8f);
            rootRt.offsetMax = new Vector2(-8f, -8f);
            rootRt.localScale = Vector3.one;
            rootRt.localRotation = Quaternion.identity;
            root.transform.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
        }

        private void RestoreLayout()
        {
            for (int i = 0; i < briefTexts.Count; i++)
                if (briefTexts[i] != null) briefTexts[i].fontSize = briefFontSizes[i];
            briefTexts.Clear();
            briefFontSizes.Clear();
            if (briefRt != null)
            {
                briefRt.anchoredPosition = briefAnchored0;
                briefRt.sizeDelta = briefSize0;
                briefRt.localScale = briefScale0;
                briefRt.offsetMin = briefOffsetMin0;
                briefRt.offsetMax = briefOffsetMax0;
            }
            if (scoreRt != null)
            {
                scoreRt.anchoredPosition = scoreAnchored0;
                scoreRt.sizeDelta = scoreSize0;
                scoreRt.localScale = scoreScale0;
                scoreRt.offsetMin = scoreOffsetMin0;
                scoreRt.offsetMax = scoreOffsetMax0;
            }
            if (photoRt != null)
            {
                photoRt.anchoredPosition = photoAnchored0;
                photoRt.sizeDelta = photoSize0;
                photoRt.localScale = photoScale0;
                photoRt.offsetMin = photoOffsetMin0;
                photoRt.offsetMax = photoOffsetMax0;
            }
            briefRt = scoreRt = photoRt = null;
            layoutApplied = false;
        }

        private static RectTransform FindPhotoButton(RectTransform page)
        {
            if (page == null) return null;
            RectTransform best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var tmp in page.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
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
            // Fallback: lowest large button on the left page.
            foreach (var button in page.GetComponentsInChildren<Button>(true))
            {
                if (button == null || !button.gameObject.activeInHierarchy) continue;
                var rt = button.GetComponent<RectTransform>();
                if (rt == null) continue;
                float area = Math.Abs(rt.rect.width * rt.rect.height);
                if (area < 2500f) continue;
                float score = -rt.anchoredPosition.y + area * 0.01f;
                if (score > bestScore) { bestScore = score; best = rt; }
            }
            return best;
        }

        private void SyncChrome()
        {
            if (evaluateLabel != null) evaluateLabel.text = busy ? UiStrings.Get("evaluating") : UiStrings.Get("evaluate");
            if (evaluateImage != null)
            {
                evaluateImage.color = busy ? Line : ButtonFace;
                var button = evaluateImage.GetComponent<Button>();
                if (button != null) button.interactable = !busy;
            }
            if (housesTabImage != null) housesTabImage.color = !contextTab ? ActiveFace : ButtonFace;
            if (orderTabImage != null) orderTabImage.color = contextTab ? ActiveFace : ButtonFace;
            if (housesTabLabel != null) housesTabLabel.color = !contextTab ? Paper : Ink;
            if (orderTabLabel != null) orderTabLabel.color = contextTab ? Paper : Ink;
            if (housesContent != null) housesContent.gameObject.SetActive(!contextTab);
            if (orderContent != null) orderContent.gameObject.SetActive(contextTab);
        }

        private void RefreshContent()
        {
            paintedResult = result;
            paintedContext = contextTab;
            paintedBusy = busy;
            if (root == null) return;
            SyncChrome();
            if (meanLabel != null)
            {
                if (result == null) meanLabel.text = "";
                else
                {
                    string mean = Number(result.summary.average);
                    string verdict = UiStrings.Verdict(result.summary.verdict);
                    meanLabel.text = mean + (result.summary.divided == true ? " · " + UiStrings.Get("divided") : "") + " · " + verdict;
                }
            }
            foreach (var go in rows) if (go != null) UnityEngine.Object.Destroy(go);
            rows.Clear();
            float y = 0;
            if (!contextTab)
            {
                if (result != null)
                    foreach (var vote in result.jury) y = AddHouseRow(housesContent, vote, y);
                Layout(housesContent, y);
            }
            else
            {
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
            rowRt.anchoredPosition = new Vector2(0, -y);
            rows.Add(row);

            var emblem = new GameObject("Emblem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            emblem.transform.SetParent(row.transform, false);
            var emblemImage = emblem.GetComponent<Image>();
            emblemImage.sprite = HouseEmblems.For(vote.houseId);
            emblemImage.preserveAspect = true;
            emblemImage.raycastTarget = false;
            var emblemRt = emblem.GetComponent<RectTransform>();
            emblemRt.anchorMin = emblemRt.anchorMax = new Vector2(0, 1);
            emblemRt.pivot = new Vector2(0, 1);
            emblemRt.anchoredPosition = new Vector2(0, -2);
            emblemRt.sizeDelta = new Vector2(34, 34);

            var name = MakeLabel(row.transform, "Name", UiStrings.House(vote.houseId) + " — " + Number(vote.score), 15, Cocoa, TextAlignmentOptions.Left);
            var nameRt = name.rectTransform;
            nameRt.anchorMin = new Vector2(0, 1);
            nameRt.anchorMax = new Vector2(1, 1);
            nameRt.pivot = new Vector2(0, 1);
            nameRt.anchoredPosition = new Vector2(42, -2);
            nameRt.sizeDelta = new Vector2(-48, 20);

            float body = 38;
            float width = Math.Max(280f, Math.Abs(parent.rect.width) > 40f ? parent.rect.width - 48f : 280f);
            foreach (string remark in vote.remarks ?? Enumerable.Empty<string>())
            {
                var line = MakeLabel(row.transform, "Remark", remark, 14, Ink, TextAlignmentOptions.Left);
                float h = EstimateHeight(remark, 14, width);
                var lineRt = line.rectTransform;
                lineRt.anchorMin = new Vector2(0, 1);
                lineRt.anchorMax = new Vector2(1, 1);
                lineRt.pivot = new Vector2(0, 1);
                lineRt.anchoredPosition = new Vector2(42, -body);
                lineRt.sizeDelta = new Vector2(-48, h);
                body += h + 2;
            }
            rowRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Math.Max(40, body + 6));
            return y + rowRt.sizeDelta.y + 8;
        }

        private float AddLine(RectTransform parent, string text, float size, Color color, float y)
        {
            var label = MakeLabel(parent, "Line", text, size, color, TextAlignmentOptions.Left);
            float width = Math.Max(120, parent.rect.width - 8);
            float h = EstimateHeight(text, size, width);
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

        private static float EstimateHeight(string text, float fontSize, float width)
        {
            if (string.IsNullOrEmpty(text)) return fontSize + 4;
            float charsPerLine = Math.Max(8f, width / (fontSize * 0.52f));
            int lines = Math.Max(1, Mathf.CeilToInt(text.Length / charsPerLine));
            return lines * (fontSize + 4) + 2;
        }

        private Image MakeButton(Transform parent, string name, string text, float w, float h, Action click, out TextMeshProUGUI label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = buttonBorder != null ? buttonBorder : WhiteSprite();
            image.type = buttonBorder != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = ButtonFace;
            image.raycastTarget = true;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
            le.minWidth = w;
            le.minHeight = h;
            label = MakeLabel(go.transform, "Label", text, 14, Ink, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 4, 2, -4, -2);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(new UnityAction(click));
            return image;
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
