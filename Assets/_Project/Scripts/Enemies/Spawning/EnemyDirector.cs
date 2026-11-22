using System.Collections.Generic;
using FishNet;
using FishNet.Managing;
using FishNet.Object;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Spawn director minimo di M2 (solo server; sui client si disattiva da solo). Vedi SPAWN_DIRECTOR.md.
    /// - Costruisce il NavMesh a runtime dalla geometria Environment (serve solo all'Host).
    /// - Tiene una cache di visibilità dei nodi di spawn, aggiornata con un budget di raycast per frame.
    /// - Ogni tick (0.5 s) decide se servono nemici e spawna un gruppo attorno al player con meno pressione,
    ///   su un nodo nell'anello 25-60 m, non visibile da nessun player e raggiungibile sul NavMesh.
    /// - Spawn scaglionato (max 2 per frame) dal pool di rete; despawn al pool (morti, bloccati, troppo lontani).
    /// - Fa "pensare" i nemici a turno, a frequenza ridotta: nessun Update per nemico.
    /// Finché non c'è M3 (wave) tiene in campo un numero fisso di nemici in base ai player vivi.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDirector : MonoBehaviour
    {
        private struct PendingSpawn
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public EnemyTarget Target;
            public NetworkObject Prefab;
        }

        [Header("Tipi di nemico (Assets/_Project/Data/Enemies)")]
        [Tooltip("Tipi che il director può spawnare; per ogni gruppo ne sceglie uno in base a SpawnWeight.")]
        [SerializeField] private EnemyDefinition[] _enemyTypes = System.Array.Empty<EnemyDefinition>();

        [Header("Quantità (test M2, sostituito dalle wave in M3)")]
        [SerializeField] private bool _spawningEnabled = true;
        [SerializeField, Min(0)] private int _baseAlive = 4;
        [SerializeField, Min(0)] private int _alivePerPlayer = 4;
        [SerializeField, Min(1)] private int _hardCap = 40;
        [SerializeField, Min(0f)] private float _startDelay = 4f;
        [SerializeField, Min(0.1f)] private float _groupCooldown = 2.5f;
        [SerializeField, Min(0.1f)] private float _tickInterval = 0.5f;
        [SerializeField] private Key _toggleKey = Key.F8;

        [Header("Scelta dei punti (SPAWN_DIRECTOR.md §8)")]
        [SerializeField, Min(0f)] private float _ringMin = SpawnScoring.DefaultRingMin;
        [SerializeField, Min(1f)] private float _ringMax = SpawnScoring.DefaultRingMax;
        [SerializeField, Min(0f)] private float _minDistance = SpawnScoring.DefaultMinDistance;
        [SerializeField, Min(1f)] private float _idealDistance = SpawnScoring.DefaultIdealDistance;
        [SerializeField, Min(1)] private int _raycastsPerFrame = 32;
        [SerializeField, Min(0.1f)] private float _visibilityMaxAge = 1.2f;
        [SerializeField, Min(1)] private int _maxSpawnsPerFrame = 2;
        [SerializeField, Min(1)] private int _maxThinksPerFrame = 16;
        [SerializeField, Min(10f)] private float _recycleDistance = 90f;

        [Header("NavMesh (costruito a runtime, solo server)")]
        [SerializeField] private Vector3 _navMeshCenter = new(0f, 5f, 0f);
        [SerializeField] private Vector3 _navMeshSize = new(166f, 24f, 166f);
        [SerializeField, Min(0.05f)] private float _navAgentRadius = 0.4f;
        [SerializeField, Min(0.5f)] private float _navAgentHeight = 1.8f;

        [Header("Debug")]
        [SerializeField] private bool _showOverlay = true;

        private NetworkManager _networkManager;
        private NavMeshDataInstance _navMeshInstance;
        private NavMeshPath _path;

        private Vector3[] _nodePositions = System.Array.Empty<Vector3>();
        private bool[] _nodeReachable = System.Array.Empty<bool>();
        private bool[] _nodeVisible = System.Array.Empty<bool>();
        private float[] _nodeCheckedAt = System.Array.Empty<float>();
        private float[] _nodeLastUsed = System.Array.Empty<float>();
        private int _visibilityCursor;

        private readonly List<EnemyAgent> _active = new(64);
        private readonly Queue<PendingSpawn> _pending = new(16);
        private readonly int[] _topIds = new int[SpawnScoring.TopCount];
        private readonly float[] _topScores = new float[SpawnScoring.TopCount];
        private int[] _pressure = new int[8];
        private float[] _typeWeights = System.Array.Empty<float>();

        private float _nextTick;
        private float _nextGroup;
        private int _thinkCursor;
        private int _spawnedTotal;
        private int _recycledTotal;
        private int _killedTotal;
        private string _overlayText = string.Empty;
        private float _nextOverlayRefresh;

        public int AliveCount => _active.Count;
        public bool SpawningEnabled => _spawningEnabled;

        // ------------------------------------------------------------------ Ciclo di vita

        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager == null || !_networkManager.IsServerStarted)
            {
                enabled = false; // client puro: niente director, niente NavMesh
                return;
            }
            if (!HasSpawnableType())
            {
                Debug.LogError("[FpsWave] EnemyDirector: nessun tipo di nemico con prefab assegnato (eseguire FpsWave/Setup/6).");
                enabled = false;
                return;
            }

            _path = new NavMeshPath();
            BuildNavMesh();
            InitNodes();
            _typeWeights = new float[_enemyTypes.Length];
            for (int i = 0; i < _enemyTypes.Length; i++)
            {
                EnemyDefinition type = _enemyTypes[i];
                if (type != null && type.Prefab != null && type.PoolPrewarm > 0)
                    _networkManager.CacheObjects(type.Prefab, type.PoolPrewarm, asServer: true);
            }

            _nextTick = Time.time + _startDelay;
            _nextGroup = _nextTick;
            Debug.Log($"[FpsWave][Server] EnemyDirector pronto: {_enemyTypes.Length} tipi di nemico, {_nodePositions.Length} nodi di spawn, F8 attiva/disattiva gli spawn.");
        }

        private void OnDestroy()
        {
            _active.Clear();
            _pending.Clear();
            if (_navMeshInstance.valid)
                NavMesh.RemoveNavMeshData(_navMeshInstance);
        }

        private void Update()
        {
            float now = Time.time;
            List<EnemyTarget> targets = EnemyTarget.Active;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_toggleKey].wasPressedThisFrame)
                ToggleSpawning();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Strumenti di prova (solo Editor/dev): K uccide il nemico più vicino al giocatore con un colpo su una
            // parte del corpo a caso (morti e smembramento), J lo colpisce a un arto senza ucciderlo (arti che si
            // staccano a forza di colpi), L agli stinchi (sx e dx a turno: senza gambe striscia), F10 nemici senza danno.
            if (keyboard != null && keyboard.kKey.wasPressedThisFrame)
                DebugHitNearest(true);
            if (keyboard != null && keyboard.jKey.wasPressedThisFrame)
                DebugHitNearest(false);
            if (keyboard != null && keyboard.lKey.wasPressedThisFrame)
                DebugHitNearest(false, true);
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame)
            {
                EnemyAgent.DebugNoDamage = !EnemyAgent.DebugNoDamage;
                Debug.Log($"[FpsWave][Server] Danno dei nemici {(EnemyAgent.DebugNoDamage ? "SPENTO" : "acceso")} (F10).");
            }
