namespace HrdnLab.FpsWave.Net
{
    /// <summary>
    /// Riconosce se un'istanza dell'Editor è un giocatore virtuale di Multiplayer Play Mode.
    /// Logica pura (solo stringhe) per poterla testare in EditMode.
    /// </summary>
    public static class EditorInstanceInfo
    {
        private const string VirtualProjectMarker = "library/vp/";

        /// <summary>
        /// True se almeno uno dei percorsi indicati sta dentro <c>Library/VP/</c>, la cartella in cui
        /// Multiplayer Play Mode crea i progetti clone. Si controllano più percorsi (Assets e cartella di lavoro)
        /// perché Assets nel clone è un collegamento simbolico e può risolversi sul progetto principale.
        /// </summary>
        public static bool IsVirtualPlayerPath(params string[] paths)
        {
            if (paths == null)
                return false;
            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;
                string normalized = path.Replace('\\', '/').ToLowerInvariant();
                if (!normalized.EndsWith("/"))
                    normalized += "/";
                if (normalized.Contains(VirtualProjectMarker))
                    return true;
            }
            return false;
        }
    }
}
