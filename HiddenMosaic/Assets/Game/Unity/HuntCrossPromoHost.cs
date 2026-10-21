using System;
using System.Collections.Generic;
using Nixin.CrossPromo;
using Nixin.Monetization;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>
    /// Persistent bridge to the shared "Nixin Games" cross-promo (com.nixin.crosspromo): lists the other studio games,
    /// runs their Try It slices, and swaps some interstitials for a promo card. It lives across scenes because the
    /// promo card can appear in the Play scene. The game only supplies a level count and a reward.
    /// </summary>
    public sealed class HuntCrossPromoHost : MonoBehaviour, ICrossPromoHost, ICrossPromoRewardHost
    {
        public const string GameId = "hidden_mosaic";

        /// <summary>One rewarded ad is worth about this many hints (a hint costs 50 coins).</summary>
        const int RewardHints = 2;

        static HuntCrossPromoHost _instance;

        readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
        CrossPromoController _controller;

        public static bool IsTrying => _instance != null && _instance._controller != null && _instance._controller.IsTrying;

        /// <summary>Creates the host once (the Home scene calls this); later calls do nothing.</summary>
        public static void EnsureCreated()
        {
            if (_instance != null)
                return;
            var go = new GameObject("HuntCrossPromoHost");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HuntCrossPromoHost>();
            _instance.Boot();
        }

        /// <summary>Opens the games page. Returns false when there is nothing to show.</summary>
        public static bool Open()
        {
            if (_instance == null || !NixinCrossPromo.IsAvailable)
                return false;
            _instance._controller.Open();
            return true;
        }

        /// <summary>Android back: closes the page or leaves a try. True when it was handled.</summary>
        public static bool TryHandleBack() => _instance != null && _instance._controller != null && _instance._controller.TryHandleBack();

        void Boot()
        {
            // A game never lists itself, so no includeSelf. Levels completed = cleared roadmap nodes.
            NixinCrossPromo.Init(null, GameId, this, levelsCompleted: () => HuntSave.State.Stars.Count);
            _controller = CrossPromoController.Create(transform);
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void ICrossPromoHost.OnTryBegin(string gameId)
        {
            Monetization.HideBanner();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.enabled || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace)
                    continue;
                // The cross-promo UI is UI Toolkit, not a Canvas, so it is not hidden here.
                canvas.enabled = false;
                _hiddenCanvases.Add(canvas);
            }
        }

        void ICrossPromoHost.OnTryEnd(string gameId)
        {
            foreach (var canvas in _hiddenCanvases)
                if (canvas != null)
                    canvas.enabled = true;
            _hiddenCanvases.Clear();
            Monetization.ShowBannerIfAllowed();
        }

        CrossPromoReward ICrossPromoRewardHost.GrantTryReward(string gameId)
        {
            HuntWallet.Earn(HuntCurrencies.Hints, RewardHints, "crosspromo_try");
            return new CrossPromoReward("+" + RewardHints + " hints");
        }
    }
}
