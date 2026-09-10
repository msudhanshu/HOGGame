using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class HuntSession
    {
        readonly List<string> _remaining;
        readonly List<string> _found = new List<string>();

        public HuntSession(IReadOnlyList<string> targets)
        {
            if (targets == null || targets.Count == 0)
                throw new ArgumentException("Need at least one target name.", nameof(targets));
            _remaining = new List<string>(targets.Count);
            for (var i = 0; i < targets.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(targets[i]))
                    throw new ArgumentException("Target names cannot be empty.", nameof(targets));
                _remaining.Add(targets[i]);
            }
        }

        public IReadOnlyList<string> Remaining => _remaining;
        public IReadOnlyList<string> Found => _found;
        public bool IsComplete => _remaining.Count == 0;

        public bool TryFind(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            var index = _remaining.IndexOf(name);
            if (index < 0)
                return false;
            _remaining.RemoveAt(index);
            _found.Add(name);
            return true;
        }
    }
}
