namespace HrdnLab.FpsWave.Stats
{
    /// <summary>Come un modificatore si combina con il valore base (vedi docs/MOD_SYSTEM.md).</summary>
    public enum ModifierOp : byte
    {
        /// <summary>Somma piatta al valore base.</summary>
        Add = 0,
        /// <summary>Percentuali sommate tra loro: 0.25 = +25%.</summary>
        AddPercent = 1,
        /// <summary>Moltiplicatori applicati in catena: 1.5 = x1.5.</summary>
        Multiply = 2,
    }

    /// <summary>
    /// Modificatore immutabile. <see cref="Source"/> identifica chi l'ha applicato
    /// (mod, upgrade, mutazione) per poterlo rimuovere in blocco.
    /// </summary>
    public readonly struct StatModifier
    {
        public readonly StatType Stat;
        public readonly ModifierOp Op;
        public readonly float Value;
        public readonly object Source;

        public StatModifier(StatType stat, ModifierOp op, float value, object source)
        {
            Stat = stat;
            Op = op;
            Value = value;
            Source = source;
        }

        public override string ToString() => $"{Stat} {Op} {Value}";
    }
}
