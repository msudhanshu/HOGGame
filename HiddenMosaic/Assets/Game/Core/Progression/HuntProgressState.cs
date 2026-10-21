using System.Collections.Generic;
using System.Text;

namespace Game.Core
{
    /// <summary>Player progress: best stars per node id and the furthest unlocked node index.
    /// Serialises to a tiny line format so Core stays free of JSON dependencies.</summary>
    public sealed class HuntProgressState
    {
        readonly Dictionary<string, int> _stars = new Dictionary<string, int>();

        public int HighestUnlocked { get; private set; }
        public IReadOnlyDictionary<string, int> Stars => _stars;

        public int TotalStars
        {
            get
            {
                var total = 0;
                foreach (var pair in _stars)
                    total += pair.Value;
                return total;
            }
        }

        public bool IsCleared(string nodeId) => !string.IsNullOrEmpty(nodeId) && _stars.ContainsKey(nodeId);

        public int StarsFor(string nodeId) =>
            !string.IsNullOrEmpty(nodeId) && _stars.TryGetValue(nodeId, out var s) ? s : 0;

        /// <summary>Keeps the best stars, unlocks the next node. Returns true on first clear.</summary>
        public bool RecordWin(int nodeIndex, string nodeId, int stars)
        {
            if (stars < 0) stars = 0;
            if (stars > 3) stars = 3;
            var first = !IsCleared(nodeId);
            if (!string.IsNullOrEmpty(nodeId) && (first || stars > _stars[nodeId]))
                _stars[nodeId] = stars;
            UnlockUpTo(nodeIndex + 1);
            return first;
        }

        public void UnlockUpTo(int nodeIndex)
        {
            if (nodeIndex > HighestUnlocked)
                HighestUnlocked = nodeIndex;
        }

        public void Clear()
        {
            _stars.Clear();
            HighestUnlocked = 0;
        }

        /// <summary>Line 1 = highest unlocked; then "id\tstars" per line.</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(HighestUnlocked).Append('\n');
            foreach (var pair in _stars)
                sb.Append(pair.Key.Replace('\n', ' ').Replace('\t', ' ')).Append('\t').Append(pair.Value).Append('\n');
            return sb.ToString();
        }

        public static HuntProgressState Deserialize(string data)
        {
            var state = new HuntProgressState();
            if (string.IsNullOrEmpty(data))
                return state;
            var lines = data.Split('\n');
            if (lines.Length > 0 && int.TryParse(lines[0], out var highest) && highest > 0)
                state.HighestUnlocked = highest;
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var tab = line.LastIndexOf('\t');
                if (tab <= 0)
                    continue;
                if (!int.TryParse(line.Substring(tab + 1), out var stars))
                    continue;
                state._stars[line.Substring(0, tab)] = stars < 0 ? 0 : (stars > 3 ? 3 : stars);
            }

            return state;
        }
    }
}
