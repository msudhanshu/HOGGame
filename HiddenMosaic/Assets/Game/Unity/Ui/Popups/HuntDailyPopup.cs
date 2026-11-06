using Game.Core;
using Nixin.Audio;
using Nixin.Icons;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Daily Hunt: today's special level (one bonus a day) + the 7-day login streak.</summary>
    public sealed class HuntDailyPopup : HuntPopupBase<string>
    {
        sealed class DayTile
        {
            public Image Face;
            public Image Edge;
            public GameObject Tick;
        }

        Text _streak;
        Text _reset;
        HuntKitButton _play;
        HuntKitButton _claim;
        Text _note;
        readonly DayTile[] _tiles = new DayTile[HuntDaily.StreakLength];
        string _from;

        protected override Vector2 CardSize => new Vector2(940f, 1560f);

        static RectTransform Put(RectTransform rt, float x, float top, float w, float h)
        {
            HuntUiKit.Place(rt, new Vector2(0f, 1f), new Vector2(w, h), new Vector2(x, -top), new Vector2(0f, 1f));
            return rt;
        }

        protected override void BuildContent(RectTransform card)
        {
            var theme = Theme;
            HuntUiKit.Ribbon(card, "DAILY HUNT", theme.primary, 700f);
            CloseButton(card, () => Dismiss());

            _streak = HuntUiKit.Label(card, "STREAK", 30, theme.inkSoft, TextAnchor.MiddleCenter, false, FontStyle.Bold, true);
            AtTop(_streak.rectTransform, 118f, new Vector2(800f, 48f));

            // ---- today's special ----
            var special = HuntUiKit.Rounded(card, "Special", HuntUiTheme.WithAlpha(theme.violet, 0.22f), 48f);
            Put(special.rectTransform, 50f, 180f, 840f, 400f);
            HuntUiKit.NeonEdge(special.transform, theme.violet, 48f, 1f);
            var tag = HuntUiKit.Label(special.transform, "TODAY'S SPECIAL", 28, new Color(0.9f, 0.78f, 1f), TextAnchor.UpperLeft,
                false, FontStyle.Bold, true);
            Put(tag.rectTransform, 32f, 24f, 400f, 40f);
            _reset = HuntUiKit.Label(special.transform, "", 28, theme.ink, TextAnchor.UpperRight, false, FontStyle.Bold);
            HuntUiKit.Place(_reset.rectTransform, new Vector2(1f, 1f), new Vector2(380f, 40f), new Vector2(-32f, -24f), new Vector2(1f, 1f));

            var orb = HuntUiKit.Image(special.transform, "Orb", HuntUiSprites.Circle, theme.deep);
            Put(orb.rectTransform, 32f, 84f, 170f, 170f);
            HuntUiKit.Stretch(HuntUiKit.Image(orb.transform, "Edge", HuntUiSprites.Ring, theme.violet).rectTransform);
            HuntUiKit.Label(orb.transform, "?", 110, new Color(0.9f, 0.78f, 1f), TextAnchor.MiddleCenter, false, FontStyle.Bold, true);
            orb.gameObject.AddComponent<HuntFloatBob>().amplitude = 6f;

            var name = HuntUiKit.Label(special.transform, "MYSTERY SHAPE", 44, theme.ink, TextAnchor.MiddleLeft, false, FontStyle.Bold, true);
            Put(name.rectTransform, 230f, 90f, 580f, 60f);
            var sub = HuntUiKit.Label(special.transform, "One of your hardest levels · bonus once a day", 28, theme.inkSoft,
                TextAnchor.UpperLeft, false, FontStyle.Normal);
            Put(sub.rectTransform, 230f, 154f, 580f, 80f);
            var coin = HuntUiKit.CoinIcon(special.transform, 44f);
            HuntUiKit.Place(coin, new Vector2(0f, 1f), new Vector2(44f, 44f), new Vector2(252f, -242f), new Vector2(0f, 1f));
            var reward = HuntUiKit.Label(special.transform, "+" + HuntDaily.SpecialCoins + "  ·  1 hint chest", 32, theme.accent,
                TextAnchor.MiddleLeft, false, FontStyle.Bold);
            Put(reward.rectTransform, 306f, 240f, 520f, 48f);

            _play = HuntUiKit.Button(special.transform, "PLAY TODAY'S HUNT", theme.glow, new Vector2(776f, 100f), OnPlaySpecial, null, 38);
            HuntUiKit.Place(_play.Root, new Vector2(0.5f, 0f), new Vector2(776f, 100f), new Vector2(0f, 34f));

            // ---- streak ----
            var label = HuntUiKit.Label(card, "LOGIN STREAK", 28, theme.inkSoft, TextAnchor.MiddleLeft, false, FontStyle.Bold, true);
            Put(label.rectTransform, 56f, 612f, 600f, 40f);
            const float tileW = 198f;
            const float tileH = 170f;
            const float gap = 16f;
            for (var i = 0; i < HuntDaily.StreakLength; i++)
            {
                var day = i + 1;
                var x = 50f + (i % 4) * (tileW + gap);
                var y = 664f + (i / 4) * (tileH + gap);
                var wide = day == HuntDaily.StreakLength;
                var w = wide ? tileW * 2f + gap : tileW;
                if (wide)
                    x = 50f + 2f * (tileW + gap);
                _tiles[i] = BuildDay(card, day, x, y, w, tileH);
            }

            _claim = HuntUiKit.Button(card, "CLAIM", theme.accent, new Vector2(760f, 130f), OnClaim, null, 48);
            HuntUiKit.Place(_claim.Root, new Vector2(0.5f, 0f), new Vector2(760f, 130f), new Vector2(0f, 150f));
            _claim.Root.gameObject.AddComponent<HuntIdlePulse>().amount = 0.025f;
            _note = HuntUiKit.Label(card, "Miss a day and the streak starts over", 28, theme.inkSoft, TextAnchor.MiddleCenter,
                false, FontStyle.Normal);
            HuntUiKit.Place(_note.rectTransform, new Vector2(0.5f, 0f), new Vector2(800f, 44f), new Vector2(0f, 84f));
        }

        DayTile BuildDay(RectTransform card, int day, float x, float y, float w, float h)
        {
            var theme = Theme;
            var tile = new DayTile();
            tile.Face = HuntUiKit.Rounded(card, "Day" + day, HuntUiTheme.WithAlpha(theme.glow, 0.06f), 36f);
            Put(tile.Face.rectTransform, x, y, w, h);
            tile.Edge = HuntUiKit.NeonEdge(tile.Face.transform, theme.glow, 36f, 0.35f);
            var reward = HuntDaily.RewardFor(day);

            var name = HuntUiKit.Label(tile.Face.transform, "DAY " + day, 24, theme.inkSoft, TextAnchor.UpperCenter, false,
                FontStyle.Bold, true);
            name.rectTransform.offsetMax = new Vector2(0f, -14f);

            Sprite sprite;
            Color tint;
            string text;
            switch (reward.Kind)
            {
                case HuntDailyRewardKind.Coins:
                    sprite = HuntUiKit.CoinSprite; tint = Color.white; text = reward.Amount.ToString();
                    break;
                case HuntDailyRewardKind.Hint:
                    sprite = HuntHintDock.GlyphOf(reward.Hint); tint = HuntHintDock.ColorOf(reward.Hint);
                    text = "1 " + HuntHints.Name(reward.Hint);
                    break;
                default:
                    sprite = HuntUiSprites.Star; tint = theme.accent; text = "TIME CHEST";
                    break;
            }

            var icon = HuntUiKit.Icon(tile.Face.transform, sprite, tint, new Vector2(60f, 60f));
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.52f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            var value = HuntUiKit.Label(tile.Face.transform, text, 28, theme.ink, TextAnchor.LowerCenter, false, FontStyle.Bold, true);
            value.rectTransform.offsetMin = new Vector2(0f, 12f);

            var tick = HuntUiKit.Image(tile.Face.transform, "Tick", NixinIcons.Load(NixinIcons.Check), theme.success);
            tick.preserveAspect = true;
            HuntUiKit.Place(tick.rectTransform, new Vector2(1f, 1f), new Vector2(40f, 40f), new Vector2(-10f, -10f), new Vector2(1f, 1f));
            tile.Tick = tick.gameObject;
            return tile;
        }

        public override void Bind(string from)
        {
            EnsureBuilt();
            _from = from;
            Refresh();
            SetBackHandler(() => Dismiss());
        }

        void Refresh()
        {
            var theme = Theme;
            var today = HuntDailyStore.TodayDay;
            var claimed = !HuntDailyStore.ClaimAvailable;
            _streak.text = "STREAK  ·  DAY " + Mathf.Max(1, claimed ? today : HuntDailyStore.Streak > 0 ? HuntDailyStore.Streak : 1);
            _reset.text = HuntDailyStore.ResetText();

            for (var i = 0; i < _tiles.Length; i++)
            {
                var day = i + 1;
                var t = _tiles[i];
                var done = day < today || (day == today && claimed);
                var isToday = day == today && !claimed;
                t.Tick.SetActive(done);
                t.Face.color = done ? HuntUiTheme.WithAlpha(theme.success, 0.12f)
                    : isToday ? HuntUiTheme.WithAlpha(theme.accent, 0.16f) : HuntUiTheme.WithAlpha(theme.glow, 0.06f);
                t.Edge.color = done ? HuntUiTheme.WithAlpha(theme.success, 0.7f)
                    : isToday ? theme.accent : HuntUiTheme.WithAlpha(theme.glow, 0.35f);
                var pulse = t.Face.GetComponent<HuntIdlePulse>();
                if (isToday && pulse == null)
                    t.Face.gameObject.AddComponent<HuntIdlePulse>().amount = 0.025f;
                else if (!isToday && pulse != null)
                    Destroy(pulse);
            }

            var reward = HuntDaily.RewardFor(today);
            _claim.SetLabel(claimed ? "CLAIMED" : "CLAIM  " + RewardText(reward));
            _claim.SetInteractable(!claimed);

            var special = HuntDailyStore.SpecialNode;
            _play.gameObject.SetActive(special != null);
            _play.SetLabel(HuntDailyStore.SpecialAvailable ? "PLAY TODAY'S HUNT" : "DONE · COME BACK TOMORROW");
            _play.SetInteractable(HuntDailyStore.SpecialAvailable);
        }

        static string RewardText(HuntDailyReward r)
        {
            switch (r.Kind)
            {
                case HuntDailyRewardKind.Coins: return "+" + r.Amount;
                case HuntDailyRewardKind.Hint: return "+1 " + HuntHints.Name(r.Hint);
                default: return "TIME CHEST";
            }
        }

        void OnClaim()
        {
            if (!HuntDailyStore.TryClaim(out var reward, out var day))
                return;
            HuntAudio.Sfx(NixinAudio.Win);
            HuntHaptics.Medium();
            HuntToast.Show("Day " + day + ": " + RewardText(reward), Theme.success);
            HuntFx.Sparkle((RectTransform)_claim.Root.parent, _claim.Root.anchoredPosition, Theme.accent, 12, 300f);
            Refresh();
        }

        void OnPlaySpecial()
        {
            Dismiss(HuntDailyStore.Launch);
        }

        protected override void OnShown()
        {
            Stagger(0.15f, _play.Root, _claim.Root);
        }
    }
}