#endif

            UpdateVisibility(targets, now);
            ProcessPendingSpawns();
            ThinkAgents(targets, now);

            if (now >= _nextTick)
            {
                _nextTick = now + _tickInterval;
                DirectorTick(targets, now);
            }
        }

        // ------------------------------------------------------------------ NavMesh e nodi

        private void BuildNavMesh()
        {
            var bounds = new Bounds(_navMeshCenter, _navMeshSize);
            var sources = new List<NavMeshBuildSource>(256);
            var markups = new List<NavMeshBuildMarkup>();
            NavMeshBuilder.CollectSources(bounds, 1 << GameLayers.Environment, NavMeshCollectGeometry.PhysicsColliders,
                0, markups, sources);

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = _navAgentRadius;
            settings.agentHeight = _navAgentHeight;
            settings.agentClimb = 0.45f;
            settings.agentSlope = 45f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = _navAgentRadius / 2f;

            float started = Time.realtimeSinceStartup;
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogError("[FpsWave][Server] Costruzione del NavMesh fallita.");
                return;
            }
            _navMeshInstance = NavMesh.AddNavMeshData(data);
            Debug.Log($"[FpsWave][Server] NavMesh costruito da {sources.Count} sorgenti in {(Time.realtimeSinceStartup - started) * 1000f:0} ms.");
        }

        private void InitNodes()
        {
            // Una volta all'avvio: la ricerca può allocare.
            EnemySpawnNode[] nodes = GetComponentsInChildren<EnemySpawnNode>(true);
            int count = nodes.Length;
            _nodePositions = new Vector3[count];
            _nodeReachable = new bool[count];
            _nodeVisible = new bool[count];
            _nodeCheckedAt = new float[count];
            _nodeLastUsed = new float[count];
            int unreachable = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = nodes[i].transform.position;
                bool onMesh = NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas);
                _nodePositions[i] = onMesh ? hit.position : p;
                _nodeReachable[i] = onMesh;
                _nodeVisible[i] = true; // finché non viene controllato vale come visibile
                _nodeCheckedAt[i] = float.NegativeInfinity;
                _nodeLastUsed[i] = float.NegativeInfinity;
                if (!onMesh)
                    unreachable++;
            }
            if (unreachable > 0)
                Debug.LogWarning($"[FpsWave][Server] {unreachable} nodi di spawn fuori dal NavMesh: ignorati.");
        }

        // ------------------------------------------------------------------ Visibilità (budget di raycast)

        /// <summary>
        /// Aggiorna a rotazione la visibilità dei nodi: un nodo è visibile se da almeno un player vivo si vede
        /// la sua parte alta o bassa (testa e gambe di un nemico lì). Massimo <see cref="_raycastsPerFrame"/> raggi per frame.
        /// I nodi fuori portata o troppo vicini ai player si scartano senza raggi.
        /// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Punti nello spazio del nemico (+Z davanti): 0 testa, 1-2 busto, 3-6 braccia, 7-10 gambe.
        private static readonly Vector3[] DebugHitPoints =
        {
            new(0f, 1.62f, 0.15f), new(0f, 1.2f, 0.2f), new(0.05f, 1.05f, 0.18f),
            new(-0.25f, 1.3f, 0.05f), new(0.25f, 1.3f, 0.05f), new(-0.24f, 1.05f, 0.32f), new(0.24f, 1.05f, 0.32f),
            new(-0.12f, 0.7f, 0.05f), new(0.12f, 0.7f, 0.05f), new(-0.12f, 0.3f, 0.05f), new(0.12f, 0.3f, 0.05f),
        };

        private EnemyAgent _debugTarget;

        private int _debugLeg;

        private void DebugHitNearest(bool kill, bool legs = false)
        {
            // Vicino al primo giocatore (la camera del player non è per forza taggata MainCamera).
            List<EnemyTarget> targets = EnemyTarget.Active;
            if (targets.Count == 0 || targets[0] == null)
                return;
            Vector3 from = targets[0].Position;
            EnemyAgent best = null;
            float bestDist = float.MaxValue;
            Health bestHealth = null;
            foreach (EnemyAgent enemy in _active)
            {
                if (enemy == null || !enemy.TryGetComponent(out Health health) || health.IsDead)
                    continue;
                float d = (enemy.transform.position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = enemy; bestHealth = health; }
            }
            // J resta sullo stesso nemico finché è vivo: così gli arti si staccano davvero (danno accumulato).
            if (_debugTarget != null && _debugTarget.isActiveAndEnabled && !_debugTarget.ClientIsDead
                && _debugTarget.TryGetComponent(out Health kept) && !kept.IsDead)
            {
                best = _debugTarget;
                bestHealth = kept;
            }
            if (best == null)
                return;
            _debugTarget = kill ? null : best;
            int pick = legs ? 9 + (_debugLeg++ & 1)
                : kill ? UnityEngine.Random.Range(0, DebugHitPoints.Length) : UnityEngine.Random.Range(3, DebugHitPoints.Length);
            Vector3 point = best.transform.TransformPoint(DebugHitPoints[pick]);
            float amount = kill ? 99999f : bestHealth.Max * (legs ? 0.38f : 0.2f);
            bestHealth.ApplyDamage(new DamageInfo(amount, false, pick == 0, -1, point));
            Debug.Log($"[FpsWave][Server] {(kill ? "K" : "J")}: {best.name} colpito al punto {pick}, arti persi 0x{best.LostLimbs:X}.");
        }
