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
        /// <summary>Roadmap node index to play (see HuntRoadmap). -1 = continue at the current node.</summary>
        public static int NodeIndex { get; private set; } = -1;
        /// <summary>Set when launching an image-HOG scene; read it there for id/title/time limit.</summary>
        public static ImageHogDef ImageHog { get; private set; }

        public static void SetNode(int nodeIndex)
        {
            Mode = HuntLaunchMode.Campaign;
            NodeIndex = nodeIndex < 0 ? -1 : nodeIndex;
            ImageHog = null;
        }

        public static void SetImageHog(int nodeIndex, ImageHogDef def)
        {
            Mode = HuntLaunchMode.Campaign;
            NodeIndex = nodeIndex;
            ImageHog = def;
        }

        public static void ResetToDefaults()
        {
            Mode = HuntLaunchMode.Campaign;
            NodeIndex = -1;
            ImageHog = null;
        }
    }
}
