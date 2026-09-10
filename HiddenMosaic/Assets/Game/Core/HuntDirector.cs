using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class HuntDirector
    {
        readonly HuntCatalog _catalog;
        readonly List<string> _pool = new List<string>();
        int _nextName;
        HuntSession _session;

        public HuntDirector(HuntCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public HuntCatalog Catalog => _catalog;
        public HuntLevelDef Current { get; private set; }
        public HuntSession Session => _session;
        public int LevelIndex { get; private set; }
        public int WaveIndex { get; private set; }
        public int WaveCount { get; private set; }
        public bool IsLevelComplete { get; private set; }
        public bool IsCampaignComplete { get; private set; }
        public string LastFindHint { get; private set; }

        public bool HasNextLevel => LevelIndex + 1 < _catalog.Count;

        public void BeginLevel(int index, IReadOnlyList<string> availableNames)
        {
            if (index < 0 || index >= _catalog.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (availableNames == null || availableNames.Count == 0)
                throw new ArgumentException("Need stamp names from the mosaic.", nameof(availableNames));

            Current = _catalog[index];
            LevelIndex = index;
            IsLevelComplete = false;
            IsCampaignComplete = false;
            LastFindHint = "";
            _pool.Clear();
            for (var i = 0; i < availableNames.Count; i++)
            {
                var name = availableNames[i];
                if (string.IsNullOrWhiteSpace(name) || _pool.Contains(name))
                    continue;
                _pool.Add(name);
            }

            if (_pool.Count == 0)
                throw new ArgumentException("Need stamp names from the mosaic.", nameof(availableNames));

            var maxWaves = (_pool.Count + Current.WaveSize - 1) / Current.WaveSize;
            WaveCount = Current.WaveCount < maxWaves ? Current.WaveCount : maxWaves;
            if (WaveCount < 1)
                WaveCount = 1;
            _nextName = 0;
            WaveIndex = 0;
            _session = new HuntSession(TakeWave());
        }

        public bool TryFind(string name)
        {
            if (_session == null || IsLevelComplete)
                return false;
            if (!_session.TryFind(name))
                return false;

            LastFindHint = name;
            if (!_session.IsComplete)
                return true;

            if (WaveIndex + 1 < WaveCount)
            {
                WaveIndex++;
                _session = new HuntSession(TakeWave());
                return true;
            }

            IsLevelComplete = true;
            IsCampaignComplete = !HasNextLevel;
            return true;
        }

        public bool TryNextLevel(IReadOnlyList<string> availableNames)
        {
            if (!IsLevelComplete || !HasNextLevel)
                return false;
            BeginLevel(LevelIndex + 1, availableNames);
            return true;
        }

        List<string> TakeWave()
        {
            var take = Current.WaveSize;
            if (take > _pool.Count - _nextName)
                take = _pool.Count - _nextName;
            if (take < 1)
                take = 1;
            var names = new List<string>(take);
            for (var i = 0; i < take; i++)
            {
                if (_nextName >= _pool.Count)
                    _nextName = 0;
                names.Add(_pool[_nextName]);
                _nextName++;
            }

            return names;
        }
    }
}