#endif

        private void UpdateVisibility(List<EnemyTarget> targets, float now)
        {
            int count = _nodePositions.Length;
            if (count == 0 || CountAlive(targets) == 0)
                return;

            float farSqr = (_ringMax * 1.6f + 5f) * (_ringMax * 1.6f + 5f);
            float nearSqr = _minDistance * _minDistance;
            int budget = _raycastsPerFrame;
            int guard = count;
            while (budget > 0 && guard-- > 0)
            {
                int i = _visibilityCursor;
                _visibilityCursor = (_visibilityCursor + 1) % count;
                if (!_nodeReachable[i])
                    continue;

                float nearest = NearestAlivePlayerSqr(_nodePositions[i], targets);
                if (nearest > farSqr)
                {
                    _nodeCheckedAt[i] = float.NegativeInfinity; // non candidabile: nessun raggio
                    continue;
                }
                if (nearest < nearSqr)
                {
                    _nodeVisible[i] = true;
                    _nodeCheckedAt[i] = now;
                    continue;
                }
                _nodeVisible[i] = IsNodeVisible(i, targets, ref budget);
                _nodeCheckedAt[i] = now;
            }
        }

        private bool IsNodeVisible(int node, List<EnemyTarget> targets, ref int budget)
        {
            Vector3 high = _nodePositions[node] + Vector3.up * 1.7f;
            Vector3 low = _nodePositions[node] + Vector3.up * 0.5f;
            for (int p = 0; p < targets.Count; p++)
            {
                EnemyTarget t = targets[p];
                if (t == null || !t.IsAlive)
                    continue;
                Vector3 eye = t.EyePosition;
                budget -= 2;
                if (!Physics.Linecast(eye, high, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                    return true;
                if (!Physics.Linecast(eye, low, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ Tick del director

        private void DirectorTick(List<EnemyTarget> targets, float now)
        {
            // Manutenzione: morti al pool dopo la posa a terra, riciclo dei bloccati e dei troppo lontani.
            float recycleSqr = _recycleDistance * _recycleDistance;
            int living = 0;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                EnemyAgent enemy = _active[i];
                if (enemy == null || !enemy.IsSpawned)
                {
                    RemoveActiveAt(i);
                    continue;
                }
                if (enemy.IsDead)
                {
                    if (now >= enemy.DespawnAt)
                    {
                        _killedTotal++;
                        DespawnAt(i);
                    }
                    continue;
                }
                if (enemy.WantsRecycle || (CountAlive(targets) > 0 && NearestAlivePlayerSqr(enemy.transform.position, targets) > recycleSqr))
                {
                    _recycledTotal++;
                    DespawnAt(i); // il director lo rimpiazza vicino ai player nei tick successivi
                    continue;
                }
                living++;
            }

            if (!_spawningEnabled || _nodePositions.Length == 0)
                return;
            int alivePlayers = CountAlive(targets);
            if (alivePlayers == 0)
                return;

            int wanted = SpawnScoring.TargetAlive(alivePlayers, _baseAlive, _alivePerPlayer, _hardCap);
            int planned = living + _pending.Count;
            if (planned >= wanted || now < _nextGroup)
                return;

            EnemyTarget victim = LeastPressuredPlayer(targets);
            EnemyDefinition type = PickEnemyType();
            if (victim == null || type == null)
                return;
            int size = Mathf.Min(Random.Range(type.GroupMin, type.GroupMax + 1), wanted - planned);
            _nextGroup = TryQueueGroup(type, victim, size, targets, now) ? now + _groupCooldown : now + _tickInterval;
        }

        /// <summary>Pressione per giocatore = nemici che lo stanno inseguendo; i nuovi gruppi vanno a chi ne ha meno.</summary>
        private EnemyTarget LeastPressuredPlayer(List<EnemyTarget> targets)
        {
            if (_pressure.Length < targets.Count)
                _pressure = new int[targets.Count * 2];
            for (int p = 0; p < targets.Count; p++)
                _pressure[p] = 0;
            for (int i = 0; i < _active.Count; i++)
            {
                EnemyTarget chased = _active[i] != null ? _active[i].Target : null;
                if (chased == null)
                    continue;
                int index = targets.IndexOf(chased);
                if (index >= 0)
                    _pressure[index]++;
            }

            EnemyTarget best = null;
            int bestPressure = int.MaxValue;
            for (int p = 0; p < targets.Count; p++)
            {
                EnemyTarget t = targets[p];
                if (t == null || !t.IsAlive)
                    continue;
                if (_pressure[p] < bestPressure || (_pressure[p] == bestPressure && Random.value < 0.5f))
                {
                    best = t;
                    bestPressure = _pressure[p];
                }
            }
            return best;
        }

        /// <summary>Un tipo per gruppo (gruppi omogenei, più credibili), scelto a caso in base a SpawnWeight.</summary>
        private EnemyDefinition PickEnemyType()
        {
            for (int i = 0; i < _enemyTypes.Length; i++)
            {
                EnemyDefinition type = _enemyTypes[i];
                _typeWeights[i] = type != null && type.Prefab != null ? type.SpawnWeight : 0f;
            }
            int index = SpawnScoring.PickByWeight(_typeWeights, _typeWeights.Length, Random.value);
            return index >= 0 ? _enemyTypes[index] : null;
        }

        private bool HasSpawnableType()
        {
            for (int i = 0; i < _enemyTypes.Length; i++)
                if (_enemyTypes[i] != null && _enemyTypes[i].Prefab != null && _enemyTypes[i].SpawnWeight > 0f)
                    return true;
            return false;
        }

        private bool TryQueueGroup(EnemyDefinition type, EnemyTarget victim, int size, List<EnemyTarget> targets, float now)
        {
            if (size <= 0)
                return false;
            int node = SelectNode(victim, targets, now);
            if (node < 0)
                return false;

            // Conferma finale: la cache può essere vecchia di un secondo.
            int confirmBudget = int.MaxValue;
            if (IsNodeVisible(node, targets, ref confirmBudget))
            {
                _nodeVisible[node] = true;
                _nodeCheckedAt[node] = now;
                return false;
            }

            Vector3 center = _nodePositions[node];
            Vector3 toVictim = victim.Position - center;
            toVictim.y = 0f;
            Quaternion rotation = toVictim.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toVictim) : Quaternion.identity;
            for (int k = 0; k < size; k++)
            {
                Vector3 p = center;
                if (k > 0)
                {
                    float angle = k * (Mathf.PI * 2f / size);
                    Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.3f;
                    if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.2f, NavMesh.AllAreas))
                        p = hit.position;
                }
                _pending.Enqueue(new PendingSpawn { Position = p, Rotation = rotation, Target = victim, Prefab = type.Prefab });
            }
            _nodeLastUsed[node] = now;
            return true;
        }

        /// <summary>Nodo migliore per un gruppo diretto a <paramref name="victim"/>, oppure -1 (si ritenta al tick dopo).</summary>
        private int SelectNode(EnemyTarget victim, List<EnemyTarget> targets, float now)
        {
            int topCount = CollectCandidates(victim, targets, now, _ringMin, _ringMax);
            if (topCount == 0)
                topCount = CollectCandidates(victim, targets, now, _minDistance, _ringMax * 1.6f); // ripiego: nascosto ma più lontano

            Vector3 victimPosition = victim.Position;
            bool victimOnMesh = NavMesh.SamplePosition(victimPosition, out NavMeshHit victimHit, 3f, NavMesh.AllAreas);
            while (topCount > 0)
            {
                int pick = SpawnScoring.PickWeighted(_topScores, topCount, Random.value);
                int node = _topIds[pick];
                if (!victimOnMesh)
                    return node; // player in salto o su un riparo: si accetta senza verifica del percorso
                if (NavMesh.CalculatePath(_nodePositions[node], victimHit.position, NavMesh.AllAreas, _path)
                    && _path.status == NavMeshPathStatus.PathComplete)
                    return node;
                SpawnScoring.RemoveTopAt(_topIds, _topScores, ref topCount, pick);
            }
            return -1;
        }

        private int CollectCandidates(EnemyTarget victim, List<EnemyTarget> targets, float now, float ringMin, float ringMax)
        {
            int topCount = 0;
            Vector3 victimPosition = victim.Position;
            Vector3 forward = victim.Forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                forward.Normalize();

            for (int n = 0; n < _nodePositions.Length; n++)
            {
                if (!_nodeReachable[n] || _nodeVisible[n] || now - _nodeCheckedAt[n] > _visibilityMaxAge)
                    continue;
                Vector3 toNode = _nodePositions[n] - victimPosition;
                toNode.y = 0f;
                float distance = toNode.magnitude;
                float nearest = Mathf.Sqrt(NearestAlivePlayerSqr(_nodePositions[n], targets));
                if (!SpawnScoring.IsInRing(distance, nearest, ringMin, ringMax, _minDistance))
                    continue;
                float facing = distance > 0.01f ? Vector3.Dot(forward, toNode / distance) : 0f;
                float penalty = SpawnScoring.RecentUsePenalty(now - _nodeLastUsed[n]);
                SpawnScoring.InsertTop(_topIds, _topScores, ref topCount, n,
                    SpawnScoring.Score(distance, facing, penalty, _idealDistance));
            }
            return topCount;
        }

        // ------------------------------------------------------------------ Spawn e despawn (pool di rete)

        private void ProcessPendingSpawns()
        {
            int spawned = 0;
            while (_pending.Count > 0 && spawned < _maxSpawnsPerFrame && _active.Count < _hardCap)
            {
                PendingSpawn request = _pending.Dequeue();
                if (request.Target == null || !request.Target.IsAlive)
                    continue; // il bersaglio è morto o uscito: il tick successivo riprogramma
                NetworkObject nob = _networkManager.GetPooledInstantiated(request.Prefab, request.Position, request.Rotation, asServer: true);
                if (nob == null)
                    break;
                _networkManager.ServerManager.Spawn(nob, null, gameObject.scene);
                if (!nob.TryGetComponent(out EnemyAgent enemy))
                {
                    Debug.LogError("[FpsWave][Server] Il prefab del nemico non ha EnemyAgent.");
                    _networkManager.ServerManager.Despawn(nob, DespawnType.Pool);
                    break;
                }
                enemy.ServerAssignTarget(request.Target);
                _active.Add(enemy);
                spawned++;
                _spawnedTotal++;
            }
        }

        private void DespawnAt(int index)
        {
            EnemyAgent enemy = _active[index];
            RemoveActiveAt(index);
            if (enemy != null && enemy.IsSpawned)
                _networkManager.ServerManager.Despawn(enemy.NetworkObject, DespawnType.Pool);
        }

        private void RemoveActiveAt(int index)
        {
            int last = _active.Count - 1;
            _active[index] = _active[last];
            _active.RemoveAt(last);
        }

        private void ToggleSpawning()
        {
            _spawningEnabled = !_spawningEnabled;
            if (!_spawningEnabled)
            {
                _pending.Clear();
                for (int i = _active.Count - 1; i >= 0; i--)
                    DespawnAt(i);
            }
            else
            {
                _nextGroup = Time.time + 1f;
            }
            Debug.Log($"[FpsWave][Server] Spawn dei nemici {(_spawningEnabled ? "attivi" : "disattivati")}.");
        }

        // ------------------------------------------------------------------ AI a turno

        private void ThinkAgents(List<EnemyTarget> targets, float now)
        {
            int count = _active.Count;
            if (count == 0)
                return;
            int thinks = 0;
            for (int step = 0; step < count && thinks < _maxThinksPerFrame; step++)
            {
                if (_thinkCursor >= count)
                    _thinkCursor = 0;
                EnemyAgent enemy = _active[_thinkCursor++];
                if (enemy == null || enemy.IsDead || now < enemy.NextThink)
                    continue;
                enemy.ServerThink(now, targets);
                thinks++;
            }
        }

        // ------------------------------------------------------------------ Utilità

        private static int CountAlive(List<EnemyTarget> targets)
        {
            int alive = 0;
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null && targets[i].IsAlive)
                    alive++;
            return alive;
        }

        private static float NearestAlivePlayerSqr(Vector3 position, List<EnemyTarget> targets)
        {
            float best = float.MaxValue;
            for (int i = 0; i < targets.Count; i++)
            {
                EnemyTarget t = targets[i];
                if (t == null || !t.IsAlive)
                    continue;
                float sqr = (t.Position - position).sqrMagnitude;
                if (sqr < best)
                    best = sqr;
            }
            return best;
        }

        // ------------------------------------------------------------------ Debug

        private void OnGUI()
        {
            if (!_showOverlay)
                return;
            float now = Time.unscaledTime;
            if (now >= _nextOverlayRefresh)
            {
                _nextOverlayRefresh = now + 0.5f;
                int wanted = SpawnScoring.TargetAlive(CountAlive(EnemyTarget.Active), _baseAlive, _alivePerPlayer, _hardCap);
                _overlayText = $"Enemies {_active.Count}/{wanted}  kills {_killedTotal}  recycled {_recycledTotal}  " +
                               $"spawn {(_spawningEnabled ? "ON" : "OFF")} [{_toggleKey}]";
            }
            GUI.Label(new Rect(10f, Screen.height - 28f, 600f, 22f), _overlayText);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
                return;
            for (int i = 0; i < _nodePositions.Length; i++)
            {
                Gizmos.color = !_nodeReachable[i] ? Color.gray : _nodeVisible[i] ? Color.red : Color.green;
                Gizmos.DrawSphere(_nodePositions[i] + Vector3.up * 0.3f, 0.35f);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(EnemyDefinition[] enemyTypes, Vector3 navMeshCenter, Vector3 navMeshSize)
        {
            _enemyTypes = enemyTypes;
            _navMeshCenter = navMeshCenter;
            _navMeshSize = navMeshSize;
        }
#endif
    }
}
