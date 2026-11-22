namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Comportamento speciale di un tipo di nemico (carica del Brute, attacco a distanza...), solo server.
    /// <see cref="EnemyAgent"/> lo interroga a ogni "pensiero": se prende il controllo, l'AI base si mette da parte.
    /// </summary>
    public interface IEnemyAbility
    {
        /// <summary>True se l'abilità sta agendo (o parte ora) e l'AI base deve saltare questo passo.</summary>
        bool ServerOverrides(float now, EnemyTarget target);

        /// <summary>Velocità attuale imposta dall'abilità (per la tolleranza della validazione dei colpi), 0 se nessuna.</summary>
        float ServerSpeedOverride { get; }
    }
}
