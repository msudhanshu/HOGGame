namespace Game.Core
{
    public sealed class HuntLevelClock
    {
        float _remaining;
        bool _running;

        public float Remaining => _remaining;
        public bool IsRunning => _running;
        public bool IsExpired => _running && _remaining <= 0f;

        public void Reset(float limitSeconds)
        {
            _remaining = limitSeconds < 0f ? 0f : limitSeconds;
            _running = limitSeconds > 0f;
        }

        public void Stop()
        {
            _running = false;
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
