using System.Collections.Generic;

namespace HrdnLab.FpsWave.Stats
{
    /// <summary>
    /// Una statistica con valore base e modificatori.
    /// Formula: finale = clamp((base + ΣAdd) * (1 + ΣAddPercent) * ΠMultiply, min, max).
    /// Il valore è in cache e si ricalcola solo quando cambiano base o modificatori.
    /// </summary>
    public sealed class Stat
    {
        private readonly List<StatModifier> _modifiers = new(4);
        private float _baseValue;
        private float _cachedValue;
        private bool _dirty = true;

        public StatType Type { get; }
        public float Min { get; }
        public float Max { get; }

        public Stat(StatType type, float baseValue, float min = float.MinValue, float max = float.MaxValue)
        {
            Type = type;
            _baseValue = baseValue;
            Min = min;
            Max = max;
        }

        public float BaseValue
        {
            get => _baseValue;
            set
            {
                if (_baseValue == value)
                    return;
                _baseValue = value;
                _dirty = true;
            }
        }

        public int ModifierCount => _modifiers.Count;

        /// <summary>Valore finale con tutti i modificatori applicati.</summary>
        public float Value
        {
            get
            {
                if (_dirty)
                {
                    _cachedValue = Calculate();
                    _dirty = false;
                }
                return _cachedValue;
            }
        }

        public void AddModifier(in StatModifier modifier)
        {
            _modifiers.Add(modifier);
            _dirty = true;
        }

        /// <summary>Rimuove tutti i modificatori applicati da <paramref name="source"/>. Restituisce quanti ne ha rimossi.</summary>
        public int RemoveModifiersFromSource(object source)
        {
            int removed = 0;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_modifiers[i].Source, source))
                {
                    _modifiers.RemoveAt(i);
                    removed++;
                }
            }
            if (removed > 0)
                _dirty = true;
            return removed;
        }

        public void ClearModifiers()
        {
            if (_modifiers.Count == 0)
                return;
            _modifiers.Clear();
            _dirty = true;
        }

        private float Calculate()
        {
            float add = 0f;
            float percent = 0f;
            float multiply = 1f;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                StatModifier m = _modifiers[i];
                switch (m.Op)
                {
                    case ModifierOp.Add: add += m.Value; break;
                    case ModifierOp.AddPercent: percent += m.Value; break;
                    case ModifierOp.Multiply: multiply *= m.Value; break;
                }
            }
            float result = (_baseValue + add) * (1f + percent) * multiply;
            if (result < Min) result = Min;
            if (result > Max) result = Max;
            return result;
        }
    }
}
