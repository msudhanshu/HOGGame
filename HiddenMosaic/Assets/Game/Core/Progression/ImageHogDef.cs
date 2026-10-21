namespace Game.Core
{
    /// <summary>A real-image hidden-object level that closes each chapter. Plays in the Play scene as a
    /// day/night reveal level when <see cref="RevealSet"/> is set, or in a separate <see cref="Scene"/>
    /// (see HuntImageHogBridge); neither = "coming soon" placeholder.</summary>
    public sealed class ImageHogDef
    {
        public ImageHogDef(string id, string title, string scene = null, float timeLimitSeconds = 180f, int toughness = 1,
            string revealSet = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "image_hog" : id;
            Title = string.IsNullOrWhiteSpace(title) ? "Photo Hunt" : title;
            Scene = scene ?? string.Empty;
            TimeLimitSeconds = timeLimitSeconds < 30f ? 30f : timeLimitSeconds;
            Toughness = toughness < 1 ? 1 : (toughness > 5 ? 5 : toughness);
            RevealSet = revealSet ?? string.Empty;
        }

        public string Id { get; }
        public string Title { get; }
        /// <summary>Scene name in Build Settings. Empty = not shipped yet (placeholder).</summary>
        public string Scene { get; }
        public float TimeLimitSeconds { get; }
        public int Toughness { get; }
        /// <summary>Reveal-set id (RevealSetAsset at Resources/HiddenMosaic/Reveal/&lt;id&gt;/RevealSet). Empty = none.</summary>
        public string RevealSet { get; }
        public bool HasRevealSet => !string.IsNullOrEmpty(RevealSet);
    }
}
