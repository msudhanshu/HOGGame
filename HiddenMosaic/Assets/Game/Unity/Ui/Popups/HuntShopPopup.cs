using System;
using System.Collections.Generic;
using Nixin.Game.Core;
using Nixin.Icons;
using Nixin.Monetization;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    public sealed class HuntShopPayload
    {
        public string From;
        public Action OnClosed;
    }

    /// <summary>Shop: free coins (ad), hints for coins, coin/hint packs (IAP), premium unlocks,
    /// restore. Reachable from anywhere including mid-level (opens over the HUD).</summary>
    public sealed class HuntShopPopup : HuntPopupBase<HuntShopPayload>
    {
        sealed class Row
        {
            public HuntKitButton Buy;
            public Func<bool> Available;
            public Func<string> Price;
            public Func<bool> Owned;
            /// <summary>Overrides label/enabled when set (background rows: price / USE / IN USE).</summary>
            public Func<KeyValuePair<string, bool>> State;
            public Image IconBg;
            public Image Icon;
        }

        HuntShopPayload _p;
        RectTransform _content;
        ScrollRect _scroll;
        readonly List<Row> _rows = new List<Row>();

        protected override Vector2 CardSize => new Vector2(960f, 1640f);

        protected override void BuildContent(RectTransform card)
        {
            HuntUiKit.Ribbon(card, "Shop", Theme.accent, 600f);

            var coins = HuntCurrencyPill.Build(card, HuntCurrencies.Coins, null, 320f);
            HuntUiKit.Place((RectTransform)coins.transform, new Vector2(0.5f, 1f), new Vector2(320f, 92f), new Vector2(-180f, -140f));
            var hints = HuntCurrencyPill.Build(card, HuntCurrencies.Hints, null, 280f);
            HuntUiKit.Place((RectTransform)hints.transform, new Vector2(0.5f, 1f), new Vector2(280f, 92f), new Vector2(190f, -140f));
            foreach (var bg in new[] { coins.transform.Find("Bg"), hints.transform.Find("Bg") })
                bg.GetComponent<Image>().color = HuntUiTheme.WithAlpha(Theme.ink, 0.8f);

            BuildScroll(card);

            Section("FREE");
            Item("Free Coins", "Watch a short video", HuntUiKit.CoinSprite, Theme.info, NixinIcons.Load(NixinIcons.Play),
                () => "+" + HuntJams.FreeCoinsReward,
                () => Monetization.IsAvailable(HuntPlacements.FreeCoins), null,
                () => Monetization.Request(HuntPlacements.FreeCoins, granted =>
                {
                    if (granted)
                        HuntAnalytics.CurrencyEarn(HuntCurrencies.Coins, HuntJams.FreeCoinsReward, "free_coins_ad");
                    Refresh();
                }));

            // Coin->hint conversion and real-money rows are hidden (economy control / IAP not shipped).
            Item("Free Hint", "Watch a short video", HuntUiKit.HintGlyph, Theme.info, NixinIcons.Load(NixinIcons.Play),
                () => "+1", () => Monetization.IsAvailable(HuntPlacements.FreeHint), null,
                () => Monetization.Request(HuntPlacements.FreeHint, granted =>
                {
                    if (granted)
                        HuntAnalytics.CurrencyEarn(HuntCurrencies.Hints, 1, "free_hint_ad");
                    Refresh();
                }));

            Section("BACKGROUNDS");
            BackgroundRow(null);
            for (var i = 0; i < HuntBackgrounds.All.Count; i++)
                BackgroundRow(HuntBackgrounds.All[i]);

            CloseButton(card, () => Dismiss(_p?.OnClosed));
        }

        /// <summary>def == null is the free default look (original game-scene and home colours).</summary>
        void BackgroundRow(HuntBackgrounds.Def def)
        {
            var row = Item(def == null ? "Default" : def.Name, def == null ? "Original look" : "Game background",
                HuntUiSprites.Star, Theme.accent, null, () => def == null ? "USE" : def.Price.ToString(),
                () => true, null, () =>
                {
                    if (HuntBackgrounds.IsOwned(def))
                    {
                        HuntBackgrounds.Select(def);
                    }
                    else if (HuntBackgrounds.TryBuy(def))
                    {
                        HuntAudio.Sfx(Nixin.Audio.NixinAudio.Win);
                        HuntToast.Show(def.Name + " unlocked!", Theme.success);
                    }
                    else
                    {
                        HuntJams.NotEnoughCoins(def.Price, null);
                    }

                    Refresh();
                });
            row.IconBg.color = def == null ? Theme.bgTop : def.Top;
            row.Icon.color = Color.white;
            row.State = () =>
            {
                if (!HuntBackgrounds.IsOwned(def))
                    return new KeyValuePair<string, bool>(def.Price + " coins", true);
                return HuntBackgrounds.IsSelected(def)
                    ? new KeyValuePair<string, bool>("IN USE", false)
                    : new KeyValuePair<string, bool>("USE", true);
            };
        }

        void BuildScroll(RectTransform card)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(card, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            HuntUiKit.Stretch(scrollRt);
            scrollRt.offsetMin = new Vector2(24f, 30f);
            scrollRt.offsetMax = new Vector2(-24f, -220f);

            var viewport = HuntUiKit.Stretch(HuntUiKit.Rect("Viewport", scrollRt));
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            _content = HuntUiKit.Rect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.padding = new RectOffset(10, 10, 10, 40);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.scrollSensitivity = 40f;
        }

        static void AddLayout(RectTransform rt, float height)
        {
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
        }

        void Section(string title)
        {
            var t = HuntUiKit.Label(_content, title, 40, Theme.inkSoft, TextAnchor.LowerLeft);
            t.rectTransform.sizeDelta = new Vector2(860f, 70f);
            AddLayout(t.rectTransform, 70f);
        }

        Row Item(string title, string subtitle, Sprite icon, Color buyColor, Sprite buyIcon, Func<string> price,
            Func<bool> available, Func<bool> owned, Action onBuy)
        {
            var row = HuntUiKit.Rounded(_content, title + "Row", Color.white, 40f);
            row.rectTransform.sizeDelta = new Vector2(860f, 170f);
            AddLayout(row.rectTransform, 170f);

            var iconBg = HuntUiKit.Image(row.transform, "IconBg", HuntUiSprites.Circle, Theme.panelAlt);
            HuntUiKit.Place(iconBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(124f, 124f), new Vector2(90f, 0f));
            var iconImg = HuntUiKit.Icon(iconBg.transform, icon, icon == HuntUiKit.CoinSprite ? Color.white : Theme.primary,
                new Vector2(84f, 84f));
            iconImg.rectTransform.anchoredPosition = Vector2.zero;

            var titleText = HuntUiKit.Label(row.transform, title, 44, Theme.ink, TextAnchor.LowerLeft);
            titleText.rectTransform.offsetMin = new Vector2(180f, 86f);
            titleText.rectTransform.offsetMax = new Vector2(-300f, -20f);
            var sub = HuntUiKit.Label(row.transform, subtitle, 32, Theme.inkSoft, TextAnchor.UpperLeft, false, FontStyle.Normal);
            sub.rectTransform.offsetMin = new Vector2(180f, 20f);
            sub.rectTransform.offsetMax = new Vector2(-300f, -90f);

            var buy = HuntUiKit.Button(row.transform, "BUY", buyColor, new Vector2(260f, 110f), onBuy, buyIcon, 40);
            HuntUiKit.Place(buy.Root, new Vector2(1f, 0.5f), new Vector2(260f, 110f), new Vector2(-150f, 6f));

            var r = new Row { Buy = buy, Available = available, Price = price, Owned = owned, IconBg = iconBg, Icon = iconImg };
            _rows.Add(r);
            return r;
        }

        void Purchase(string title, string subtitle, Sprite icon, string placement, string productId, Func<bool> owned = null)
        {
            Item(title, subtitle, icon, Theme.success, null, () => PriceOf(productId),
                () => Monetization.IsAvailable(placement), owned, () => Monetization.Request(placement, granted =>
                {
                    if (granted)
                    {
                        HuntAudio.Sfx(Nixin.Audio.NixinAudio.Win);
                        HuntHaptics.Medium();
                        HuntToast.Show("Thank you!", Theme.success);
                    }

                    Refresh();
                }));
        }

        static string PriceOf(string productId)
        {
            if (GameServices.Store != null && GameServices.Store.TryGetProduct(productId, out var p)
                && !string.IsNullOrEmpty(p.LocalizedPrice))
                return p.LocalizedPrice;
            return "BUY";
        }

        void Refresh()
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                if (r.State != null)
                {
                    var st = r.State();
                    r.Buy.SetLabel(st.Key);
                    r.Buy.SetInteractable(st.Value);
                    continue;
                }

                var owned = r.Owned != null && r.Owned();
                if (owned)
                {
                    r.Buy.SetLabel("OWNED");
                    r.Buy.SetInteractable(false);
                    continue;
                }

                var available = r.Available == null || r.Available();
                r.Buy.SetLabel(available ? r.Price() : "LATER");
                r.Buy.SetInteractable(available);
            }
        }

        public override void Bind(HuntShopPayload payload)
        {
            EnsureBuilt();
            _p = payload ?? new HuntShopPayload();
            Refresh();
            _scroll.verticalNormalizedPosition = 1f;
            SetBackHandler(() => Dismiss(_p.OnClosed));
        }
    }
}
