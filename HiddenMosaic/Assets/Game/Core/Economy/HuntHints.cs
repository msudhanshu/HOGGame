namespace Game.Core
{
    /// <summary>The four in-level helpers. Order = dock order. Names / effects are data here so
    /// swapping a hint later is a one-place change.</summary>
    public enum HuntHintKind
    {
        /// <summary>Marks one remaining target.</summary>
        LockOn = 0,
        /// <summary>Flashes the full colour image for a moment.</summary>
        Peek = 1,
        /// <summary>Pings the position of every remaining target.</summary>
        Radar = 2,
        /// <summary>Adds seconds to the clock.</summary>
        FreezeTime = 3
    }

    public static class HuntHints
    {
        public static readonly HuntHintKind[] All =
        {
            HuntHintKind.LockOn, HuntHintKind.Peek, HuntHintKind.Radar, HuntHintKind.FreezeTime
        };

        public static string Name(HuntHintKind kind, HuntEconomyTuning t = null)
        {
            t = t ?? new HuntEconomyTuning();
            switch (kind)
            {
                case HuntHintKind.LockOn: return "LOCK-ON";
                case HuntHintKind.Peek: return "PEEK";
                case HuntHintKind.Radar: return "RADAR";
                default: return "+" + (int)t.FreezeSeconds + "s";
            }
        }

        public static string Description(HuntHintKind kind, HuntEconomyTuning t = null)
        {
            t = t ?? new HuntEconomyTuning();
            switch (kind)
            {
                case HuntHintKind.LockOn: return "Reveals one target";
                case HuntHintKind.Peek: return "Shows the full image for " + (int)t.PeekSeconds + "s";
                case HuntHintKind.Radar: return "Pings every remaining target";
                default: return "Adds " + (int)t.FreezeSeconds + " seconds";
            }
        }

        /// <summary>Hints per shop pack.</summary>
        public const int PackSize = 3;

        /// <summary>Coin price of a <see cref="PackSize"/> pack: 15% under buying singly, rounded to 5.</summary>
        public static int PackPrice(HuntHintKind kind, HuntEconomyTuning t = null)
        {
            var raw = CoinPrice(kind, t) * PackSize * 0.85f;
            return System.Math.Max(5, (int)System.Math.Round(raw / 5f) * 5);
        }

        /// <summary>Coins charged for one use when the player owns none of that hint.</summary>
        public static int CoinPrice(HuntHintKind kind, HuntEconomyTuning t = null)
        {
            t = t ?? new HuntEconomyTuning();
            switch (kind)
            {
                case HuntHintKind.LockOn: return t.HintPriceLockOn;
                case HuntHintKind.Peek: return t.HintPricePeek;
                case HuntHintKind.Radar: return t.HintPriceRadar;
                default: return t.HintPriceFreeze;
            }
        }
    }
}
