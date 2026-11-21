using System.Collections.Generic;

namespace HrdnLab.FpsWave.Stats
{
    /// <summary>
    /// Insieme di statistiche di un'entità (arma, player). I modificatori per statistiche
    /// non presenti nel blocco vengono ignorati, così una mod generica non rompe nulla.
    /// </summary>
    public sealed class StatBlock
    {
        private readonly Dictionary<StatType, Stat> _stats = new();

        public int Version { get; private set; }

        public Stat Define(StatType type, float baseValue, float min = float.MinValue, float max = float.MaxValue)
        {
            var stat = new Stat(type, baseValue, min, max);
            _stats[type] = stat;
            Version++;
            return stat;
        }

        public bool Has(StatType type) => _stats.ContainsKey(type);

        public bool TryGet(StatType type, out Stat stat) => _stats.TryGetValue(type, out stat);

        /// <summary>Valore finale; <paramref name="fallback"/> se la statistica non esiste.</summary>
        public float Get(StatType type, float fallback = 0f) =>
            _stats.TryGetValue(type, out Stat stat) ? stat.Value : fallback;

        public bool AddModifier(in StatModifier modifier)
        {
            if (!_stats.TryGetValue(modifier.Stat, out Stat stat))
                return false;
            stat.AddModifier(modifier);
            Version++;
            return true;
        }

        public int RemoveModifiersFromSource(object source)
        {
            int removed = 0;
            foreach (Stat stat in _stats.Values)
                removed += stat.RemoveModifiersFromSource(source);
            if (removed > 0)
                Version++;
            return removed;
        }
    }
}
