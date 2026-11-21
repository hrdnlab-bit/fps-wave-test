namespace HrdnLab.FpsWave.Combat
{
    /// <summary>
    /// Bersaglio in movimento: il server usa la sua velocità per allargare la tolleranza
    /// della validazione dei colpi (il client vede la posizione interpolata, in ritardo).
    /// </summary>
    public interface IMovingTarget
    {
        /// <summary>Velocità attuale vista dal server, in m/s.</summary>
        float ServerSpeed { get; }
    }
}
