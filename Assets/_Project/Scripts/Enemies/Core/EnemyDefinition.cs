using FishNet.Object;
using UnityEngine;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Scheda di un tipo di nemico: tutte le sue statistiche in un solo asset (`Assets/_Project/Data/Enemies/`).
    /// Il prefab legge da qui salute, movimento e attacco; lo spawn director legge peso, gruppo e costo.
    /// I valori base vengono da DESIGN_GAMEPLAY.md §4; lo scaling per wave (M3) moltiplicherà questi valori.
    /// Per aggiungere un tipo: una voce nel catalogo di `EnemyContentBuilder` (crea asset e prefab).
    /// </summary>
    [CreateAssetMenu(menuName = "FpsWave/Enemy Definition", fileName = "Enemy_New")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identità")]
        [SerializeField] private string _id = "runner";
        [SerializeField] private string _displayName = "Runner";
        [SerializeField] private NetworkObject _prefab;

        [Header("Salute")]
        [SerializeField, Min(1f)] private float _maxHealth = 60f;

        [Header("Movimento")]
        [SerializeField, Min(0.1f)] private float _moveSpeed = 6f;
        [SerializeField, Min(0.1f)] private float _acceleration = 30f;
        [SerializeField, Min(1f)] private float _angularSpeed = 720f;
        [SerializeField, Min(0.1f)] private float _radius = 0.35f;
        [SerializeField, Min(0.5f)] private float _height = 1.8f;

        [Header("Attacco corpo a corpo")]
        [SerializeField, Min(0f)] private float _attackDamage = 6f;
        [SerializeField, Min(0.1f)] private float _attackInterval = 1f;
        [SerializeField, Min(0f)] private float _attackWindup = 0.35f;
        [SerializeField, Min(0.5f)] private float _attackRange = 1.9f;
        [SerializeField, Min(0.3f)] private float _stopDistance = 1.4f;

        [Header("Spawn")]
        [Tooltip("Peso nella scelta del tipo da spawnare (0 = mai).")]
        [SerializeField, Min(0f)] private float _spawnWeight = 1f;
        [SerializeField] private Vector2Int _groupSize = new(2, 4);
        [Tooltip("Costo nel budget della wave (M3, SPAWN_DIRECTOR.md).")]
        [SerializeField, Min(1)] private int _budgetCost = 1;
        [SerializeField, Min(1)] private int _firstWave = 1;
        [SerializeField, Min(0)] private int _poolPrewarm = 24;

        public string Id => _id;
        public string DisplayName => _displayName;
        public NetworkObject Prefab => _prefab;
        public float MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public float Acceleration => _acceleration;
        public float AngularSpeed => _angularSpeed;
        public float Radius => _radius;
        public float Height => _height;
        public float AttackDamage => _attackDamage;
        public float AttackInterval => _attackInterval;
        public float AttackWindup => _attackWindup;
        public float AttackRange => _attackRange;
        public float StopDistance => _stopDistance;
        public float SpawnWeight => _spawnWeight;
        public int GroupMin => Mathf.Max(1, Mathf.Min(_groupSize.x, _groupSize.y));
        public int GroupMax => Mathf.Max(1, Mathf.Max(_groupSize.x, _groupSize.y));
        public int BudgetCost => _budgetCost;
        public int FirstWave => _firstWave;
        public int PoolPrewarm => _poolPrewarm;

#if UNITY_EDITOR
        public void EditorSetup(string id, string displayName, float maxHealth, float moveSpeed, float attackDamage,
            float attackInterval, float attackRange, float stopDistance, float spawnWeight, Vector2Int groupSize,
            int budgetCost, int firstWave, float radius, float height)
        {
            _id = id;
            _displayName = displayName;
            _maxHealth = maxHealth;
            _moveSpeed = moveSpeed;
            _attackDamage = attackDamage;
            _attackInterval = attackInterval;
            _attackRange = attackRange;
            _stopDistance = stopDistance;
            _spawnWeight = spawnWeight;
            _groupSize = groupSize;
            _budgetCost = budgetCost;
            _firstWave = firstWave;
            _radius = radius;
            _height = height;
        }

        public void EditorSetPrefab(NetworkObject prefab) => _prefab = prefab;
        public void EditorSetPoolPrewarm(int count) => _poolPrewarm = Mathf.Max(0, count);
#endif
    }
}
