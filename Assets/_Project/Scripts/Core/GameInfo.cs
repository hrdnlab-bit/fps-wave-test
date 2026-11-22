namespace HrdnLab.FpsWave.Core
{
    /// <summary>
    /// Informazioni di versione del gioco.
    /// ProtocolVersion va incrementato a ogni modifica incompatibile della rete:
    /// Host e client devono avere lo stesso valore per potersi connettere.
    /// </summary>
    public static class GameInfo
    {
        public const string Version = "0.2.0";
        public const int ProtocolVersion = 3; // 3: tuta, a terra/rianimazione, ping, corazza del Brute; 2: conferma colpo con danno, colore del tracciante
    }
}
