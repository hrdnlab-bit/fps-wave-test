using FishNet.Object;
using FishNet.Object.Synchronizing;
using HrdnLab.FpsWave.Combat;
using UnityEngine;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Corazza a piastre di un nemico, autorevole sul server: salute di ogni piastra solo sul server,
    /// piastre rotte replicate in una maschera di bit (un byte, max 8 piastre). Ai client arriva solo il cambio di maschera:
    /// nascondono la piastra, spengono il suo collider e fanno partire i detriti.
    /// Riutilizzata dal pool: tutto torna intero in <see cref="OnStartServer"/>.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class EnemyArmor : NetworkBehaviour
    {
        [SerializeField] private ArmorPlate[] _plates = System.Array.Empty<ArmorPlate>();

        private readonly SyncVar<byte> _broken = new();
        private float[] _health = System.Array.Empty<float>();

        public int PlateCount => _plates.Length;

        private void Awake()
        {
            _health = new float[_plates.Length];
            _broken.OnChange += HandleBrokenChanged;
        }

        private void OnDestroy() => _broken.OnChange -= HandleBrokenChanged;

        public override void OnStartServer()
        {
            base.OnStartServer();
            for (int i = 0; i < _plates.Length; i++)
                _health[i] = _plates[i] != null ? _plates[i].MaxHealth : 0f;
            _broken.Value = 0;
            ApplyMask(0);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyMask(_broken.Value); // chi entra in partita vede subito le piastre già rotte, senza effetti
        }

        /// <summary>
        /// Server: un colpo sulla zona <paramref name="zoneIndex"/>. Se è una piastra intera la piastra assorbe:
        /// al corpo arriva solo <paramref name="bodyDamage"/>. Restituisce true se il colpo ha preso una piastra.
        /// </summary>
        [Server]
        public bool ServerAbsorb(int zoneIndex, float damage, out float bodyDamage, out bool broke)
        {
            bodyDamage = damage;
            broke = false;
            for (int i = 0; i < _plates.Length; i++)
            {
                ArmorPlate plate = _plates[i];
                if (plate == null || plate.Zone.Index != zoneIndex)
                    continue;
                if ((_broken.Value & (1 << i)) != 0)
                    return false;
                bodyDamage = ArmorMath.BodyDamage(damage, _health[i], plate.PassThrough, out float remaining, out broke);
                _health[i] = remaining;
                if (broke)
                {
                    _broken.Value = (byte)(_broken.Value | (1 << i));
                    Debug.Log($"[FpsWave][Server] {name}: piastra {plate.name} rotta.");
                }
                return true;
            }
            return false;
        }

        private void HandleBrokenChanged(byte previous, byte next, bool asServer)
        {
            ApplyMask(next);
            if (asServer && IsClientInitialized)
                return; // sull'host gli effetti partono una volta sola (lato client)
            if (!IsClientInitialized)
                return;
            for (int i = 0; i < _plates.Length; i++)
            {
                int bit = 1 << i;
                if ((next & bit) != 0 && (previous & bit) == 0 && _plates[i] != null)
                    _plates[i].PlayBreakFx();
            }
        }

        private void ApplyMask(byte mask)
        {
            for (int i = 0; i < _plates.Length; i++)
                if (_plates[i] != null)
                    _plates[i].SetBroken((mask & (1 << i)) != 0);
        }

#if UNITY_EDITOR
        public void EditorSetup(ArmorPlate[] plates) => _plates = plates;
#endif
    }

    /// <summary>Calcolo del danno sulle piastre (logica pura, coperta da test).</summary>
    public static class ArmorMath
    {
        /// <summary>
        /// La piastra prende il colpo: passa al corpo <paramref name="passThrough"/> del danno; se la piastra si rompe,
        /// anche l'eccedenza oltre la sua salute arriva al corpo.
        /// </summary>
        public static float BodyDamage(float damage, float plateHealth, float passThrough, out float remaining, out bool broke)
        {
            remaining = plateHealth - damage;
            broke = remaining <= 0f;
            float body = damage * passThrough;
            if (broke)
            {
                body += -remaining * (1f - passThrough);
                remaining = 0f;
            }
            return body;
        }
    }
}
