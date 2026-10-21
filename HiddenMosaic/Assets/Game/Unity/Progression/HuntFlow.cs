using Game.Core;
using UnityEngine.SceneManagement;

namespace Game.Unity
{
    /// <summary>Scene-level navigation shared by Home, Map and Play.</summary>
    public static class HuntFlow
    {
        /// <summary>Mosaic → Play scene; image HOG → bridge (scene or "coming soon").</summary>
        public static void Launch(HuntNode node)
        {
            if (node == null)
                return;
            if (node.Kind == HuntNodeKind.ImageHog)
            {
                PayPhotoFee(node, () => HuntImageHogBridge.Launch(node));
                return;
            }

            HuntLaunchRequest.SetNode(node.Index);
            HuntPopups.CloseAll();
            SceneManager.LoadScene(HuntScenes.Play);
        }

        /// <summary>Coin entry fee of a playable photo hunt (0 for "coming soon" placeholders).</summary>
        public static int PhotoFee(HuntNode node)
        {
            if (node == null || node.Kind != HuntNodeKind.ImageHog || node.IsPlaceholder
                && !HuntImageHogBridge.IsSceneAvailable(node))
                return 0;
            return HuntEconomyRules.PhotoFee(HuntRoadmapService.ToughnessOf(node), HuntWallet.Tuning);
        }

        /// <summary>Charges the photo-hunt fee, then runs proceed. Short on coins: offers the free-coins
        /// ad / shop, and re-tries once the player has topped up.</summary>
        public static void PayPhotoFee(HuntNode node, System.Action proceed)
        {
            var fee = PhotoFee(node);
            if (fee <= 0)
            {
                proceed?.Invoke();
                return;
            }

            if (HuntWallet.TrySpend(HuntCurrencies.Coins, fee, "photo_fee"))
            {
                proceed?.Invoke();
                return;
            }

            HuntJams.NotEnoughCoins(fee, () => PayPhotoFee(node, proceed));
        }

        public static void GoHome()
        {
            HuntLaunchRequest.ResetToDefaults();
            HuntPopups.CloseAll();
            SceneManager.LoadScene(HuntScenes.Home);
        }

        /// <summary>True when this node is harder than the previous mosaic level.</summary>
        public static bool IsDifficultyUp(HuntNode node)
        {
            if (node == null || node.Kind != HuntNodeKind.Mosaic)
                return false;
            var prev = HuntRoadmapService.Previous(node);
            while (prev != null && prev.Kind != HuntNodeKind.Mosaic)
                prev = HuntRoadmapService.Previous(prev);
            return prev != null && HuntRoadmapService.ToughnessOf(node) > HuntRoadmapService.ToughnessOf(prev);
        }

        public static HuntLevelIntroPayload IntroFor(HuntNode node, HuntLevelDef def)
        {
            var hog = node != null && node.Kind == HuntNodeKind.ImageHog;
            return new HuntLevelIntroPayload
            {
                Node = node,
                Title = def != null ? def.Title : HuntRoadmapService.TitleOf(node),
                Toughness = def != null ? def.Toughness : HuntRoadmapService.ToughnessOf(node),
                Targets = def != null ? def.TargetCount : 0,
                TimeLimit = def != null ? def.TimeLimitSeconds : (hog ? node.ImageHog.TimeLimitSeconds : 0f),
                BestStars = HuntSave.StarsFor(node),
                DifficultyUp = IsDifficultyUp(node)
            };
        }
    }
}
