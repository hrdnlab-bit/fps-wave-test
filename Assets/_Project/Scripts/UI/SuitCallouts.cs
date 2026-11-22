using System;

namespace HrdnLab.FpsWave.UI
{
    public enum CalloutKind : byte
    {
        Info,
        Warning,
        Positive,
    }

    /// <summary>
    /// "Voce" dell'IA della tuta, solo per il player locale: una riga breve sotto la bussola.
    /// Chiunque può chiamare <see cref="Raise"/> (abilità, salute, rianimazione); il HUD ascolta <see cref="Raised"/>.
    /// </summary>
    public static class SuitCallouts
    {
        public static event Action<string, CalloutKind> Raised;

        public static void Raise(string text, CalloutKind kind = CalloutKind.Info)
        {
            Raised?.Invoke(text, kind);
        }
    }
}
