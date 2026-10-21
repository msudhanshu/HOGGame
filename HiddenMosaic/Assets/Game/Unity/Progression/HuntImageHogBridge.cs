using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Unity
{
    /// <summary>Contract between the meta layer and the real-image HOG scene (built separately).
    ///
    /// Launch side (map / Win "Next"): <see cref="Launch"/>. If the node's scene is in Build
    /// Settings it is loaded with HuntLaunchRequest.ImageHog set; otherwise a "Coming soon"
    /// placeholder popup lets the player continue.
    ///
    /// Image-HOG scene side: read HuntLaunchRequest.ImageHog for id/title/time limit, then on
    /// finish call ReportResult(...) and ReturnToHome(). Home applies stars/coins and shows the
    /// Chapter Complete popup.</summary>
    public static class HuntImageHogBridge
    {
        public sealed class Result
        {
            public int NodeIndex;
            public bool Success;
            public float TimeLeftFraction;
            public int HintsUsed;
        }

        /// <summary>Set by ReportResult, consumed by Home (HuntHomeScreen).</summary>
        public static Result Pending { get; private set; }

        /// <summary>True when the node plays as a day/night reveal level inside the Play scene.</summary>
        public static bool HasRevealSet(HuntNode node)
        {
            return node != null && node.Kind == HuntNodeKind.ImageHog && node.ImageHog.HasRevealSet
                   && RevealSetAsset.Load(node.ImageHog.RevealSet) != null;
        }

        public static bool IsSceneAvailable(HuntNode node)
        {
            return node != null && node.Kind == HuntNodeKind.ImageHog && !string.IsNullOrEmpty(node.ImageHog.Scene)
                   && Application.CanStreamedLevelBeLoaded(node.ImageHog.Scene);
        }

        public static void Launch(HuntNode node)
        {
            if (node == null || node.Kind != HuntNodeKind.ImageHog)
                return;
            if (HasRevealSet(node))
            {
                HuntAnalytics.ImageHogStart(node.ImageHog.Id, false);
                HuntLaunchRequest.SetImageHog(node.Index, node.ImageHog);
                HuntPopups.CloseAll();
                SceneManager.LoadScene(HuntScenes.Play);
                return;
            }

            HuntAnalytics.ImageHogStart(node.ImageHog.Id, node.IsPlaceholder || !IsSceneAvailable(node));
            if (IsSceneAvailable(node))
            {
                HuntLaunchRequest.SetImageHog(node.Index, node.ImageHog);
                HuntPopups.CloseAll();
                SceneManager.LoadScene(node.ImageHog.Scene);
                return;
            }

            HuntPopups.ComingSoon(node, () =>
            {
                Pending = new Result { NodeIndex = node.Index, Success = true, TimeLeftFraction = 0f, HintsUsed = 0 };
                if (SceneManager.GetActiveScene().name == HuntScenes.Home)
                    HuntHomeScreen.ConsumePendingResult();
                else
                    ReturnToHome();
            });
        }

        /// <summary>Call from the image-HOG scene when the level ends.</summary>
        public static void ReportResult(bool success, float timeLeftFraction, int hintsUsed)
        {
            var index = HuntLaunchRequest.NodeIndex;
            Pending = new Result
            {
                NodeIndex = index,
                Success = success,
                TimeLeftFraction = Mathf.Clamp01(timeLeftFraction),
                HintsUsed = Mathf.Max(0, hintsUsed)
            };
        }

        public static void ReturnToHome()
        {
            HuntLaunchRequest.ResetToDefaults();
            HuntPopups.CloseAll();
            SceneManager.LoadScene(HuntScenes.Home);
        }

        internal static Result TakePending()
        {
            var p = Pending;
            Pending = null;
            return p;
        }
    }
}
