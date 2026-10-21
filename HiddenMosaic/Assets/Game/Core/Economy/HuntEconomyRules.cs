namespace Game.Core
{
    /// <summary>Economy numbers. Defaults match the PRD; the Unity layer overlays remote config.</summary>
    public sealed class HuntEconomyTuning
    {
        public int StartCoins = 100;
        public int StartHints = 3;
        public int WinBase = 8;
        public int WinPerToughness = 4;
        public float Star2Mult = 1.5f;
        public float Star3Mult = 2f;
        /// <summary>Replay without a better star rating pays only this token amount.</summary>
        public int ReplayCoins = 2;
        public float Star3TimeFrac = 0.5f;
        public float Star2TimeFrac = 0.25f;
        /// <summary>Photo hunt payout at 1 star (scaled by the star multipliers like a mosaic win).</summary>
        public int ChapterChest = 40;
        public int HintCoinCost = 50;
        /// <summary>Coin entry fee for a photo hunt: base + perToughness * toughness (1-5).</summary>
        public int PhotoFeeBase = 50;
        public int PhotoFeePerToughness = 15;
        public int ContinueBase = 30;
        public int ContinueStep = 15;
        public float ContinueSeconds = 30f;
    }

    public static class HuntEconomyRules
    {
        /// <summary>3 = fast and no hints, 2 = reasonably fast, 1 = finished.</summary>
        public static int StarsFor(float timeLeftFrac, int hintsUsed, HuntEconomyTuning t)
        {
            t = t ?? new HuntEconomyTuning();
            if (timeLeftFrac >= t.Star3TimeFrac && hintsUsed <= 0)
                return 3;
            if (timeLeftFrac >= t.Star2TimeFrac)
                return 2;
            return 1;
        }

        static float StarMult(int stars, HuntEconomyTuning t) =>
            stars >= 3 ? t.Star3Mult : stars == 2 ? t.Star2Mult : 1f;

        static int Round1(float coins)
        {
            var rounded = (int)System.Math.Round(coins);
            return rounded < 1 ? 1 : rounded;
        }

        /// <summary>Pays `full` on first clear. On a replay, only the improvement over the previous
        /// best (full(stars) - full(prevBest)) is paid; no improvement pays the token ReplayCoins.</summary>
        static int Payout(float baseCoins, int stars, bool firstClear, int prevBestStars, HuntEconomyTuning t)
        {
            var full = Round1(baseCoins * StarMult(stars, t));
            if (firstClear)
                return full;
            if (stars > prevBestStars)
            {
                var before = prevBestStars < 1 ? 0 : Round1(baseCoins * StarMult(prevBestStars, t));
                return System.Math.Max(1, full - before);
            }

            return t.ReplayCoins < 0 ? 0 : t.ReplayCoins;
        }

        public static int CoinsForWin(int toughness, int stars, bool firstClear, int prevBestStars, HuntEconomyTuning t)
        {
            t = t ?? new HuntEconomyTuning();
            if (toughness < 1) toughness = 1;
            if (toughness > 5) toughness = 5;
            return Payout(t.WinBase + t.WinPerToughness * toughness, stars, firstClear, prevBestStars, t);
        }

        /// <summary>Photo hunt payout, same first-clear / improvement rules as a mosaic win.</summary>
        public static int CoinsForPhoto(int stars, bool firstClear, int prevBestStars, HuntEconomyTuning t)
        {
            t = t ?? new HuntEconomyTuning();
            return Payout(t.ChapterChest, stars, firstClear, prevBestStars, t);
        }

        /// <summary>Coin fee charged on every photo hunt attempt.</summary>
        public static int PhotoFee(int toughness, HuntEconomyTuning t)
        {
            t = t ?? new HuntEconomyTuning();
            if (toughness < 1) toughness = 1;
            if (toughness > 5) toughness = 5;
            return t.PhotoFeeBase + t.PhotoFeePerToughness * toughness;
        }

        /// <summary>Coin price of the (continuesUsed + 1)-th continue in the same level.</summary>
        public static int ContinueCost(int continuesUsed, HuntEconomyTuning t)
        {
            t = t ?? new HuntEconomyTuning();
            if (continuesUsed < 0) continuesUsed = 0;
            return t.ContinueBase + t.ContinueStep * continuesUsed;
        }

        /// <summary>Display name for a 1-5 toughness band.</summary>
        public static string ToughnessName(int toughness)
        {
            switch (toughness)
            {
                case 1: return "Easy";
                case 2: return "Medium";
                case 3: return "Tricky";
                case 4: return "Hard";
                default: return toughness <= 0 ? "Easy" : "Expert";
            }
        }
    }
}
