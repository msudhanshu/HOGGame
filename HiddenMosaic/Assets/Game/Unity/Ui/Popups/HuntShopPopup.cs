using System;
using System.Collections.Generic;
using Game.Core;
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

        const int TabHints = 0;
        const int TabFree = 1;
        const int TabLooks = 2;
        static readonly string[] TabNames = { "HINTS", "FREE", "LOOKS" };

        readonly List<GameObject>[] _tabObjects = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        readonly Image[] _tabFaces = new Image[3];
        readonly Text[] _tabLabels = new Text[3];
        int _buildTab;
        int _tab;

        protected override void BuildContent(RectTransform card)
        {
            HuntUiKit.Ribbon(card, "SHOP", Theme.accent, 600f);

            var coins = HuntCurrencyPill.Build(card, HuntCurrencies.Coins, null, 380f);
            HuntUiKit.Place((RectTransform)coins.transform, new Vector2(0.5f, 1f), new Vector2(380f, 92f), new Vector2(0f, -140f));

            BuildTabs(card);
            BuildScroll(card);

            // ---- HINTS: packs of 3, bought with coins ----
            _buildTab = TabHints;
            Section("HINT PACKS");
            var tuning = HuntWallet.Tuning;
            for (var i = 0; i < HuntHints.All.Length; i++)
            {
                var kind = HuntHints.All[i];
                var price = HuntHints.PackPrice(kind, tuning);
                var row = Item(HuntHints.Name(kind, tuning) + "  ×" + HuntHints.PackSize,
                    HuntHints.Description(kind, tuning),
                    HuntHintDock.GlyphOf(kind), Theme.accent, HuntUiKit.CoinSprite, () => price.ToString(), () => true, null, () => BuyPack(kind));
                row.IconBg.color = HuntUiTheme.WithAlpha(HuntHintDock.ColorOf(kind), 0.2f);
                row.Icon.color = HuntHintDock.ColorOf(kind);
                row.Buy.SetColor(Theme.accent);
            }

            // ---- FREE: rewarded videos ----
            _buildTab = TabFree;
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
            Item("Free Lock-on", "Watch a short video", HuntHintDock.GlyphOf(Game.Core.HuntHintKind.LockOn), Theme.info,
                NixinIcons.Load(NixinIcons.Play),
                () => "+1", () => Monetization.IsAvailable(HuntPlacements.FreeHint), null,
                () => Monetization.Request(HuntPlacements.FreeHint, granted =>
                {
                    if (granted)
                        HuntAnalytics.CurrencyEarn(HuntCurrencies.Hints, 1, "free_hint_ad");
                    Refresh();
                }));

            // ---- LOOKS: arena backgrounds ----
            _buildTab = TabLooks;
            Section("BACKGROUNDS");
            BackgroundRow(null);
            for (var i = 0; i < HuntBackgrounds.All.Count; i++)
                BackgroundRow(HuntBackgrounds.All[i]);

            CloseButton(card, () => Dismiss(_p?.OnClosed));
            SelectTab(TabHints);
        }

        void BuyPack(Game.Core.HuntHintKind kind)
        {
            var price = HuntHints.PackPrice(kind, HuntWallet.Tuning);
            if (!HuntWallet.TrySpend(HuntCurrencies.Coins, price, "hint_pack_" + kind))
            {
                HuntJams.NotEnoughCoins(price, null);
                return;
            }

            HuntWallet.Earn(HuntHintStock.Currency(kind), HuntHints.PackSize, "shop_pack");
            HuntAudio.Sfx(Nixin.Audio.NixinAudio.Win);
            HuntHaptics.Light();
            HuntToast.Show("+" + HuntHints.PackSize + " " + HuntHints.Name(kind, HuntWallet.Tuning), Theme.success);
            Refresh();
        }

        void BuildTabs(RectTransform card)
        {
            var row = HuntUiKit.Rect("Tabs", card);
            HuntUiKit.Place(row, new Vector2(0.5f, 1f), new Vector2(860f, 100f), new Vector2(0f, -250f), new Vector2(0.5f, 1f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            for (var i = 0; i < TabNames.Length; i++)
            {
                var index = i;
                var face = HuntUiKit.Rounded(row, "Tab" + i, Theme.deep, 46f, true);
                HuntUiKit.NeonEdge(face.transform, Theme.glow, 46f, 0.6f);
                _tabFaces[i] = face;
                _tabLabels[i] = HuntUiKit.Label(face.transform, TabNames[i], 38, Theme.ink, TextAnchor.MiddleCenter, false,
                    FontStyle.Bold, true);
                var button = face.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.transition = Selectable.Transition.None;
                face.gameObject.AddComponent<HuntButtonJuice>();
                button.onClick.AddListener(() => SelectTab(index));
            }
        }

        void SelectTab(int tab)
        {
            _tab = tab;
            for (var t = 0; t < _tabObjects.Length; t++)
                for (var i = 0; i < _tabObjects[t].Count; i++)
                    if (_tabObjects[t][i] != null)
                        _tabObjects[t][i].SetActive(t == tab);
            for (var i = 0; i < _tabFaces.Length; i++)
            {
                var on = i == tab;
                _tabFaces[i].color = on ? Theme.glow : Theme.deep;
                _tabLabels[i].color = on ? Theme.deep : Theme.ink;
            }

            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f;
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
            scrollRt.offsetMax = new Vector2(-24f, -380f);

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
            var t = HuntUiKit.Label(_content, title, 36, Theme.inkSoft, TextAnchor.LowerLeft, false, FontStyle.Bold, true);
            t.rectTransform.sizeDelta = new Vector2(860f, 70f);
            AddLayout(t.rectTransform, 70f);
            _tabObjects[_buildTab].Add(t.gameObject);
        }

        Row Item(string title, string subtitle, Sprite icon, Color buyColor, Sprite buyIcon, Func<string> price,
            Func<bool> available, Func<bool> owned, Action onBuy)
        {
            var row = HuntUiKit.Rounded(_content, title + "Row", Theme.panelAlt, 40f);
            row.rectTransform.sizeDelta = new Vector2(860f, 170f);
            AddLayout(row.rectTransform, 170f);

            var iconBg = HuntUiKit.Image(row.transform, "IconBg", HuntUiSprites.Circle, Theme.deep);
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

            _tabObjects[_buildTab].Add(row.gameObject);
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
