using System;
using System.Collections.Generic;
using Nixin.Game.Core;
using Nixin.Icons;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Wallet pill (coins / hints) with icon, rolling counter and "+" shop button.
    /// Coin-fly effects Hold() part of the balance and Release() it as icons land.</summary>
    public sealed class HuntCurrencyPill : MonoBehaviour
    {
        static readonly List<HuntCurrencyPill> Live = new List<HuntCurrencyPill>();

        string _currency;
        HuntCounterText _counter;
        int _held;

        public RectTransform IconRect { get; private set; }
        public string Currency => _currency;

        /// <summary>Most recently enabled visible pill for a currency (coin-fly target).</summary>
        public static HuntCurrencyPill Find(string currency)
        {
            for (var i = Live.Count - 1; i >= 0; i--)
            {
                var pill = Live[i];
                if (pill != null && pill._currency == currency && pill.isActiveAndEnabled)
                    return pill;
            }

            return null;
        }

        /// <summary>Call before crediting a reward that a popup will fly into the pill.</summary>
        public static void HoldOn(string currency, int amount)
        {
            var pill = Find(currency);
            if (pill != null)
                pill.Hold(amount);
        }

        public static HuntCurrencyPill Build(Transform parent, string currency, Action onPlus, float width = 300f)
        {
            var theme = HuntUiTheme.Current;
            var root = HuntUiKit.Rect(currency + "Pill", parent);
            HuntUiKit.Place(root, new Vector2(0.5f, 0.5f), new Vector2(width, 92f));
            var bg = HuntUiKit.Rounded(root, "Bg", new Color(0.12f, 0.03f, 0.25f, 0.45f), 46f);
            HuntUiKit.Stretch(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(36f, 6f);
            bg.rectTransform.offsetMax = new Vector2(0f, -6f);

            var icon = currency == HuntCurrencies.Coins ? HuntUiKit.CoinIcon(root, 104f) : HuntUiKit.HintBadgeIcon(root, 100f);
            HuntUiKit.Place(icon, new Vector2(0f, 0.5f), icon.sizeDelta, new Vector2(52f, 0f));

            var label = HuntUiKit.Label(root, "0", 46, Color.white, TextAnchor.MiddleCenter, outline: true);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.rectTransform.offsetMin = new Vector2(100f, 0f);
            label.rectTransform.offsetMax = new Vector2(onPlus != null ? -76f : -16f, 0f);

            var pill = root.gameObject.AddComponent<HuntCurrencyPill>();
            pill._currency = currency;
            pill.IconRect = icon;
            pill._counter = label.gameObject.AddComponent<HuntCounterText>();

            if (onPlus != null)
            {
                var plus = HuntUiKit.IconButton(root, NixinIcons.Load(NixinIcons.Plus), theme.success, 68f, onPlus);
                HuntUiKit.Place(plus.Root, new Vector2(1f, 0.5f), new Vector2(68f, 68f), new Vector2(-40f, 4f));
            }

            pill.Refresh(false);
            return pill;
        }

        void OnEnable()
        {
            Live.Add(this);
            if (GameServices.Wallet != null)
                GameServices.Wallet.OnBalanceChanged += OnBalance;
            Refresh(false);
        }

        void OnDisable()
        {
            Live.Remove(this);
            if (GameServices.Wallet != null)
                GameServices.Wallet.OnBalanceChanged -= OnBalance;
        }

        void OnBalance(string currency, int balance)
        {
            if (currency != _currency)
                return;
            Refresh(true);
            if (_held == 0)
                HuntTweener.Punch(transform, 0.12f, 0.35f);
        }

        public void Refresh(bool animate)
        {
            if (_counter == null || string.IsNullOrEmpty(_currency) || GameServices.Wallet == null)
                return;
            var value = Mathf.Max(0, GameServices.Wallet.Get(_currency) - _held);
            _counter.SetValue(value, animate);
        }

        /// <summary>Hide `amount` of the balance until Release (icons in flight).</summary>
        public void Hold(int amount)
        {
            _held += Mathf.Max(0, amount);
            Refresh(false);
        }

        public void Release(int amount)
        {
            _held = Mathf.Max(0, _held - Mathf.Max(0, amount));
            Refresh(true);
        }

        public void ReleaseAll()
        {
            _held = 0;
            Refresh(true);
        }
    }
}
