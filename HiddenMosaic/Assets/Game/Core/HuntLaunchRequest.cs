namespace Game.Core
{
    public enum HuntLaunchMode
    {
        Campaign,
        Single
    }

    /// <summary>Passed from menus into the play scene (static session bootstrap).</summary>
    public static class HuntLaunchRequest
    {
        public static HuntLaunchMode Mode { get; private set; } = HuntLaunchMode.Campaign;
        public static int StartLevelIndex { get; private set; }

        public static void SetCampaign(int startLevelIndex = 0)
        {
            Mode = HuntLaunchMode.Campaign;
            StartLevelIndex = startLevelIndex < 0 ? 0 : startLevelIndex;
        }

        public static void SetSingle(int levelIndex)
        {
            Mode = HuntLaunchMode.Single;
            StartLevelIndex = levelIndex < 0 ? 0 : levelIndex;
        }

        public static void ResetToDefaults()
        {
            Mode = HuntLaunchMode.Campaign;
            StartLevelIndex = 0;
        }
    }
}
