using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace HrdnLab.FpsWave.Net
{
    public enum NetMode : byte
    {
        /// <summary>Rete locale / stesso PC (Tugboat): test con Multiplayer Play Mode e LAN.</summary>
        Lan = 0,
        /// <summary>Steam (FishySteamworks, relay Steam): partite con amici via Internet.</summary>
        Steam = 1,
    }

    /// <summary>
    /// Avvio e arresto della rete con Multipass (Tugboat + FishySteamworks sullo stesso NetworkManager).
    /// Multipass ha le azioni server globali disattivate, così si avvia solo il transport scelto: per questo
    /// server e client vanno sempre avviati e fermati da qui, non con ServerManager.Start/StopConnection.
    /// </summary>
    public static class NetTransports
    {
        public static Multipass GetMultipass(NetworkManager nm) =>
            nm != null && nm.TransportManager != null ? nm.TransportManager.Transport as Multipass : null;

        /// <summary>Indice del transport per la modalità richiesta, -1 se non configurato.</summary>
        public static int IndexOf(NetworkManager nm, NetMode mode)
        {
            Multipass mp = GetMultipass(nm);
            if (mp == null)
                return mode == NetMode.Lan ? 0 : -1;
            for (int i = 0; i < mp.Transports.Count; i++)
            {
                Transport t = mp.Transports[i];
                bool match = mode == NetMode.Lan ? t is Tugboat : t is FishySteamworks.FishySteamworks;
                if (match) return i;
            }
            return -1;
        }

        /// <summary>Avvia il server sul transport della modalità; false se non disponibile o fallito subito.</summary>
        public static bool StartServer(NetworkManager nm, NetMode mode, ushort port)
        {
            Multipass mp = GetMultipass(nm);
            if (mp == null)
            {
                nm.TransportManager.Transport.SetPort(port);
                return nm.ServerManager.StartConnection();
            }
            int index = IndexOf(nm, mode);
            if (index < 0)
            {
                Debug.LogError($"[FpsWave] Transport per {mode} non configurato in Multipass (esegui FpsWave/Setup/5).");
                return false;
            }
            mp.SetPort(port, index);
            return mp.StartConnection(true, index);
        }

        /// <summary>Avvia il client verso <paramref name="address"/> (IP per LAN, SteamID64 dell'Host per Steam).</summary>
        public static bool StartClient(NetworkManager nm, NetMode mode, string address, ushort port)
        {
            Multipass mp = GetMultipass(nm);
            if (mp != null)
            {
                int index = IndexOf(nm, mode);
                if (index < 0)
                    return false;
                mp.SetClientTransport(index);
                mp.SetPort(port, index);
            }
            return nm.ClientManager.StartConnection(address, port);
        }

        /// <summary>Chiude client e server su tutti i transport (anche all'uscita dal Play in Editor).</summary>
        public static void StopAll(NetworkManager nm)
        {
            if (nm == null || nm.ClientManager == null || nm.ServerManager == null)
                return;
            if (nm.ClientManager.Started)
                nm.ClientManager.StopConnection();

            Multipass mp = GetMultipass(nm);
            if (mp == null)
            {
                if (nm.ServerManager.Started)
                    nm.ServerManager.StopConnection(true);
                return;
            }
            for (int i = 0; i < mp.Transports.Count; i++)
            {
                if (mp.GetConnectionState(true, i) != LocalConnectionState.Stopped)
                    mp.StopServerConnection(true, i);
            }
        }
    }
}
