using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Unity.PlayTests
{
    /// <summary>Loads Home and Play and opens every meta popup; fails on any logged error/exception.</summary>
    public sealed class HuntMetaSmokeTests
    {
        static IEnumerator Frames(int n)
        {
            for (var i = 0; i < n; i++)
                yield return null;
        }

        static IEnumerator Settle() => Frames(30);

        [UnityTest]
        public IEnumerator HomeAndPopupsRender()
        {
            SceneManager.LoadScene(HuntScenes.Home);
            yield return Frames(60);
            Assert.IsNotNull(Object.FindAnyObjectByType<HuntHomeScreen>());
            Assert.IsNotNull(HuntCurrencyPill.Find(HuntCurrencies.Coins), "home coins pill");
            Assert.Greater(HuntRoadmapService.Nodes.Count, 0);

            HuntPopups.Shop("test");
            yield return Settle();
            Assert.IsTrue(HuntPopups.AnyOpen);
            HuntPopups.Settings("test");
            yield return Settle();
            HuntPopups.CloseAll();

            HuntPopups.Jam(new HuntJamPayload { Message = "x", AdAvailable = true, OnAd = () => { }, CoinCost = 50, OnCoins = () => { }, OnShop = () => { } });
            yield return Settle();
            HuntPopups.CloseAll();

            var nodes = HuntRoadmapService.Nodes;
            HuntPopups.LevelIntro(HuntFlow.IntroFor(nodes[1], HuntRoadmapService.LevelFor(nodes[1])));
            yield return Settle();
            HuntPopups.CloseAll();

            HuntPopups.Win(new HuntWinPayload { Stars = 3, Coins = 60, FirstClear = true, CanDouble = true, RequestDouble = cb => cb(60) });
            yield return Frames(150);
            HuntPopups.CloseAll();

            HuntPopups.Fail(new HuntFailPayload { Found = 3, Total = 6, ContinueCost = 60, AdContinueAvailable = true, OnContinueAd = () => { }, OnContinueCoins = () => { } });
            yield return Settle();
            HuntPopups.CloseAll();

            HuntPopups.Pause(new HuntPausePayload());
            yield return Settle();
            HuntPopups.CloseAll();

            HuntPopups.Chapter(new HuntChapterPayload { Chapter = 0, Coins = 150 });
            yield return Frames(120);
            HuntPopups.CloseAll();

            HuntNode hog = null;
            foreach (var n in nodes)
                if (n.Kind == HuntNodeKind.ImageHog) { hog = n; break; }
            Assert.IsNotNull(hog, "roadmap has an image HOG node");
            HuntPopups.ComingSoon(hog, null);
            yield return Settle();
            HuntPopups.CloseAll();
            yield return Settle();
        }

        [UnityTest]
        public IEnumerator PlaySceneShowsIntroThenRuns()
        {
            HuntLaunchRequest.SetNode(0);
            SceneManager.LoadScene(HuntScenes.Play);
            yield return Frames(90);
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayHost>());
            Assert.IsTrue(HuntPopups.AnyOpen, "level intro open");
            HuntPopups.CloseAll();
            yield return Frames(30);
            Assert.IsFalse(HuntPopups.AnyOpen);
        }
    }
}
