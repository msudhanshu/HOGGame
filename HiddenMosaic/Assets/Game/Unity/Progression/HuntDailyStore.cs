using System;
using Game.Core;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Daily Hunt persistence + payouts over <see cref="HuntDaily"/> (pure rules live in Core).</summary>
    public static class HuntDailyStore
    {
        const string Key = "hunt.daily.v1";
        static HuntDailyState _state;

        public static HuntDailyState State => _state ?? (_state = HuntDailyState.Parse(PlayerPrefs.GetString(Key, "")));

        public static string Today => HuntDaily.DayKey(DateTime.Now);

        static void Save()
        {
            PlayerPrefs.SetString(Key, State.Serialize());
            PlayerPrefs.Save();
        }

        public static bool ClaimAvailable => !HuntDaily.AlreadyClaimed(State, Today);
        public static bool SpecialAvailable => HuntDaily.SpecialAvailable(State, Today) && SpecialNode != null;
        /// <summary>Home tile dot: something to collect or play today.</summary>
        public static bool HasAttention => ClaimAvailable || SpecialAvailable;
        public static int TodayDay => HuntDaily.TodayDay(State, Today);
        public static int Streak => State.Streak;

        public static TimeSpan TimeToReset => DateTime.Now.Date.AddDays(1) - DateTime.Now;

        /// <summary>Today's special: one of the hardest unlocked mosaic levels, the same for the whole day.</summary>
        public static HuntNode SpecialNode
        {
            get
            {
                var nodes = HuntRoadmapService.Nodes;
                var idx = HuntDaily.PickNodeIndex(Today, nodes, HuntSave.IsUnlocked, HuntRoadmapService.ToughnessOf);
                return idx < 0 ? null : HuntRoadmapService.Node(idx);
            }
        }

        /// <summary>Claims today's streak reward. Returns it for the popup to celebrate.</summary>
        public static bool TryClaim(out HuntDailyReward reward, out int day)
        {
            reward = default;
            day = TodayDay;
            if (!ClaimAvailable)
                return false;
            HuntDaily.Claim(State, Today);
            Save();
            reward = HuntDaily.RewardFor(day);
            Grant(reward, "daily_streak");
            return true;
        }

        static void Grant(HuntDailyReward reward, string source)
        {
            switch (reward.Kind)
            {
                case HuntDailyRewardKind.Coins:
                    HuntWallet.Earn(HuntCurrencies.Coins, reward.Amount, source);
                    break;
                case HuntDailyRewardKind.Hint:
                    HuntWallet.Earn(HuntHintStock.Currency(reward.Hint), reward.Amount, source);
                    break;
                default:
                    HuntWallet.Earn(HuntCurrencies.Coins, reward.Amount, source);
                    foreach (var kind in HuntHints.All)
                        HuntWallet.Earn(HuntHintStock.Currency(kind), 1, source);
                    break;
            }
        }

        public static void Launch()
        {
            var node = SpecialNode;
            if (node != null)
                HuntFlow.LaunchDaily(node);
        }

        /// <summary>Called by the win flow of a daily launch. Pays once per day; returns the bonus coins (0 = already paid).
        /// The caller shows / holds the coins; the hint chest (one random hint) is granted here.</summary>
        public static int AwardSpecialWin()
        {
            if (!HuntDaily.SpecialAvailable(State, Today))
                return 0;
            State.HuntDoneDay = Today;
            Save();
            var kinds = HuntHints.All;
            var pick = kinds[HuntDaily.PickIndex(Today + "chest", kinds.Length)];
            HuntWallet.Earn(HuntHintStock.Currency(pick), 1, "daily_special");
            return HuntDaily.SpecialCoins;
        }

        public static string ResetText()
        {
            var t = TimeToReset;
            return "Resets in " + (int)t.TotalHours + "h " + t.Minutes.ToString("00") + "m";
        }
    }
}
