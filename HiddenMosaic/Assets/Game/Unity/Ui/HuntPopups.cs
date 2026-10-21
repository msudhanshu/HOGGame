using System;
using System.Collections.Generic;
using Nixin.StoreEngagement;
using Nixin.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Typed openers for the code-built popups. Templates are created and registered
    /// with the shared UiNavigator lazily (same pattern as MonetizationOfferPresenter); the
    /// navigator's canvas is DontDestroyOnLoad and sorts above scene canvases.</summary>
    public static class HuntPopups
    {
        static readonly Dictionary<Type, UiView> Templates = new Dictionary<Type, UiView>();

        public static UiNavigator Navigator => Nav();

        static UiNavigator Nav()
        {
            HuntUiInput.EnsureEventSystem();
            var nav = UiNavigator.Ensure();
            nav.SetDimmerColor(new Color(0.12f, 0.02f, 0.26f, 0.62f));
            return nav;
        }

        public static void Open<TView, TPayload>(TPayload payload) where TView : HuntPopupBase<TPayload>
        {
            var nav = Nav();
            if (!Templates.TryGetValue(typeof(TView), out var view) || view == null)
            {
                var go = new GameObject(typeof(TView).Name, typeof(RectTransform));
                go.SetActive(false);
                view = go.AddComponent<TView>();
                nav.Register<TView>(UiKind.Popup, (TView)view);
                Templates[typeof(TView)] = view;
            }

            nav.Open<TView, TPayload>(payload);
        }

        public static bool AnyOpen => NixinStoreEngagement.IsPopupOpen
            || (UiNavigator.Current != null && UiNavigator.Current.IsBlocking);

        public static void CloseAll()
        {
            if (UiNavigator.Current != null)
                UiNavigator.Current.CloseAllPopups();
        }

        /// <summary>Full-screen layer above popups for confetti / flying coins / toasts.</summary>
        public static RectTransform OverlayLayer
        {
            get
            {
                var nav = Nav();
                var canvas = nav.transform.Find(UiHost.CanvasName);
                if (canvas == null)
                    return null;
                var fx = canvas.Find("HuntFx") as RectTransform;
                if (fx == null)
                {
                    fx = HuntUiKit.Stretch(HuntUiKit.Rect("HuntFx", canvas));
                    var group = fx.gameObject.AddComponent<CanvasGroup>();
                    group.blocksRaycasts = false;
                    group.interactable = false;
                }

                fx.SetAsLastSibling();
                return fx;
            }
        }

        public static void LevelIntro(HuntLevelIntroPayload p) => Open<HuntLevelIntroPopup, HuntLevelIntroPayload>(p);
        public static void Win(HuntWinPayload p) => Open<HuntWinPopup, HuntWinPayload>(p);
        public static void Fail(HuntFailPayload p) => Open<HuntFailPopup, HuntFailPayload>(p);
        public static void Jam(HuntJamPayload p) => Open<HuntJamPopup, HuntJamPayload>(p);
        public static void Pause(HuntPausePayload p) => Open<HuntPausePopup, HuntPausePayload>(p);
        public static void Chapter(HuntChapterPayload p) => Open<HuntChapterPopup, HuntChapterPayload>(p);

        public static void Settings(string from)
        {
            Open<HuntSettingsPopup, string>(from ?? "");
        }

        public static void Shop(string from, Action onClosed = null)
        {
            HuntAnalytics.ShopView(from);
            Open<HuntShopPopup, HuntShopPayload>(new HuntShopPayload { From = from, OnClosed = onClosed });
        }

        public static void ComingSoon(Game.Core.HuntNode node, Action onContinue)
        {
            Open<HuntComingSoonPopup, HuntComingSoonPayload>(new HuntComingSoonPayload { Node = node, OnContinue = onContinue });
        }
    }

    /// <summary>Short floating message pill (e.g. "Finish level 4 first").</summary>
    public static class HuntToast
    {
        public static void Show(string message, Color? color = null)
        {
            var layer = HuntPopups.OverlayLayer;
            if (layer == null)
                return;
            var pill = HuntUiKit.Rounded(layer, "Toast", color ?? new Color(0.17f, 0.07f, 0.3f, 0.92f), 44f);
            HuntUiKit.Place(pill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(820f, 120f), new Vector2(0f, -420f));
            HuntUiKit.Label(pill.transform, message, 42, Color.white, TextAnchor.MiddleCenter);
            var group = pill.gameObject.AddComponent<CanvasGroup>();
            HuntTweener.Scale(pill.transform, Vector3.one * 0.6f, Vector3.one, 0.3f, Game.Core.Ease.OutBack);
            HuntTweener.Fade(group, 1f, 0f, 0.35f, 1.6f, () => UnityEngine.Object.Destroy(pill.gameObject));
        }
    }
}
