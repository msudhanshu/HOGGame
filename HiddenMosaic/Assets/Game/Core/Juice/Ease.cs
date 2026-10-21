using System;

namespace Game.Core
{
    /// <summary>Easing curves for UI juice (t in 0..1).</summary>
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InQuad(float t) => t * t;
        public static float OutCubic(float t) => 1f - (float)Math.Pow(1f - t, 3);
        public static float InOutSine(float t) => -((float)Math.Cos(Math.PI * t) - 1f) / 2f;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static float InBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const double c4 = 2 * Math.PI / 3;
            return (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1);
        }

        /// <summary>0 → 1 → 0 decaying wobble; multiply by an amplitude for punch-scale.</summary>
        public static float Punch(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            return (float)(Math.Sin(t * Math.PI * 3) * (1 - t));
        }
    }
}
