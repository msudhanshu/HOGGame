using System;
using Game.Core;
using Nixin.Audio;
using Nixin.Icons;
using Nixin.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Code-built popup: card pops in (OutBack), scales out on Dismiss. Content is
    /// built once in BuildContent, then refreshed per open in Bind.</summary>
    public abstract class HuntPopupBase<TPayload> : UiView<TPayload>
    {
        protected RectTransform CardRoot;
        protected Image CardFace;
        CanvasGroup _group;
        bool _built;
        bool _closing;

        protected HuntUiTheme Theme => HuntUiTheme.Current;
        protected virtual Vector2 CardSize => new Vector2(900f, 1100f);
        protected virtual Color CardColor => Theme.panel;
        protected virtual string OpenSfx => NixinAudio.Pause;
        /// <summary>Edge-to-edge page instead of a floating card (fades in, no pop).</summary>
        protected virtual bool FullScreen => false;

        protected abstract void BuildContent(RectTransform card);

        protected void EnsureBuilt()
        {
            if (_built)
                return;
            _built = true;
            var rt = transform as RectTransform;
            if (rt == null)
                rt = gameObject.AddComponent<RectTransform>();
            HuntUiKit.Stretch(rt);
            _group = HuntUiKit.Group(gameObject);
            CardFace = HuntUiKit.Card(transform, CardSize, CardColor, FullScreen ? 1f : 48f);
            CardRoot = (RectTransform)CardFace.transform.parent;
            if (FullScreen)
            {
                HuntUiKit.Stretch(CardRoot);
                CardRoot.Find("Shadow").gameObject.SetActive(false);
            }
            SetCloseOnBackdrop(false);
            var content = CardFace.rectTransform;
            if (FullScreen)
            {
                // Keep the ribbon / close button clear of notches and rounded corners.
                content = HuntUiKit.Stretch(HuntUiKit.Rect("SafeArea", content));
                content.gameObject.AddComponent<SafeAreaFitter>();
            }

            BuildContent(content);
        }

        public override void OnOpened()
        {
            EnsureBuilt();
            _closing = false;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            HuntTweener.Kill(CardRoot);
            if (FullScreen)
                CardRoot.localScale = Vector3.one;
            else
                HuntTweener.Scale(CardRoot, Vector3.one * 0.55f, Vector3.one, 0.42f, Ease.OutBack);
            HuntTweener.Fade(_group, 0f, 1f, 0.15f);
            HuntAudio.Sfx(OpenSfx);
            HuntAnalytics.PopupView(GetType().Name);
            OnShown();
        }

        protected virtual void OnShown()
        {
        }

        /// <summary>Animate out, close this popup, then run after (open the next thing there).</summary>
        protected void Dismiss(Action after = null)
        {
            if (_closing)
                return;
            _closing = true;
            _group.interactable = false;
            HuntTweener.Kill(CardRoot);
            if (!FullScreen)
                HuntTweener.Scale(CardRoot, CardRoot.localScale, Vector3.one * 0.7f, 0.16f, Ease.InBack);
            HuntTweener.Fade(_group, 1f, 0f, 0.16f, 0f, () =>
            {
                if (IsOpen && UiNavigator.Current != null)
                    UiNavigator.Current.Close();
                after?.Invoke();
            });
        }

        /// <summary>Wraps an action so the popup closes first.</summary>
        protected Action Then(Action action) => () => Dismiss(action);

        protected HuntKitButton CloseButton(RectTransform card, Action onClose)
        {
            var b = HuntUiKit.IconButton(card, NixinIcons.Load(NixinIcons.Close), Theme.danger, 96f, onClose);
            HuntUiKit.Place(b.Root, new Vector2(1f, 1f), new Vector2(96f, 96f), new Vector2(-6f, -6f));
            b.Button.GetComponent<HuntButtonJuice>().sfx = NixinAudio.Cancel;
            return b;
        }

        protected Text Body(RectTransform card, string text, float top, float height, int size = 44)
        {
            var t = HuntUiKit.Label(card, text, size, Theme.ink, TextAnchor.MiddleCenter, false, FontStyle.Normal);
            t.rectTransform.anchorMin = new Vector2(0f, 1f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.rectTransform.offsetMin = new Vector2(60f, 0f);
            t.rectTransform.offsetMax = new Vector2(-60f, 0f);
            t.rectTransform.anchoredPosition = new Vector2(0f, -top);
            t.rectTransform.sizeDelta = new Vector2(-120f, height);
            return t;
        }

        /// <summary>Places a widget at a vertical offset from the card's top centre.</summary>
        protected static RectTransform AtTop(RectTransform rect, float top, Vector2 size)
        {
            HuntUiKit.Place(rect, new Vector2(0.5f, 1f), size, new Vector2(0f, -top), new Vector2(0.5f, 1f));
            return rect;
        }

        /// <summary>Staggered pop-in for a list of widgets.</summary>
        protected static void Stagger(float startDelay, params Transform[] items)
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null || !items[i].gameObject.activeSelf)
                    continue;
                items[i].localScale = Vector3.zero;
                HuntTweener.Scale(items[i], Vector3.zero, Vector3.one, 0.35f, Ease.OutBack, startDelay + i * 0.07f);
            }
        }
    }
}
