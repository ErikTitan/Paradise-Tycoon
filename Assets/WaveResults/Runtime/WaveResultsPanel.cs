using System;
using System.Globalization;
using System.Text;
using ParadiseTycoon.Hud;
using ParadiseTycoon.Scrum104;
using UnityEngine;
using UnityEngine.UI;

namespace ParadiseTycoon.WaveResults
{
    public sealed class WaveResultsPanel : MonoBehaviour, IWaveResultsView
    {
        static readonly Color Ink = new Color(.06f, .17f, .22f);
        static readonly Color Sea = new Color(.04f, .47f, .49f);
        static readonly CultureInfo Format = CultureInfo.GetCultureInfo("sk-SK");
        readonly HudIcon[] stars = new HudIcon[5];
        RectTransform safeArea, card;
        GameObject overlay;
        Text summary, reward, explanation;
        Rect lastSafeArea;
        Vector2Int lastScreen;
        public event Action ReturnRequested;
        public bool IsVisible => overlay != null && overlay.activeSelf;

        public static WaveResultsPanel Create()
        {
            var root = new GameObject("WaveResults", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            var panel = root.AddComponent<WaveResultsPanel>();
            panel.Build();
            return panel;
        }

        void Build()
        {
            overlay = Rect("ResultsOverlay", transform).gameObject;
            Stretch((RectTransform)overlay.transform);
            overlay.AddComponent<Image>().color = new Color(0, 0, 0, .65f);
            safeArea = Rect("SafeArea", overlay.transform);
            card = Rect("Card", safeArea);
            card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
            card.gameObject.AddComponent<Image>().color = new Color(.94f, .96f, .91f);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 20, 20);
            layout.spacing = 9;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var title = Label("Title", card, "Výsledok vlny", 32, 42, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;

            var starsRow = Rect("Stars", card);
            Height(starsRow, 50);
            var row = starsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            row.spacing = 12;
            for (int i = 0; i < stars.Length; i++)
            {
                var icon = Rect("Star" + (i + 1), starsRow);
                var size = icon.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = size.preferredHeight = 46;
                stars[i] = icon.gameObject.AddComponent<HudIcon>();
                stars[i].SetShape(HudIcon.Shape.Resort);
                stars[i].raycastTarget = false;
            }
            summary = Label("Summary", card, "", 23, 34, TextAnchor.MiddleCenter);
            summary.resizeTextForBestFit = true;
            summary.resizeTextMinSize = 17;
            summary.resizeTextMaxSize = 23;
            var rewardRow = CenteredRow("RewardRow", card, 48);
            var rewardBox = Rect("Reward", rewardRow);
            Width(rewardBox, 480);
            reward = Label("RewardText", rewardBox, "", 24, 48, TextAnchor.MiddleCenter);
            Stretch(reward.rectTransform);
            reward.fontStyle = FontStyle.Bold;
            reward.color = new Color(.45f, .29f, .04f);
            var divider = Rect("Divider", card);
            Height(divider, 1);
            divider.gameObject.AddComponent<Image>().color = new Color(.06f, .17f, .22f, .15f);
            var explanationTitle = Label("ExplanationTitle", card, "Čo ovplyvnilo hodnotenie", 25, 34, TextAnchor.MiddleLeft);
            explanationTitle.fontStyle = FontStyle.Bold;

            var scroll = Rect("Explanation", card);
            var scrollSize = scroll.gameObject.AddComponent<LayoutElement>();
            scrollSize.minHeight = 85;
            scrollSize.flexibleHeight = 1;
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", scroll);
            Stretch(viewport);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(0, -4);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            explanation = Label("Services", viewport, "", 23, 0, TextAnchor.UpperLeft);
            explanation.supportRichText = true;
            var content = explanation.rectTransform;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            var buttonRow = CenteredRow("ButtonRow", card, 50);
            var buttonRect = Rect("ReturnToIsland", buttonRow);
            Width(buttonRect, 480);
            var background = buttonRect.gameObject.AddComponent<Image>();
            background.color = Sea;
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var buttonLabel = Label("Label", buttonRect, "Späť na ostrov", 24, 50, TextAnchor.MiddleCenter);
            Stretch(buttonLabel.rectTransform);
            buttonLabel.color = Color.white;
            button.onClick.AddListener(() => {
                if (PauseMenuController.Instance != null) PauseMenuController.Instance.PlayClick();
                ReturnRequested?.Invoke();
            });
            overlay.SetActive(false);
        }

        public void Show(WaveResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            for (int i = 0; i < stars.Length; i++)
                stars[i].color = i < result.Stars ? new Color(.94f, .67f, .16f) : new Color(.68f, .72f, .70f);
            summary.text = "Spokojnosť: " + (result.Satisfaction * 100).ToString("0", Format)
                + " %   •   Vybavené požiadavky: " + result.CompletedCount + " / " + result.RequestedCount;
            reward.text = "Odmena za vlnu: +" + result.Coins.ToString("N0", Format) + " mincí";
            var text = new StringBuilder();
            int shown = 0;
            foreach (var service in result.ServicesByImpact)
            {
                if (service.LostSatisfactionPoints <= .000001 || shown == 3) break;
                if (shown > 0) text.AppendLine();
                text.Append("• <b>").Append(service.Stats.DisplayName.Replace("<", "‹").Replace(">", "›")).Append("</b>: ");
                if (service.Stats.CompletedCount > 0)
                    text.Append("priemerné čakanie ").Append(service.AverageWaitSeconds.ToString("0.#", Format)).Append(" s; ");
                text.Append("vybavené ").Append(service.Stats.CompletedCount).Append(" / ").Append(service.Stats.RequestedCount);
                shown++;
            }
            explanation.text = shown == 0 ? "Žiadna služba neznížila hodnotenie. Výborná obsluha!" :
                "Najväčší vplyv na pokles spokojnosti mali:" + Environment.NewLine + text;
            overlay.SetActive(true);
            UpdateLayout();
            explanation.rectTransform.anchoredPosition = Vector2.zero;
        }

        public void Hide() => overlay.SetActive(false);

        void LateUpdate()
        {
            if (IsVisible && (lastSafeArea != Screen.safeArea || lastScreen != new Vector2Int(Screen.width, Screen.height)))
                UpdateLayout();
        }

        void UpdateLayout()
        {
            lastSafeArea = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            float width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            safeArea.anchorMin = new Vector2(lastSafeArea.xMin / width, lastSafeArea.yMin / height);
            safeArea.anchorMax = new Vector2(lastSafeArea.xMax / width, lastSafeArea.yMax / height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            card.sizeDelta = new Vector2(Mathf.Min(850, Mathf.Max(320, safeArea.rect.width - 32)),
                Mathf.Min(550, Mathf.Max(400, safeArea.rect.height - 32)));
            Canvas.ForceUpdateCanvases();
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static RectTransform CenteredRow(string name, Transform parent, float height)
        {
            var rect = Rect(name, parent);
            Height(rect, height);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return rect;
        }

        static void Width(RectTransform rect, float width)
        {
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minWidth = 0;
            size.preferredWidth = width;
        }

        static void Height(RectTransform rect, float height)
        {
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
        }

        static Text Label(string name, Transform parent, string value, int fontSize, float height, TextAnchor alignment)
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = value;
            label.fontSize = fontSize;
            label.color = Ink;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            if (height > 0) Height(rect, height);
            return label;
        }
    }
}
