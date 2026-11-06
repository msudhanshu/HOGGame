using System;
using System.Collections.Generic;
using Game.Core;
using Nixin.Icons;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Left drawer listing every era (name + subtitle + progress). Tap a row to jump there.</summary>
    public sealed class HuntEraDrawer : MonoBehaviour
    {
        const float Width = 780f;

        RectTransform _panel;
        RectTransform _list;
        Text _hint;
        CanvasGroup _group;
        Action<int> _onPick;
        bool _open;

        public bool IsOpen => _open;

        public static HuntEraDrawer Build(RectTransform parent, Action<int> onPick)
        {
            var theme = HuntUiTheme.Current;
            var root = HuntUiKit.Stretch(HuntUiKit.Rect("EraDrawer", parent));
            var drawer = root.gameObject.AddComponent<HuntEraDrawer>();
            drawer._onPick = onPick;
            drawer._group = HuntUiKit.Group(root.gameObject);

            var dim = HuntUiKit.Image(root, "Dim", HuntUiSprites.White, new Color(0.01f, 0.02f, 0.08f, 0.7f), true);
            HuntUiKit.Stretch(dim.rectTransform);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(drawer.Close);

            var panel = HuntUiKit.Rounded(root, "Panel", theme.panel, 56f, true);
            drawer._panel = panel.rectTransform;
            drawer._panel.anchorMin = new Vector2(0f, 0f);
            drawer._panel.anchorMax = new Vector2(0f, 1f);
            drawer._panel.pivot = new Vector2(0f, 0.5f);
            drawer._panel.sizeDelta = new Vector2(Width, 0f);
            drawer._panel.anchoredPosition = Vector2.zero;
            HuntUiKit.NeonEdge(panel.transform, theme.glow, 56f, 0.9f);

            var title = HuntUiKit.Label(panel.transform, "TIMELINES", 30, theme.inkSoft, TextAnchor.MiddleLeft, false, FontStyle.Bold, true);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(56f, -150f), new Vector2(-170f, -100f));
            var heading = HuntUiKit.Label(panel.transform, "ALL ERAS", 56, theme.ink, TextAnchor.MiddleLeft, false, FontStyle.Bold, true);
            Anchor(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(56f, -230f), new Vector2(-170f, -150f));

            var close = HuntUiKit.IconButton(panel.transform, NixinIcons.Load(NixinIcons.Close), theme.panelAlt, 104f, drawer.Close);
            HuntUiKit.Place(close.Root, new Vector2(1f, 1f), new Vector2(104f, 104f), new Vector2(-82f, -150f));

            // scrolling list
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(24f, 150f);
            scrollRt.offsetMax = new Vector2(-24f, -260f);
            var viewport = HuntUiKit.Stretch(HuntUiKit.Rect("Viewport", scrollRt));
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            var content = HuntUiKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.padding = new RectOffset(8, 8, 8, 24);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            drawer._list = content;

            drawer._hint = HuntUiKit.Label(panel.transform, "", 32, theme.inkSoft, TextAnchor.MiddleCenter, false, FontStyle.Normal);
            Anchor(drawer._hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(32f, 30f), new Vector2(-32f, 130f));

            root.gameObject.SetActive(false);
            return drawer;
        }

        static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        /// <summary>Rebuilds the rows and slides the drawer in.</summary>
        public void Open(IReadOnlyList<HuntEraInfo> eras)
        {
            if (_open)
                return;
            _open = true;
            Fill(eras);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            HuntTweener.Kill(_panel);
            HuntTweener.Move(_panel, new Vector2(-Width, 0f), Vector2.zero, 0.3f, Ease.OutCubic);
            HuntTweener.Fade(_group, 0f, 1f, 0.2f);
            HuntAudio.Sfx(Nixin.Audio.NixinAudio.Pause);
        }

        public void Close()
        {
            if (!_open)
                return;
            _open = false;
            HuntTweener.Kill(_panel);
            HuntTweener.Move(_panel, _panel.anchoredPosition, new Vector2(-Width, 0f), 0.22f, Ease.InQuad);
            HuntTweener.Fade(_group, 1f, 0f, 0.22f, 0f, () =>
            {
                if (!_open)
                    gameObject.SetActive(false);
            });
        }

        void Fill(IReadOnlyList<HuntEraInfo> eras)
        {
            for (var i = _list.childCount - 1; i >= 0; i--)
                Destroy(_list.GetChild(i).gameObject);

            string nextLocked = null;
            string currentName = null;
            for (var i = 0; i < eras.Count; i++)
            {
                Row(eras[i]);
                if (eras[i].Current)
                    currentName = HuntMapScreen.ChapterName(eras[i].Chapter);
                else if (!eras[i].Unlocked && nextLocked == null)
                    nextLocked = HuntMapScreen.ChapterName(eras[i].Chapter);
            }

            _hint.text = nextLocked != null && currentName != null
                ? "Finish " + currentName + " to open " + nextLocked
                : "";
        }

        void Row(HuntEraInfo era)
        {
            var theme = HuntUiTheme.Current;
            var done = era.Unlocked && era.Count > 0 && era.Cleared >= era.Count;
            var edge = era.Current ? theme.glow : done ? theme.success : theme.locked;
            var row = HuntUiKit.Rounded(_list, "Era" + era.Chapter,
                HuntUiTheme.WithAlpha(era.Current ? theme.glow : done ? theme.success : Color.white, era.Current ? 0.14f : done ? 0.08f : 0.04f),
                40f, true);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 150f;
            le.preferredHeight = 150f;
            HuntUiKit.NeonEdge(row.transform, edge, 40f, era.Current ? 1f : 0.55f);

            var badge = HuntUiKit.Image(row.transform, "Num", HuntUiSprites.Circle,
                era.Current ? theme.glow : done ? theme.deep : theme.deep);
            HuntUiKit.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(96f, 96f), new Vector2(70f, 0f));
            HuntUiKit.Stretch(HuntUiKit.Image(badge.transform, "Edge", HuntUiSprites.Ring, edge).rectTransform);
            var num = HuntUiKit.Label(badge.transform, (era.Chapter + 1).ToString(), 44,
                era.Current ? theme.deep : theme.ink, TextAnchor.MiddleCenter, false, FontStyle.Bold, true);

            var name = HuntUiKit.Label(row.transform, HuntMapScreen.ChapterName(era.Chapter).ToUpperInvariant(), 34,
                era.Current ? theme.accent : theme.ink, TextAnchor.LowerLeft, false, FontStyle.Bold, true);
            Anchor(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(140f, 0f), new Vector2(-150f, -18f));
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sub = HuntUiKit.Label(row.transform, HuntMapScreen.EraSubtitle(era.Chapter), 30, theme.inkSoft, TextAnchor.UpperLeft,
                false, FontStyle.Normal);
            Anchor(sub.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(140f, 18f), new Vector2(-150f, 0f));
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;

            if (!era.Unlocked)
            {
                var lockIcon = HuntUiKit.Image(row.transform, "Lock", NixinIcons.Load(NixinIcons.Lock), theme.inkSoft);
                lockIcon.preserveAspect = true;
                HuntUiKit.Place(lockIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(50f, 50f), new Vector2(-62f, 0f));
            }
            else if (done)
            {
                var tick = HuntUiKit.Image(row.transform, "Tick", NixinIcons.Load(NixinIcons.Check), theme.success);
                tick.preserveAspect = true;
                HuntUiKit.Place(tick.rectTransform, new Vector2(1f, 0.5f), new Vector2(54f, 54f), new Vector2(-62f, 0f));
            }
            else
            {
                var prog = HuntUiKit.Label(row.transform, era.Cleared + "/" + era.Count, 34, theme.glow, TextAnchor.MiddleRight,
                    false, FontStyle.Bold, true);
                Anchor(prog.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-190f, 0f), new Vector2(-30f, 0f));
            }

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row;
            button.transition = Selectable.Transition.None;
            row.gameObject.AddComponent<HuntButtonJuice>();
            var chapter = era.Chapter;
            button.onClick.AddListener(() =>
            {
                Close();
                _onPick?.Invoke(chapter);
            });
            _ = num;
        }
    }
}
