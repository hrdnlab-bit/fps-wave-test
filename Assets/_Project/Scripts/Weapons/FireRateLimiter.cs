namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>
    /// Limitatore di cadenza lato server a "secchiello di gettoni": tollera che i colpi arrivino
    /// raggruppati per via del jitter di rete, ma impedisce di superare la cadenza media.
    /// </summary>
    public sealed class FireRateLimiter
    {
        private readonly float _capacity;
        private float _shotsPerSecond;
        private float _tokens;
        private double _lastTime;
        private bool _initialized;

        public FireRateLimiter(float shotsPerSecond, float burstCapacity = 3f)
        {
            _shotsPerSecond = shotsPerSecond;
            _capacity = burstCapacity < 1f ? 1f : burstCapacity;
            _tokens = _capacity;
        }

        public float Tokens => _tokens;

        public void SetRate(float shotsPerSecond) => _shotsPerSecond = shotsPerSecond;

        /// <summary>True se un colpo all'istante <paramref name="now"/> (secondi) è consentito; in tal caso consuma un gettone.</summary>
        public bool TryConsume(double now)
        {
            if (!_initialized)
            {
                _lastTime = now;
                _initialized = true;
            }

            double elapsed = now - _lastTime;
            if (elapsed > 0)
            {
                _tokens += (float)(elapsed * _shotsPerSecond);
                if (_tokens > _capacity)
                    _tokens = _capacity;
                _lastTime = now;
            }

            if (_tokens < 1f)
                return false;
            _tokens -= 1f;
            return true;
        }
    }
}
