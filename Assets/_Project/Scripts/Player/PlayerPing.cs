using System;
using FishNet.Object;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Enemies;
using UnityEngine;

namespace HrdnLab.FpsWave.Player
{
    /// <summary>
    /// Segnalazioni di squadra (tasto centrale del mouse o Z): un punto nel mondo o un nemico, visibili a tutti nel HUD
    /// con la distanza per qualche secondo. Il ping su un nemico lo segue mentre si muove.
    /// Il client invia il punto mirato, il server limita la frequenza e lo inoltra a tutti (≈20 byte per ping).
    /// </summary>
    public sealed class PlayerPing : NetworkBehaviour
    {
        public readonly struct PingInfo
        {
            public readonly int PingerId;
            public readonly Vector3 Point;
            public readonly NetworkObject Target;
            public readonly bool FromLocalPlayer;

            public PingInfo(int pingerId, Vector3 point, NetworkObject target, bool fromLocalPlayer)
            {
                PingerId = pingerId;
                Point = point;
                Target = target;
                FromLocalPlayer = fromLocalPlayer;
            }
        }

        /// <summary>Tutti i client: nuovo ping (sostituisce il ping precedente dello stesso player).</summary>
        public static event Action<PingInfo> ClientPinged;

        [SerializeField, Min(5f)] private float _range = 150f;
        [SerializeField, Min(0.1f)] private float _cooldown = 0.4f;

        private PlayerInputReader _input;
        private PlayerLook _look;
        private float _nextLocal;
        private float _nextServer;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _look = GetComponent<PlayerLook>();
        }

        private void Update()
        {
            if (!IsOwner || _input == null || !_input.PingPressed || Time.time < _nextLocal)
                return;
            Camera cam = _look != null ? _look.Camera : null;
            if (cam == null)
                return;
            _nextLocal = Time.time + _cooldown;
            Ray ray = new(cam.transform.position, cam.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, _range, GameLayers.ShotMask, QueryTriggerInteraction.Ignore))
                return;
            EnemyAgent enemy = hit.collider.GetComponentInParent<EnemyAgent>();
            NetworkObject target = enemy != null && !enemy.ClientIsDead ? enemy.NetworkObject : null;
            RpcPing(hit.point, target);
        }

        [ServerRpc]
        private void RpcPing(Vector3 point, NetworkObject target)
        {
            if (Time.time < _nextServer)
                return;
            _nextServer = Time.time + _cooldown * 0.75f;
            RpcBroadcast(OwnerId, point, target);
        }

        [ObserversRpc]
        private void RpcBroadcast(int pingerId, Vector3 point, NetworkObject target)
        {
            ClientPinged?.Invoke(new PingInfo(pingerId, point, target, IsOwner));
        }
    }
}
