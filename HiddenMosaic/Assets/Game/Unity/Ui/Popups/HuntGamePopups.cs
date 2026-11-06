using System;
using System.Collections.Generic;
using Game.Core;
using Nixin.Audio;
using Nixin.Icons;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    // ───────────────────────────── Level intro / difficulty jump ─────────────────────────────

    public sealed class HuntLevelIntroPayload
    {
        public HuntNode Node;
        public string Title;
        public int Toughness;
        public int Targets;
        public float TimeLimit;
        public int BestStars;
        /// <summary>Toughness went up versus the previous level → "DIFFICULTY UP!" tag.</summary>
        public bool DifficultyUp;
        public string EraName;
        public int Waves;
        public int WaveSize;
        /// <summary>Silhouette of the finished picture (mosaic levels).</summary>
        public Sprite Silhouette;
        /// <summary>Real colour icons of the first wave's targets.</summary>
        public List<Sprite> TargetIcons = new List<Sprite>();
        /// <summary>Filled by the popup before OnPlay: hints the player took along (applied at the start).</summary>
        public List<HuntHintKind> ChosenBoosts = new List<HuntHintKind>();
        /// <summary>Hints offered as "take with you" boosts. Empty = no boost row.</summary>
        public HuntHintKind[] BoostChoices = { HuntHintKind.FreezeTime, HuntHintKind.LockOn, HuntHintKind.Radar };
        public Action OnPlay;
        /// <summary>Null = no home button; START stays full width.</summary>
        public Action OnHome;
        /// <summary>Null = no close button (in-level intro); back key then plays.</summary>
        public Action OnClose;
    }

    public sealed class HuntLevelIntroPopup : HuntPopupBase<HuntLevelIntroPayload>
    {
        sealed class BoostTile
        {
            public HuntHintKind Kind;
            public Image Face;
            public Image Edge;
            public RectTransform Check;
            public Text Info;
            public bool Chosen;
        }

        HuntLevelIntroPayload _p;
        Text _era;
        Text _title;
        Text _tagline;
        RectTransform _jump;
        Image[] _stars;
        RectTransform _starsRoot;
        Image _previewImage;
        Text _previewCaption;
        Text _diffName;
        Image[] _diffBars;
        Text _timeValue;
        Text _waveValue;
        Text _targetsLabel;
        RectTransform _targetRow;
        RectTransform _boostRow;
        Text _boostLabel;
        readonly List<BoostTile> _boosts = new List<BoostTile>();
        HuntKitButton _play;
        HuntKitButton _home;
        HuntKitButton _close;

        protected override bool BottomSheet => true;
        protected override Vector2 CardSize => new Vector2(0f, 1500f);

        static RectTransform Put(RectTransform rt, float x, float top, float w, float h)
        {
            HuntUiKit.Place(rt, new Vector2(0f, 1f), new Vector2(w, h), new Vector2(x, -top), new Vector2(0f, 1f));
            return rt;
        }

        static Text Caption(RectTransform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            return HuntUiKit.Label(parent, text, size, color, anchor, false, FontStyle.Bold, true);
        }

        protected override void BuildContent(RectTransform card)
        {
            var theme = Theme;
            var grab = HuntUiKit.Rounded(card, "Grabber", new Color(1f, 1f, 1f, 0.25f), 6f);
            HuntUiKit.Place(grab.rectTransform, new Vector2(0.5f, 1f), new Vector2(110f, 10f), new Vector2(0f, -22f), new Vector2(0.5f, 1f));

            _era = Caption(card, "", 30, theme.inkSoft);
            Put(_era.rectTransform, 64f, 56f, 760f, 44f);
            _title = Caption(card, "LEVEL 1", 84, theme.ink);
            Put(_title.rectTransform, 64f, 100f, 760f, 110f);

            _stars = HuntUiKit.Stars(card, 0, 50f, 8f);
            _starsRoot = (RectTransform)_stars[0].transform.parent;
            Put(_starsRoot, 64f, 214f, _starsRoot.sizeDelta.x, _starsRoot.sizeDelta.y);

            var jumpBg = HuntUiKit.Rounded(card, "DifficultyUp", theme.danger, 30f);
            _jump = jumpBg.rectTransform;
            HuntUiKit.Place(_jump, new Vector2(1f, 1f), new Vector2(340f, 64f), new Vector2(-200f, -190f), new Vector2(1f, 1f));
            HuntUiKit.Label(_jump, "DIFFICULTY UP", 32, Color.white, TextAnchor.MiddleCenter, true, FontStyle.Bold, true);
            _jump.gameObject.AddComponent<HuntIdlePulse>().amount = 0.05f;

            _close = CloseButton(card, () => Dismiss(_p?.OnClose));
            HuntUiKit.Place(_close.Root, new Vector2(1f, 1f), new Vector2(104f, 104f), new Vector2(-70f, -70f));

            // preview (left) + stats (right)
            var preview = HuntUiKit.Rounded(card, "Preview", theme.deep, 40f, true);
            Put(preview.rectTransform, 64f, 296f, 360f, 360f);
            HuntUiKit.NeonEdge(preview.transform, theme.glow, 40f, 0.85f);
            var glow = HuntUiKit.Image(preview.transform, "Glow", HuntUiSprites.Radial, HuntUiTheme.WithAlpha(theme.bgGlow, 0.5f));
            HuntUiKit.Stretch(glow.rectTransform);
            _previewImage = HuntUiKit.Image(preview.transform, "Silhouette", null, theme.glow);
            _previewImage.preserveAspect = true;
            HuntUiKit.Stretch(_previewImage.rectTransform, 40f);
            _previewCaption = Caption(preview.rectTransform, "TARGET SHAPE", 22, theme.inkSoft);
            _previewCaption.rectTransform.anchorMin = new Vector2(0f, 0f);
            _previewCaption.rectTransform.anchorMax = new Vector2(1f, 0f);
            _previewCaption.rectTransform.offsetMin = new Vector2(24f, 14f);
            _previewCaption.rectTransform.offsetMax = new Vector2(-24f, 56f);

            var diff = HuntUiKit.Rounded(card, "Difficulty", new Color(1f, 1f, 1f, 0.06f), 36f);
            Put(diff.rectTransform, 454f, 296f, 562f, 220f);
            HuntUiKit.NeonEdge(diff.transform, Color.white, 36f, 0.12f);
            var diffLabel = Caption(diff.rectTransform, "DIFFICULTY", 24, theme.inkSoft);
            Put(diffLabel.rectTransform, 28f, 18f, 400f, 36f);
            _diffName = Caption(diff.rectTransform, "EASY", 54, theme.accent);
            Put(_diffName.rectTransform, 28f, 62f, 500f, 76f);
            var bars = HuntUiKit.Rect("Bars", diff.rectTransform);
            Put(bars, 28f, 158f, 506f, 16f);
            _diffBars = new Image[5];
            for (var i = 0; i < 5; i++)
            {
                var bar = HuntUiKit.Rounded(bars, "Bar" + i, theme.locked, 8f);
                bar.rectTransform.anchorMin = new Vector2(i / 5f, 0f);
                bar.rectTransform.anchorMax = new Vector2((i + 1) / 5f, 1f);
                bar.rectTransform.offsetMin = new Vector2(i == 0 ? 0f : 4f, 0f);
                bar.rectTransform.offsetMax = new Vector2(i == 4 ? 0f : -4f, 0f);
                _diffBars[i] = bar;
            }

            _timeValue = StatBox(card, 454f, "TIME");
            _waveValue = StatBox(card, 454f + 292f, "WAVES");

            // targets
            _targetsLabel = Caption(card, "TARGETS TO FIND", 28, theme.inkSoft);
            Put(_targetsLabel.rectTransform, 64f, 692f, 900f, 40f);
            _targetRow = HuntUiKit.Rect("Targets", card);
            Put(_targetRow, 64f, 740f, 952f, 124f);
            var tl = _targetRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tl.spacing = 16f;
            tl.childAlignment = TextAnchor.MiddleLeft;
            tl.childControlWidth = false;
            tl.childControlHeight = false;
            tl.childForceExpandWidth = false;
            tl.childForceExpandHeight = false;

            // boosts
            _boostLabel = Caption(card, "TAKE WITH YOU", 28, theme.inkSoft);
            Put(_boostLabel.rectTransform, 64f, 896f, 900f, 40f);
            _boostRow = HuntUiKit.Rect("Boosts", card);
            Put(_boostRow, 64f, 944f, 952f, 210f);
            var bl = _boostRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bl.spacing = 20f;
            bl.childControlWidth = true;
            bl.childControlHeight = true;
            bl.childForceExpandWidth = true;
            bl.childForceExpandHeight = true;

            _play = HuntUiKit.Button(card, "START HUNT", theme.glow, new Vector2(700f, 150f), () => StartHunt(), null, 60);
            HuntUiKit.Place(_play.Root, new Vector2(0.5f, 0f), new Vector2(700f, 150f), new Vector2(60f, 190f));
            _play.Root.gameObject.AddComponent<HuntIdlePulse>().amount = 0.025f;
            _play.Button.GetComponent<HuntButtonJuice>().sfx = NixinAudio.Confirm;

            _home = HuntUiKit.IconButton(card, NixinIcons.Load(NixinIcons.Home), theme.panelAlt, 130f, () => Dismiss(_p?.OnHome));
            HuntUiKit.Place(_home.Root, new Vector2(0.5f, 0f), new Vector2(130f, 130f), new Vector2(-380f, 200f));
        }

        Text StatBox(RectTransform card, float x, string label)
        {
            var theme = Theme;
            var box = HuntUiKit.Rounded(card, label + "Box", new Color(1f, 1f, 1f, 0.06f), 36f);
            Put(box.rectTransform, x, 536f, 270f, 120f);
            HuntUiKit.NeonEdge(box.transform, Color.white, 36f, 0.12f);
            var l = Caption(box.rectTransform, label, 22, theme.inkSoft, TextAnchor.UpperCenter);
            l.rectTransform.offsetMin = new Vector2(0f, 0f);
            l.rectTransform.offsetMax = new Vector2(0f, -14f);
            var v = HuntUiKit.Label(box.rectTransform, "-", 46, theme.ink, TextAnchor.LowerCenter, false, FontStyle.Bold, true);
            v.rectTransform.offsetMin = new Vector2(0f, 12f);
            return v;
        }

        public override void Bind(HuntLevelIntroPayload payload)
        {
            EnsureBuilt();
            _p = payload ?? new HuntLevelIntroPayload();
            var theme = Theme;
            var hog = _p.Node != null && _p.Node.Kind == HuntNodeKind.ImageHog;
            var number = _p.Node != null ? _p.Node.Number : 1;
            var color = hog ? theme.accent : theme.ToughnessColor(_p.Toughness);

            var eraIndex = _p.Node != null ? _p.Node.Chapter + 1 : 1;
            _era.text = ("ERA " + eraIndex + (string.IsNullOrEmpty(_p.EraName) ? "" : "  ·  " + _p.EraName)).ToUpperInvariant();
            _title.text = hog ? "PHOTO HUNT" : "LEVEL " + number;
            for (var i = 0; i < _stars.Length; i++)
                _stars[i].color = i < _p.BestStars ? theme.accent : new Color(1f, 1f, 1f, 0.18f);
            _jump.gameObject.SetActive(_p.DifficultyUp);

            if (_p.Silhouette != null)
            {
                _previewImage.sprite = _p.Silhouette;
                _previewImage.gameObject.SetActive(true);
                _previewImage.color = theme.glow;
            }
            else
            {
                _previewImage.sprite = HuntUiKit.HintGlyph;
                _previewImage.gameObject.SetActive(true);
                _previewImage.color = HuntUiTheme.WithAlpha(theme.glow, 0.7f);
            }

            _previewCaption.text = hog ? "REAL PHOTO" : "TARGET SHAPE";
            _diffName.text = HuntEconomyRules.ToughnessName(_p.Toughness).ToUpperInvariant();
            _diffName.color = color;
            for (var i = 0; i < _diffBars.Length; i++)
                _diffBars[i].color = i < Mathf.Clamp(_p.Toughness, 1, 5) ? color : new Color(1f, 1f, 1f, 0.15f);
            var secs = Mathf.CeilToInt(_p.TimeLimit);
            _timeValue.text = (secs / 60) + ":" + (secs % 60).ToString("00");
            _waveValue.text = _p.Waves > 0 ? _p.Waves.ToString() : "1";

            var perWave = _p.WaveSize > 0 ? _p.WaveSize : _p.Targets;
            _targetsLabel.text = hog ? "FIND THE HIDDEN OBJECTS"
                : "TARGETS TO FIND" + (perWave > 0 ? "  ·  " + perWave + (_p.Waves > 1 ? " PER WAVE" : "") : "");
            FillTargets();
            FillBoosts();

            var hasHome = _p.OnHome != null;
            _home.gameObject.SetActive(hasHome);
            _play.Root.anchoredPosition = new Vector2(hasHome ? 60f : 0f, 190f);
            _play.Root.sizeDelta = new Vector2(hasHome ? 700f : 880f, 150f);
            _close.gameObject.SetActive(_p.OnClose != null);
            SetBackHandler(() => Dismiss(_p.OnClose ?? (Action)StartHuntAction));
        }

        void StartHuntAction() => StartHunt();

        void FillTargets()
        {
            for (var i = _targetRow.childCount - 1; i >= 0; i--)
                Destroy(_targetRow.GetChild(i).gameObject);
            var theme = Theme;
            var count = _p.TargetIcons != null ? _p.TargetIcons.Count : 0;
            var size = count <= 6 ? 112f : Mathf.Max(72f, 112f - (count - 6) * 10f);
            for (var i = 0; i < count; i++)
            {
                var chip = HuntUiKit.Rounded(_targetRow, "Target" + i, HuntUiTheme.WithAlpha(theme.glow, 0.1f), 30f);
                chip.rectTransform.sizeDelta = new Vector2(size, size);
                HuntUiKit.NeonEdge(chip.transform, theme.glow, 30f, 0.8f);
                if (_p.TargetIcons[i] != null)
                {
                    var icon = HuntUiKit.Image(chip.transform, "Icon", _p.TargetIcons[i], Color.white);
                    icon.preserveAspect = true;
                    HuntUiKit.Stretch(icon.rectTransform, size * 0.12f);
                }
            }

            _targetsLabel.gameObject.SetActive(count > 0 || true);
        }

        void FillBoosts()
        {
            for (var i = _boostRow.childCount - 1; i >= 0; i--)
                Destroy(_boostRow.GetChild(i).gameObject);
            _boosts.Clear();
            var choices = _p.BoostChoices;
            var any = choices != null && choices.Length > 0;
            _boostLabel.gameObject.SetActive(any);
            _boostRow.gameObject.SetActive(any);
            if (!any)
                return;
            for (var i = 0; i < choices.Length; i++)
                _boosts.Add(BuildBoost(choices[i]));
            RefreshBoosts();
        }

        BoostTile BuildBoost(HuntHintKind kind)
        {
            var theme = Theme;
            var color = HuntHintDock.ColorOf(kind);
            var tile = new BoostTile { Kind = kind };
            var face = HuntUiKit.Rounded(_boostRow, kind + "Boost", HuntUiTheme.WithAlpha(color, 0.08f), 40f, true);
            tile.Face = face;
            tile.Edge = HuntUiKit.NeonEdge(face.transform, color, 40f, 0.55f);
            var glyph = HuntUiKit.Icon(face.transform, HuntHintDock.GlyphOf(kind), color, new Vector2(84f, 84f));
            glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.68f);
            glyph.rectTransform.anchoredPosition = Vector2.zero;
            var name = Caption(face.rectTransform, HuntHints.Name(kind, HuntWallet.Tuning), 28, Color.white, TextAnchor.MiddleCenter);
            name.rectTransform.anchorMin = new Vector2(0f, 0.22f);
            name.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
            tile.Info = Caption(face.rectTransform, "", 26, theme.accent, TextAnchor.MiddleCenter);
            tile.Info.rectTransform.anchorMin = new Vector2(0f, 0.02f);
            tile.Info.rectTransform.anchorMax = new Vector2(1f, 0.22f);
            tile.Info.rectTransform.offsetMin = tile.Info.rectTransform.offsetMax = Vector2.zero;

            var check = HuntUiKit.Image(face.transform, "Check", HuntUiSprites.Circle, color);
            HuntUiKit.Place(check.rectTransform, new Vector2(1f, 1f), new Vector2(46f, 46f), new Vector2(-12f, -12f), new Vector2(1f, 1f));
            var tick = HuntUiKit.Image(check.transform, "Tick", NixinIcons.Load(NixinIcons.Check), theme.deep);
            tick.preserveAspect = true;
            HuntUiKit.Stretch(tick.rectTransform, 10f);
            tile.Check = check.rectTransform;

            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            face.gameObject.AddComponent<HuntButtonJuice>();
            button.onClick.AddListener(() => ToggleBoost(tile));
            return tile;
        }

        void ToggleBoost(BoostTile tile)
        {
            if (!tile.Chosen)
            {
                var owned = HuntHintStock.Count(tile.Kind) > 0;
                if (!owned && !HuntWallet.CanAfford(HuntCurrencies.Coins, HuntHintStock.Price(tile.Kind)))
                {
                    HuntToast.Show("Not enough coins");
                    return;
                }
            }

            tile.Chosen = !tile.Chosen;
            RefreshBoosts();
        }

        void RefreshBoosts()
        {
            var theme = Theme;
            for (var i = 0; i < _boosts.Count; i++)
            {
                var t = _boosts[i];
                var color = HuntHintDock.ColorOf(t.Kind);
                var count = HuntHintStock.Count(t.Kind);
                t.Info.text = count > 0 ? "OWNED  ×" + count : HuntHintStock.Price(t.Kind) + " COINS";
                t.Info.color = count > 0 ? theme.inkSoft : theme.accent;
                t.Check.gameObject.SetActive(t.Chosen);
                t.Face.color = HuntUiTheme.WithAlpha(color, t.Chosen ? 0.22f : 0.08f);
                t.Edge.color = HuntUiTheme.WithAlpha(color, t.Chosen ? 1f : 0.55f);
            }
        }

        void StartHunt()
        {
            _p.ChosenBoosts = new List<HuntHintKind>();
            for (var i = 0; i < _boosts.Count; i++)
                if (_boosts[i].Chosen)
                    _p.ChosenBoosts.Add(_boosts[i].Kind);
            Dismiss(_p.OnPlay);
        }

        protected override void OnShown()
        {
            Stagger(0.2f, _targetRow, _boostRow, _play.Root);
            if (_p.DifficultyUp)
            {
                _jump.localScale = Vector3.zero;
                HuntTweener.Scale(_jump, Vector3.one * 2.2f, Vector3.one, 0.5f, Ease.OutBack, 0.45f);
                HuntTweener.Delay(_jump, 0.5f, () =>
                {
                    HuntAudio.Sfx(NixinAudio.Error, 1.2f);
                    HuntHaptics.Medium();
                });
            }
        }
    }

    // ───────────────────────────── Win ─────────────────────────────

    public sealed class HuntWinPayload
    {
        public string Title = "TIMELINE SECURED";
        public int Stars;
        /// <summary>Already credited; caller holds it on the coin pill (HuntCurrencyPill.HoldOn) so it can fly in.</summary>
        public int Coins;
        public bool FirstClear;
        public bool CanDouble;
        /// <summary>Plays the ×2 rewarded ad; calls back with extra coins credited + held (0 = none).</summary>
        public Action<Action<int>> RequestDouble;
        public string NextLabel = "NEXT";
        public Action OnNext;
        public Action OnHome;
    }

    public sealed class HuntWinPopup : HuntPopupBase<HuntWinPayload>
    {
        HuntWinPayload _p;
        Text _ribbon;
        Image[] _stars;
        RectTransform _starsRoot;
        RectTransform _coinRow;
        RectTransform _coinIcon;
        HuntCounterText _coins;
        Text _subtitle;
        HuntKitButton _double;
        HuntKitButton _next;
        HuntKitButton _home;
        int _pendingFly;

        protected override Vector2 CardSize => new Vector2(880f, 1060f);
        protected override string OpenSfx => NixinAudio.Win;

        protected override void BuildContent(RectTransform card)
        {
            _ribbon = HuntUiKit.Ribbon(card, "TIMELINE SECURED", Theme.success, 860f);

            var glow = HuntUiKit.Image(card, "Glow", HuntUiSprites.Star, HuntUiTheme.WithAlpha(Theme.accent, 0.18f));
            AtTop(glow.rectTransform, 90f, new Vector2(520f, 520f));
            glow.gameObject.AddComponent<HuntSpin>().degreesPerSecond = 18f;

            _stars = HuntUiKit.Stars(card, 0, 170f, 20f, arc: true);
            _starsRoot = (RectTransform)_stars[0].transform.parent;
            AtTop(_starsRoot, 200f, _starsRoot.sizeDelta);

            _subtitle = HuntUiKit.Label(card, "", 44, Theme.inkSoft, TextAnchor.MiddleCenter, false, FontStyle.Normal);
            AtTop(_subtitle.rectTransform, 430f, new Vector2(760f, 60f));

            _coinRow = AtTop(HuntUiKit.Rect("CoinRow", card), 510f, new Vector2(440f, 130f));
            var rowBg = HuntUiKit.Rounded(_coinRow, "Bg", Theme.panelAlt, 50f);
            HuntUiKit.Stretch(rowBg.rectTransform);
            _coinIcon = HuntUiKit.CoinIcon(_coinRow, 120f);
            HuntUiKit.Place(_coinIcon, new Vector2(0f, 0.5f), new Vector2(120f, 120f), new Vector2(40f, 0f));
            var amount = HuntUiKit.Label(_coinRow, "+0", 72, Theme.ink);
            amount.rectTransform.offsetMin = new Vector2(110f, 0f);
            _coins = amount.gameObject.AddComponent<HuntCounterText>();
            _coins.prefix = "+";

            _double = HuntUiKit.Button(card, "×2 COINS", Theme.violet, new Vector2(560f, 120f), OnDouble,
                NixinIcons.Load(NixinIcons.Play), 46);
            HuntUiKit.Place(_double.Root, new Vector2(0.5f, 0f), new Vector2(560f, 120f), new Vector2(0f, 290f));

            _next = HuntUiKit.Button(card, "NEXT", Theme.success, new Vector2(480f, 140f), () => Collect(_p?.OnNext), null, 60);
            HuntUiKit.Place(_next.Root, new Vector2(0.5f, 0f), new Vector2(480f, 140f), new Vector2(60f, 120f));
            _next.Button.GetComponent<HuntButtonJuice>().sfx = NixinAudio.Confirm;

            _home = HuntUiKit.IconButton(card, NixinIcons.Load(NixinIcons.Home), Theme.primary, 130f, () => Collect(_p?.OnHome));
            HuntUiKit.Place(_home.Root, new Vector2(0.5f, 0f), new Vector2(130f, 130f), new Vector2(-280f, 125f));
        }

        public override void Bind(HuntWinPayload payload)
        {
            EnsureBuilt();
            _p = payload ?? new HuntWinPayload();
            _ribbon.text = _p.Title;
            for (var i = 0; i < _stars.Length; i++)
                _stars[i].color = new Color(0f, 0f, 0f, 0.15f);
            _coins.SetValue(0, false);
            _subtitle.text = _p.FirstClear ? "New level cleared!" : "Replay bonus";
            _double.gameObject.SetActive(_p.CanDouble && _p.RequestDouble != null && _p.Coins > 0);
            _double.SetInteractable(true);
            _next.SetLabel(_p.NextLabel);
            _pendingFly = _p.Coins;
            SetBackHandler(() => Collect(_p.OnHome));
        }

        protected override void OnShown()
        {
            HuntHaptics.Medium();
            var layer = HuntPopups.OverlayLayer;
            if (layer != null)
                HuntFx.Confetti(layer, new Vector2(0f, 300f), 80);
            for (var i = 0; i < 3; i++)
            {
                var star = _stars[i];
                var earned = i < _p.Stars;
                var index = i;
                star.transform.localScale = Vector3.zero;
                HuntTweener.Scale(star.transform, Vector3.one * 2f, Vector3.one, 0.4f, Ease.OutBack, 0.35f + i * 0.28f,
                    () =>
                    {
                        if (!earned)
                            return;
                        HuntAudio.Sfx(NixinAudio.Confirm, 1f + index * 0.15f);
                        HuntHaptics.Light();
                        HuntFx.Sparkle((RectTransform)star.transform.parent, star.rectTransform.anchoredPosition, Theme.accent);
                    });
                if (earned)
                    HuntTweener.Tint(star, new Color(0f, 0f, 0f, 0.15f), Theme.accent, 0.1f, 0.35f + i * 0.28f);
            }

            HuntTweener.Delay(_coins, 1.2f, () => _coins.SetValue(_p.Coins, true, 0.8f));
            Stagger(1.3f, _double.Root, _next.Root, _home.Root);
        }

        void OnDouble()
        {
            _double.SetInteractable(false);
            _p.RequestDouble?.Invoke(extra =>
            {
                if (extra <= 0)
                {
                    _double.SetInteractable(true);
                    return;
                }

                _pendingFly += extra;
                _coins.SetValue(_coins.Value + extra, true, 0.6f);
                HuntTweener.Punch(_coinRow, 0.2f, 0.4f);
                HuntFx.Sparkle((RectTransform)_coinRow.parent, _coinRow.anchoredPosition, Theme.accent, 10, 260f);
                _double.gameObject.SetActive(false);
            });
        }

        /// <summary>Coins fly to the wallet pill, then the popup closes and runs next.</summary>
        void Collect(Action next)
        {
            var pill = HuntCurrencyPill.Find(HuntCurrencies.Coins);
            var amount = _pendingFly;
            _pendingFly = 0;
            if (pill == null || amount <= 0 || HuntPopups.OverlayLayer == null)
            {
                if (pill != null)
                    pill.ReleaseAll();
                Dismiss(next);
                return;
            }

            var count = Mathf.Clamp(amount / 10, 4, 12);
            var per = Mathf.Max(1, amount / count);
            var from = RectTransformUtility.WorldToScreenPoint(null, _coinIcon.position);
            _next.Button.interactable = false;
            _home.Button.interactable = false;
            HuntFx.FlyIcons(HuntPopups.OverlayLayer, from, pill.IconRect, count, HuntUiKit.CoinSprite, Color.white,
                () =>
                {
                    pill.Release(per);
                    HuntAudio.Sfx(NixinAudio.Click, 1.4f, 0.6f);
                },
                () =>
                {
                    pill.ReleaseAll();
                    _next.Button.interactable = true;
                    _home.Button.interactable = true;
                    Dismiss(next);
                });
        }

        public override void OnClosed()
        {
            var pill = HuntCurrencyPill.Find(HuntCurrencies.Coins);
            if (pill != null)
                pill.ReleaseAll();
        }
    }

    // ───────────────────────────── Fail / continue ─────────────────────────────

    public sealed class HuntFailPayload
    {
        public int Found;
        public int Total;
        public int ContinueCost;
        public float ContinueSeconds = 30f;
        /// <summary>Seconds the rewarded video adds (usually less than the coin continue).</summary>
        public float AdContinueSeconds = 20f;
        public bool AdContinueAvailable;
        public Action OnContinueCoins;
        public Action OnContinueAd;
        public Action OnRetry;
        public Action OnHome;
    }

    public sealed class HuntFailPopup : HuntPopupBase<HuntFailPayload>
    {
        HuntFailPayload _p;
        Text _clock;
        Text _progressText;
        Image _progressFill;
        HuntKitButton _coins;
        HuntKitButton _ad;
        HuntKitButton _retry;
        HuntKitButton _home;

        protected override Vector2 CardSize => new Vector2(880f, 1120f);
        protected override string OpenSfx => NixinAudio.Fail;

        protected override void BuildContent(RectTransform card)
        {
            HuntUiKit.Ribbon(card, "TIME'S UP", Theme.danger, 760f);

            _clock = HuntUiKit.Label(card, "0:00", 150, Theme.danger, TextAnchor.MiddleCenter, true, FontStyle.Bold, true);
            AtTop(_clock.rectTransform, 120f, new Vector2(600f, 190f));

            var bar = HuntUiKit.Rounded(card, "ProgressBg", new Color(0f, 0f, 0f, 0.1f), 30f);
            AtTop(bar.rectTransform, 330f, new Vector2(620f, 60f));
            _progressFill = HuntUiKit.Rounded(bar.transform, "Fill", Theme.success, 30f);
            _progressFill.rectTransform.anchorMin = Vector2.zero;
            _progressFill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _progressFill.rectTransform.offsetMin = Vector2.zero;
            _progressFill.rectTransform.offsetMax = Vector2.zero;
            _progressText = HuntUiKit.Label(bar.transform, "", 34, Color.white, TextAnchor.MiddleCenter, true, FontStyle.Bold, true);

            _ad = HuntUiKit.Button(card, "WATCH VIDEO  +20s", Theme.accent, new Vector2(700f, 140f), () => Dismiss(_p?.OnContinueAd),
                NixinIcons.Load(NixinIcons.Play), 36);
            AtTop(_ad.Root, 440f, new Vector2(700f, 140f));
            _ad.Root.gameObject.AddComponent<HuntIdlePulse>().amount = 0.03f;

            _coins = HuntUiKit.Button(card, "+30s", Theme.glow, new Vector2(700f, 130f), () => Dismiss(_p?.OnContinueCoins),
                HuntUiKit.CoinSprite, 44);
            AtTop(_coins.Root, 600f, new Vector2(700f, 130f));

            _retry = HuntUiKit.Button(card, "RETRY", Theme.primary, new Vector2(400f, 120f), () => Dismiss(_p?.OnRetry),
                NixinIcons.Load(NixinIcons.Restart), 46);
            HuntUiKit.Place(_retry.Root, new Vector2(0.5f, 0f), new Vector2(400f, 120f), new Vector2(90f, 110f));

            _home = HuntUiKit.IconButton(card, NixinIcons.Load(NixinIcons.Home), Theme.primary, 120f, () => Dismiss(_p?.OnHome));
            HuntUiKit.Place(_home.Root, new Vector2(0.5f, 0f), new Vector2(120f, 120f), new Vector2(-220f, 110f));
        }

        public override void Bind(HuntFailPayload payload)
        {
            EnsureBuilt();
            _p = payload ?? new HuntFailPayload();
            var frac = _p.Total > 0 ? Mathf.Clamp01(_p.Found / (float)_p.Total) : 0f;
            _progressFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.08f, frac), 1f);
            _progressText.text = "FOUND " + _p.Found + " / " + _p.Total;
            var secs = Mathf.RoundToInt(_p.ContinueSeconds);
            _coins.SetLabel("+" + secs + "s   " + _p.ContinueCost);
            _coins.SetInteractable(_p.OnContinueCoins != null);
            _ad.SetLabel("WATCH VIDEO  +" + Mathf.RoundToInt(_p.AdContinueSeconds) + "s");
            _ad.gameObject.SetActive(_p.AdContinueAvailable && _p.OnContinueAd != null);
            AtTop(_coins.Root, _ad.gameObject.activeSelf ? 600f : 460f, new Vector2(700f, 130f));
            SetBackHandler(() => Dismiss(_p.OnHome));
        }

        protected override void OnShown()
        {
            HuntHaptics.Heavy();
            var rt = _clock.rectTransform;
            var basePos = rt.anchoredPosition;
            HuntTweener.To(rt, 0.5f, k => rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(k * 40f) * 18f * (1f - k), 0f),
                Ease.Linear, 0.2f, () => rt.anchoredPosition = basePos);
            Stagger(0.25f, _coins.Root, _ad.Root, _retry.Root, _home.Root);
        }
    }

    // ───────────────────────────── Jam (out of hints / coins) ─────────────────────────────

    public sealed class HuntJamPayload
    {
        public string Currency = HuntCurrencies.Hints;
        public string Title = "Out of Hints!";
        public string Message = "";
        public bool AdAvailable;
        public string AdLabel = "WATCH VIDEO";
        public Action OnAd;
        /// <summary>0 hides the coins option.</summary>
        public int CoinCost;
        public string CoinLabel = "";
        public Action OnCoins;
        public Action OnShop;
        public Action OnClose;
    }

    public sealed class HuntJamPopup : HuntPopupBase<HuntJamPayload>
    {
        HuntJamPayload _p;
        Text _title;
        Text _message;
        RectTransform _iconSlot;
        HuntKitButton _ad;
        HuntKitButton _coins;
        HuntKitButton _shop;

        protected override Vector2 CardSize => new Vector2(860f, 1040f);
        protected override string OpenSfx => NixinAudio.Error;

        protected override void BuildContent(RectTransform card)
        {
            _title = HuntUiKit.Ribbon(card, "Out of Hints!", Theme.primary, 760f);
            _iconSlot = AtTop(HuntUiKit.Rect("IconSlot", card), 130f, new Vector2(220f, 220f));
            var bob = _iconSlot.gameObject.AddComponent<HuntFloatBob>();
            bob.amplitude = 10f;
            bob.rotate = 8f;
            _message = Body(card, "", 370f, 120f);

            _ad = HuntUiKit.Button(card, "WATCH VIDEO", Theme.info, new Vector2(620f, 130f), () => Dismiss(_p?.OnAd),
                NixinIcons.Load(NixinIcons.Play), 46);
            AtTop(_ad.Root, 510f, new Vector2(620f, 130f));

            _coins = HuntUiKit.Button(card, "USE COINS", Theme.accent, new Vector2(620f, 130f), () => Dismiss(_p?.OnCoins),
                HuntUiKit.CoinSprite, 46);
            AtTop(_coins.Root, 665f, new Vector2(620f, 130f));

            _shop = HuntUiKit.Button(card, "GO TO SHOP", Theme.success, new Vector2(620f, 130f), () => Dismiss(_p?.OnShop),
                NixinIcons.Load(NixinIcons.Plus), 46);
            HuntUiKit.Place(_shop.Root, new Vector2(0.5f, 0f), new Vector2(620f, 130f), new Vector2(0f, 110f));

            CloseButton(card, () => Dismiss(_p?.OnClose));
        }

        public override void Bind(HuntJamPayload payload)
        {
            EnsureBuilt();
            _p = payload ?? new HuntJamPayload();
            _title.text = _p.Title;
            _message.text = _p.Message;
            for (var i = _iconSlot.childCount - 1; i >= 0; i--)
                Destroy(_iconSlot.GetChild(i).gameObject);
            var icon = _p.Currency == HuntCurrencies.Coins
                ? HuntUiKit.CoinIcon(_iconSlot, 220f)
                : HuntUiKit.HintBadgeIcon(_iconSlot, 220f);
            HuntUiKit.Stretch(icon);
            _ad.gameObject.SetActive(_p.AdAvailable && _p.OnAd != null);
            _ad.SetLabel(_p.AdLabel);
            _coins.gameObject.SetActive(_p.CoinCost > 0 && _p.OnCoins != null);
            _coins.SetLabel(string.IsNullOrEmpty(_p.CoinLabel) ? "USE " + _p.CoinCost : _p.CoinLabel);
            _coins.SetInteractable(HuntWallet.CanAfford(HuntCurrencies.Coins, _p.CoinCost));
            _shop.gameObject.SetActive(_p.OnShop != null);
            SetBackHandler(() => Dismiss(_p.OnClose));
        }

        protected override void OnShown()
        {
            HuntHaptics.Medium();
            Stagger(0.15f, _iconSlot, _ad.Root, _coins.Root, _shop.Root);
        }
    }

    // ───────────────────────────── Pause ─────────────────────────────

    public sealed class HuntPausePayload
    {
        public string Title = "PAUSED";
        public Action OnResume;
        public Action OnRestart;
        public Action OnHome;
    }

    public sealed class HuntPausePopup : HuntPopupBase<HuntPausePayload>
    {
        HuntPausePayload _p;
        HuntSettingsBlock _settings;
        Text _title;

        protected override Vector2 CardSize => new Vector2(860f, 1100f);

        protected override void BuildContent(RectTransform card)
        {
            _title = HuntUiKit.Ribbon(card, "PAUSED", Theme.primary, 700f);
            _settings = HuntSettingsBlock.Build(card, 150f);

            var resume = HuntUiKit.Button(card, "RESUME", Theme.glow, new Vector2(640f, 140f), () => Dismiss(_p?.OnResume),
                NixinIcons.Load(NixinIcons.Play), 54);
            AtTop(resume.Root, 560f, new Vector2(640f, 140f));
            resume.Root.gameObject.AddComponent<HuntIdlePulse>().amount = 0.025f;
            var restart = HuntUiKit.Button(card, "RESTART", Theme.primary, new Vector2(640f, 120f), () => Dismiss(_p?.OnRestart),
                NixinIcons.Load(NixinIcons.Restart), 42);
            AtTop(restart.Root, 740f, new Vector2(640f, 120f));
            var home = HuntUiKit.Button(card, "LEAVE MISSION", Theme.danger, new Vector2(640f, 120f), () => Dismiss(_p?.OnHome),
                NixinIcons.Load(NixinIcons.Home), 38);
            AtTop(home.Root, 890f, new Vector2(640f, 120f));
        }

        public override void Bind(HuntPausePayload payload)
        {
            EnsureBuilt();
            _p = payload ?? new HuntPausePayload();
            _title.text = _p.Title;
            _settings.Sync();
            SetBackHandler(() => Dismiss(_p.OnResume));
        }
    }

    // ───────────────────────────── Settings ─────────────────────────────

    /// <summary>Music / Sound / Vibration toggles, shared by Pause and Settings.</summary>
    public sealed class HuntSettingsBlock
    {
        HuntKitToggle _music;
        HuntKitToggle _sfx;
        HuntKitToggle _haptics;

        public static HuntSettingsBlock Build(RectTransform card, float top)
        {
            var block = new HuntSettingsBlock();
            var bg = HuntUiKit.Rounded(card, "SettingsBg", HuntUiTheme.Current.panelAlt, 40f);
            HuntUiKit.Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(760f, 380f), new Vector2(0f, -top), new Vector2(0.5f, 1f));
            block._music = HuntUiKit.Toggle(bg.transform, "Music", HuntSettings.Music, v =>
            {
                HuntSettings.Music = v;
                HuntAudio.EnsureMusic();
            });
            HuntUiKit.Place(block._music.Root, new Vector2(0.5f, 1f), new Vector2(680f, 110f), new Vector2(0f, -70f));
            block._sfx = HuntUiKit.Toggle(bg.transform, "Sound FX", HuntSettings.Sfx, v => HuntSettings.Sfx = v);
            HuntUiKit.Place(block._sfx.Root, new Vector2(0.5f, 1f), new Vector2(680f, 110f), new Vector2(0f, -190f));
            block._haptics = HuntUiKit.Toggle(bg.transform, "Vibration", HuntSettings.Haptics, v =>
            {
                HuntSettings.Haptics = v;
                if (v)
                    HuntHaptics.Medium();
            });
            HuntUiKit.Place(block._haptics.Root, new Vector2(0.5f, 1f), new Vector2(680f, 110f), new Vector2(0f, -310f));
            return block;
        }

        public void Sync()
        {
            _music.Set(HuntSettings.Music);
            _sfx.Set(HuntSettings.Sfx);
            _haptics.Set(HuntSettings.Haptics);
        }
    }

    public sealed class HuntSettingsPopup : HuntPopupBase<string>
    {
        HuntSettingsBlock _settings;

        protected override bool FullScreen => true;
        protected override Vector2 CardSize => new Vector2(1080f, 1920f);

        /// <summary>Replace with the studio's hosted policy before release.</summary>
        public const string PrivacyUrl = "https://nixinstudio.com/privacy";
        public const string CompanyName = "Nixin Studio";
        public const string ContactEmail = "support@nixinstudio.com";

        protected override void BuildContent(RectTransform card)
        {
            // Ribbon is drawn half above its anchor for floating cards; drop it fully onto the page.
            var title = HuntUiKit.Ribbon(card, "Settings", Theme.primary, 700f);
            ((RectTransform)title.transform.parent).anchoredPosition = new Vector2(0f, -90f);
            _settings = HuntSettingsBlock.Build(card, 310f);

            var privacy = HuntUiKit.Button(card, "PRIVACY POLICY", Theme.primary, new Vector2(620f, 110f),
                () => Application.OpenURL(PrivacyUrl), null, 38);
            AtTop(privacy.Root, 770f, new Vector2(620f, 110f));

            var logoTex = Resources.Load<Texture2D>("HiddenMosaic/NixinLogo");
            if (logoTex != null)
            {
                var sprite = Sprite.Create(logoTex, new Rect(0f, 0f, logoTex.width, logoTex.height), new Vector2(0.5f, 0.5f), 100f);
                var logo = HuntUiKit.Image(card, "CompanyLogo", sprite, Color.white);
                logo.preserveAspect = true;
                HuntUiKit.Place(logo.rectTransform, new Vector2(0.5f, 0f), new Vector2(474f, 124f), new Vector2(0f, 330f));
            }

            var name = HuntUiKit.Label(card, CompanyName, 44, Theme.ink, TextAnchor.MiddleCenter);
            HuntUiKit.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(800f, 60f), new Vector2(0f, 250f));
            var contact = HuntUiKit.Label(card, "Contact: " + ContactEmail, 34, Theme.inkSoft, TextAnchor.MiddleCenter, false,
                FontStyle.Normal);
            HuntUiKit.Place(contact.rectTransform, new Vector2(0.5f, 0f), new Vector2(900f, 50f), new Vector2(0f, 185f));
            var version = HuntUiKit.Label(card, "v" + Application.version, 30, Theme.inkSoft, TextAnchor.MiddleCenter, false,
                FontStyle.Normal);
            HuntUiKit.Place(version.rectTransform, new Vector2(0.5f, 0f), new Vector2(600f, 44f), new Vector2(0f, 120f));

            CloseButton(card, () => Dismiss());
        }

        public override void Bind(string from)
        {
            EnsureBuilt();
            _settings.Sync();
            SetBackHandler(() => Dismiss());
        }
    }

    // ───────────────────────────── Test rewarded ad ─────────────────────────────

    /// <summary>Stand-in for a rewarded video when the real ad SDK isn't running (Editor /
    /// standalone): full-screen overlay, countdown, then close to claim.</summary>
    public sealed class HuntTestAdPopup : HuntPopupBase<Action<bool>>
    {
        const float Seconds = 5f;

        Action<bool> _done;
        Text _count;
        Text _hint;
        HuntKitButton _close;
        float _left;
        bool _running;

        protected override bool FullScreen => true;
        protected override Vector2 CardSize => new Vector2(1080f, 1920f);
        protected override Color CardColor => new Color(0.05f, 0.05f, 0.08f, 1f);
        protected override string OpenSfx => null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            Nixin.Game.Core.SimulatedAdHooks.RewardedPresenter =
                done => HuntPopups.Open<HuntTestAdPopup, Action<bool>>(done);
        }

        protected override void BuildContent(RectTransform card)
        {
            var tag = HuntUiKit.Label(card, "TEST AD", 40, Color.white, TextAnchor.MiddleCenter);
            HuntUiKit.Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(600f, 60f), new Vector2(0f, -120f));
            var title = HuntUiKit.Label(card, "Rewarded video", 64, Color.white, TextAnchor.MiddleCenter);
            HuntUiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900f, 100f), new Vector2(0f, 120f));
            _count = HuntUiKit.Label(card, "5", 220, Theme.accent, TextAnchor.MiddleCenter);
            HuntUiKit.Place(_count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(600f, 260f), new Vector2(0f, -80f));
            _hint = HuntUiKit.Label(card, "Reward in 5s", 40, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter, false,
                FontStyle.Normal);
            HuntUiKit.Place(_hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(800f, 60f), new Vector2(0f, -280f));

            _close = HuntUiKit.IconButton(card, NixinIcons.Load(NixinIcons.Close), Theme.danger, 110f, () => Finish(true));
            HuntUiKit.Place(_close.Root, new Vector2(1f, 1f), new Vector2(110f, 110f), new Vector2(-30f, -30f));
        }

        public override void Bind(Action<bool> done)
        {
            EnsureBuilt();
            _done = done;
            _left = Seconds;
            _running = true;
            _close.Root.gameObject.SetActive(false);
            _hint.text = "Reward in " + Mathf.CeilToInt(_left) + "s";
            _count.text = Mathf.CeilToInt(_left).ToString();
            SetBackHandler(() => { });
        }

        void Update()
        {
            if (!_running)
                return;
            _left -= Time.unscaledDeltaTime;
            if (_left > 0f)
            {
                var n = Mathf.CeilToInt(_left);
                _count.text = n.ToString();
                _hint.text = "Reward in " + n + "s";
                return;
            }

            _running = false;
            _count.text = "\u2713";
            _hint.text = "Reward earned - close to claim";
            _close.Root.gameObject.SetActive(true);
            SetBackHandler(() => Finish(true));
        }

        void Finish(bool earned)
        {
            _running = false;
            var done = _done;
            _done = null;
            Dismiss(() => done?.Invoke(earned));
        }
    }

    // ───────────────────────────── Chapter complete ─────────────────────────────

    public sealed class HuntChapterPayload
    {
        public int Chapter;
        public int Coins;
        public Action OnContinue;
    }

    public sealed class HuntChapterPopup : HuntPopupBase<HuntChapterPayload>
    {
        HuntChapterPayload _p;
        Text _title;
        RectTransform _chest;
        RectTransform _lid;
        Text _reward;
        HuntKitButton _continue;
        bool _opened;

        protected override Vector2 CardSize => new Vector2(860f, 1080f);
        protected override string OpenSfx => NixinAudio.Win;

        protected override void BuildContent(RectTransform card)
        {
            _title = HuntUiKit.Ribbon(card, "Chapter Complete!", Theme.accent, 800f);
            _chest = AtTop(HuntUiKit.Rect("Chest", card), 170f, new Vector2(360f, 320f));
            var art = Theme.chestIcon;
            if (art != null)
            {
                var img = HuntUiKit.Image(_chest, "Art", art, Color.white);
                img.preserveAspect = true;
                HuntUiKit.Stretch(img.rectTransform);
                _lid = img.rectTransform;
            }
            else
            {
                var wood = HuntUiTheme.Hex("#B5651D");
                var body = HuntUiKit.Rounded(_chest, "Body", wood, 30f);
                HuntUiKit.Place(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(340f, 190f), new Vector2(0f, 95f));
                var band = HuntUiKit.Rounded(body.transform, "Band", Theme.accent, 10f);
                HuntUiKit.Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(60f, 190f));
                var lock_ = HuntUiKit.Image(body.transform, "Lock", HuntUiSprites.Circle, HuntUiTheme.Darken(Theme.accent, 0.2f));
                HuntUiKit.Place(lock_.rectTransform, new Vector2(0.5f, 1f), new Vector2(64f, 64f), new Vector2(0f, -20f));
                var lid = HuntUiKit.Rounded(_chest, "Lid", HuntUiTheme.Lighten(wood, 0.12f), 40f);
                HuntUiKit.Place(lid.rectTransform, new Vector2(0.5f, 0f), new Vector2(360f, 120f), new Vector2(0f, 230f));
                var lidBand = HuntUiKit.Rounded(lid.transform, "Band", Theme.accent, 10f);
                HuntUiKit.Place(lidBand.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(60f, 120f));
                _lid = lid.rectTransform;
            }

            _reward = HuntUiKit.Label(card, "+150", 90, Theme.ink, TextAnchor.MiddleCenter, false);
            AtTop(_reward.rectTransform, 560f, new Vector2(600f, 120f));
            var coin = HuntUiKit.CoinIcon(_reward.transform, 100f);
            HuntUiKit.Place(coin, new Vector2(0f, 0.5f), new Vector2(100f, 100f), new Vector2(80f, 0f));

            _continue = HuntUiKit.Button(card, "COLLECT", Theme.success, new Vector2(520f, 140f), Collect, null, 60);
            HuntUiKit.Place(_continue.Root, new Vector2(0.5f, 0f), new Vector2(520f, 140f), new Vector2(0f, 110f));
        }

        public override void Bind(HuntChapterPayload payload)
        {
            EnsureBuilt();
            _p = payload ?? new HuntChapterPayload();
            _title.text = "Chapter " + (_p.Chapter + 1) + " Complete!";
            _reward.text = "+" + _p.Coins;
            _reward.gameObject.SetActive(false);
            _opened = false;
            _lid.anchoredPosition = new Vector2(0f, 230f);
            _lid.localRotation = Quaternion.identity;
            SetBackHandler(Collect);
        }

        protected override void OnShown()
        {
            // Wobble, then burst open.
            HuntTweener.To(_chest, 0.9f, k => _chest.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * 50f) * 6f * k),
                Ease.Linear, 0.3f, OpenChest);
        }

        void OpenChest()
        {
            _chest.localRotation = Quaternion.identity;
            _opened = true;
            HuntAudio.Sfx(NixinAudio.Confirm, 0.9f);
            HuntHaptics.Heavy();
            var lidStart = _lid.anchoredPosition;
            HuntTweener.To(_lid, 0.45f, k =>
            {
                _lid.anchoredPosition = lidStart + new Vector2(60f * k, 140f * k);
                _lid.localRotation = Quaternion.Euler(0f, 0f, -25f * k);
            }, Ease.OutBack);
            HuntTweener.Punch(_chest, 0.25f, 0.5f);
            HuntFx.Sparkle((RectTransform)_chest.parent, _chest.anchoredPosition - new Vector2(0f, 160f), Theme.accent, 14, 320f);
            if (HuntPopups.OverlayLayer != null)
                HuntFx.Confetti(HuntPopups.OverlayLayer, new Vector2(0f, 200f), 70);
            _reward.gameObject.SetActive(true);
            HuntTweener.Scale(_reward.transform, Vector3.zero, Vector3.one, 0.45f, Ease.OutBack);
        }

        void Collect()
        {
            if (!_opened)
                OpenChest();
            var pill = HuntCurrencyPill.Find(HuntCurrencies.Coins);
            if (pill == null || HuntPopups.OverlayLayer == null)
            {
                if (pill != null)
                    pill.ReleaseAll();
                Dismiss(_p.OnContinue);
                return;
            }

            _continue.Button.interactable = false;
            var from = RectTransformUtility.WorldToScreenPoint(null, _reward.transform.position);
            var per = Mathf.Max(1, _p.Coins / 10);
            HuntFx.FlyIcons(HuntPopups.OverlayLayer, from, pill.IconRect, 10, HuntUiKit.CoinSprite, Color.white,
                () => pill.Release(per),
                () =>
                {
                    pill.ReleaseAll();
                    _continue.Button.interactable = true;
                    Dismiss(_p.OnContinue);
                });
        }

        public override void OnClosed()
        {
            var pill = HuntCurrencyPill.Find(HuntCurrencies.Coins);
            if (pill != null)
                pill.ReleaseAll();
        }
    }

    // ───────────────────────────── Photo hunt placeholder ─────────────────────────────

    public sealed class HuntComingSoonPayload
    {
        public HuntNode Node;
        public Action OnContinue;
    }

    public sealed class HuntComingSoonPopup : HuntPopupBase<HuntComingSoonPayload>
    {
        HuntComingSoonPayload _p;
        Text _title;

        protected override Vector2 CardSize => new Vector2(860f, 900f);

        protected override void BuildContent(RectTransform card)
        {
            _title = HuntUiKit.Ribbon(card, "Photo Hunt", Theme.accent, 700f);
            var art = AtTop(HuntUiKit.Rect("Art", card), 150f, new Vector2(240f, 240f));
            var ring = HuntUiKit.Image(art, "Ring", HuntUiSprites.Circle, Theme.accent);
            HuntUiKit.Stretch(ring.rectTransform);
            var star = HuntUiKit.Image(art, "Star", HuntUiSprites.Star, Color.white);
            HuntUiKit.Stretch(star.rectTransform, 40f);
            art.gameObject.AddComponent<HuntSpin>().degreesPerSecond = 25f;
            Body(card, "Real-photo hidden object levels are coming soon!\nKeep going to unlock the next chapter.", 420f, 180f);
            var go = HuntUiKit.Button(card, "CONTINUE", Theme.success, new Vector2(520f, 140f), () => Dismiss(_p?.OnContinue), null, 56);
            HuntUiKit.Place(go.Root, new Vector2(0.5f, 0f), new Vector2(520f, 140f), new Vector2(0f, 110f));
            CloseButton(card, () => Dismiss());
        }

        public override void Bind(HuntComingSoonPayload payload)
        {
            EnsureBuilt();
            _p = payload ?? new HuntComingSoonPayload();
            _title.text = _p.Node != null ? _p.Node.ImageHog.Title : "Photo Hunt";
            SetBackHandler(() => Dismiss());
        }
    }
}
