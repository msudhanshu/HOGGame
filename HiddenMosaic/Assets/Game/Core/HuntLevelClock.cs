namespace Game.Core
{
    public sealed class HuntLevelClock
    {
        float _remaining;
        bool _running;

        public float Remaining => _remaining;
        public float Limit { get; private set; }
        public bool IsRunning => _running;
        public bool IsExpired => _running && _remaining <= 0f;
        /// <summary>Remaining / limit in 0..1 (star rating input).</summary>
        public float RemainingFraction => Limit > 0f ? _remaining / Limit : 0f;

        public void Reset(float limitSeconds)
        {
            _remaining = limitSeconds < 0f ? 0f : limitSeconds;
            Limit = _remaining;
            _running = limitSeconds > 0f;
        }

        public void Stop()
        {
            _running = false;
        }

        /// <summary>Continue after time-out: adds seconds and restarts the clock.</summary>
        public void AddTime(float seconds)
        {
            if (seconds <= 0f)
                return;
            _remaining += seconds;
            _running = true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!_running || deltaSeconds <= 0f)
                return;
            _remaining -= deltaSeconds;
            if (_remaining < 0f)
                _remaining = 0f;
        }
    }
}
