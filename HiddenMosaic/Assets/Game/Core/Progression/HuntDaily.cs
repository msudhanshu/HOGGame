using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Core
{
    public enum HuntDailyRewardKind
    {
        Coins,
        Hint,
        /// <summary>Day-7 chest: coins plus one of every hint.</summary>
        Chest
    }

    public struct HuntDailyReward
    {
        public HuntDailyRewardKind Kind;
        public int Amount;
        public HuntHintKind Hint;
    }

    /// <summary>Persisted daily-hunt progress. Day keys are "yyyy-MM-dd" in the player's local calendar.</summary>
    public sealed class HuntDailyState
    {
        public string LastClaimDay = "";
        /// <summary>Consecutive days claimed up to and including <see cref="LastClaimDay"/>.</summary>
        public int Streak;
        /// <summary>Day the special hunt was won (one try's worth of reward per day).</summary>
        public string HuntDoneDay = "";

        public string Serialize() => LastClaimDay + "|" + Streak.ToString(CultureInfo.InvariantCulture) + "|" + HuntDoneDay;

        public static HuntDailyState Parse(string raw)
        {
            var s = new HuntDailyState();
            if (string.IsNullOrEmpty(raw))
                return s;
            var parts = raw.Split('|');
            if (parts.Length > 0) s.LastClaimDay = parts[0];
            if (parts.Length > 1) int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out s.Streak);
            if (parts.Length > 2) s.HuntDoneDay = parts[2];
            return s;
        }
    }

    public static class HuntDaily
    {
        public const int StreakLength = 7;
        public const int SpecialCoins = 250;

        public static string DayKey(DateTime local) => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        static string Yesterday(string today)
        {
            return DateTime.TryParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? DayKey(d.AddDays(-1))
                : "";
        }

        public static bool AlreadyClaimed(HuntDailyState state, string today) => state.LastClaimDay == today;

        /// <summary>1..7: which day of the streak today's reward is (or was, when already claimed).</summary>
        public static int TodayDay(HuntDailyState state, string today)
        {
            if (state.LastClaimDay == today)
                return ((Math.Max(1, state.Streak) - 1) % StreakLength) + 1;
            if (state.LastClaimDay == Yesterday(today))
                return (Math.Max(0, state.Streak) % StreakLength) + 1;
            return 1;
        }

        /// <summary>Marks today claimed and advances (or restarts) the streak. Returns the reward day claimed.</summary>
        public static int Claim(HuntDailyState state, string today)
        {
            if (AlreadyClaimed(state, today))
                return TodayDay(state, today);
            var day = TodayDay(state, today);
            state.Streak = state.LastClaimDay == Yesterday(today) ? state.Streak + 1 : 1;
            state.LastClaimDay = today;
            return day;
        }

        public static HuntDailyReward RewardFor(int day)
        {
            switch (((day - 1) % StreakLength + StreakLength) % StreakLength + 1)
            {
                case 1: return new HuntDailyReward { Kind = HuntDailyRewardKind.Coins, Amount = 50 };
                case 2: return new HuntDailyReward { Kind = HuntDailyRewardKind.Hint, Amount = 1, Hint = HuntHintKind.LockOn };
                case 3: return new HuntDailyReward { Kind = HuntDailyRewardKind.Coins, Amount = 100 };
                case 4: return new HuntDailyReward { Kind = HuntDailyRewardKind.Coins, Amount = 150 };
                case 5: return new HuntDailyReward { Kind = HuntDailyRewardKind.Hint, Amount = 1, Hint = HuntHintKind.Radar };
                case 6: return new HuntDailyReward { Kind = HuntDailyRewardKind.Coins, Amount = 200 };
                default: return new HuntDailyReward { Kind = HuntDailyRewardKind.Chest, Amount = 300 };
            }
        }

        public static bool SpecialAvailable(HuntDailyState state, string today) => state.HuntDoneDay != today;

        /// <summary>Same pick for everyone on a given day: hashes the day key into the candidate list.</summary>
        public static int PickIndex(string today, int candidateCount)
        {
            if (candidateCount <= 0)
                return -1;
            unchecked
            {
                var h = 17;
                for (var i = 0; i < today.Length; i++)
                    h = h * 31 + today[i];
                return (int)((uint)h % (uint)candidateCount);
            }
        }

        /// <summary>Hardest unlocked mosaic nodes (by toughness) form the pool; one is chosen by day.</summary>
        public static int PickNodeIndex(string today, IReadOnlyList<HuntNode> nodes, Func<HuntNode, bool> isUnlocked,
            Func<HuntNode, int> toughness)
        {
            var best = -1;
            var pool = new List<int>();
            for (var i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.Kind != HuntNodeKind.Mosaic || (isUnlocked != null && !isUnlocked(n)))
                    continue;
                var t = toughness != null ? toughness(n) : 1;
                if (t > best)
                {
                    best = t;
                    pool.Clear();
                }

                if (t == best)
                    pool.Add(n.Index);
            }

            var pick = PickIndex(today, pool.Count);
            return pick < 0 ? -1 : pool[pick];
        }
    }
}
