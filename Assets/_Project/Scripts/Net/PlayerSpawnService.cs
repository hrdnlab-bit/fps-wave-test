using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using HrdnLab.FpsWave.Player;
using UnityEngine;

namespace HrdnLab.FpsWave.Net
{
    /// <summary>
    /// Solo server: quando un client ha caricato le scene di gioco gli crea il player,
    /// assegnandogli i punti di spawn della scena a rotazione.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnService : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private NetworkObject _playerPrefab;

        private int _nextSpawn;

        private void Start()
        {
            if (_networkManager == null)
                _networkManager = GetComponent<NetworkManager>();
            _networkManager.SceneManager.OnClientLoadedStartScenes += HandleClientLoadedStartScenes;
        }

        private void OnDestroy()
        {
            if (_networkManager != null && _networkManager.SceneManager != null)
                _networkManager.SceneManager.OnClientLoadedStartScenes -= HandleClientLoadedStartScenes;
        }

        private void HandleClientLoadedStartScenes(NetworkConnection connection, bool asServer)
        {
            if (!asServer)
                return;
            if (_playerPrefab == null)
            {
                Debug.LogError("[FpsWave] PlayerSpawnService: prefab del player non assegnato.");
                return;
            }

            // Evento raro (un ingresso): la ricerca dei punti di spawn può allocare.
            PlayerSpawnPoint[] points = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.InstanceID);
            Vector3 position = Vector3.up;
            Quaternion rotation = Quaternion.identity;
            PlayerSpawnPoint point = null;
            if (points.Length > 0)
            {
                point = points[_nextSpawn % points.Length];
                _nextSpawn++;
                position = point.transform.position;
                rotation = point.transform.rotation;
            }

            NetworkObject player = Instantiate(_playerPrefab, position, rotation);
            if (point != null)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(player.gameObject, point.gameObject.scene);
            _networkManager.ServerManager.Spawn(player, connection);
        }

#if UNITY_EDITOR
        public void EditorSetup(NetworkManager networkManager, NetworkObject playerPrefab)
        {
            _networkManager = networkManager;
            _playerPrefab = playerPrefab;
        }
#endif
    }
}
