using System;
using HrdnLab.FpsWave.Audio;

namespace HrdnLab.FpsWave.UI
{
    public enum CalloutKind : byte
    {
        Info,
        Warning,
        Positive,
    }

    /// <summary>
    /// "Voce" dell'IA della tuta, solo per il player locale: una riga breve sotto la bussola con un segnale sonoro.
    /// Chiunque può chiamare <see cref="Raise"/> (abilità, salute, rianimazione); il HUD ascolta <see cref="Raised"/>.
    /// Pronta per le battute registrate: basterà associare un clip a ogni messaggio.
    /// </summary>
    public static class SuitCallouts
    {
        public static event Action<string, CalloutKind> Raised;

        public static void Raise(string text, CalloutKind kind = CalloutKind.Info)
        {
            Raised?.Invoke(text, kind);
            GameAudio.Play2D(kind == CalloutKind.Warning ? SoundId.SuitAlarm : SoundId.SuitCallout);
        }

        /// <summary>Come <see cref="Raise"/> ma senza segnale sonoro (quando il chiamante ha già il suo suono).</summary>
        public static void Show(string text, CalloutKind kind = CalloutKind.Info) => Raised?.Invoke(text, kind);
    }
}
