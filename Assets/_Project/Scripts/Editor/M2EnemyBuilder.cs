using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Object;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Enemies;
using HrdnLab.FpsWave.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Genera in modo ripetibile il contenuto nemici di M2: prefab del Runner (pool di rete), componente
    /// EnemyTarget sul Player, e nella scena Greybox i muri di copertura extra, i nodi di spawn e l'EnemyDirector.
    /// Non rigenera il Player (le armi di Setup/4 restano): lo modifica solo se manca EnemyTarget.
    /// Viene chiamato anche da "3. Build M1 Content", così una rigenerazione completa non perde i nemici.
    /// </summary>
    public static class M2EnemyBuilder
    {
        private const string MaterialsDir = "Assets/_Project/Art/Materials";
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string RunnerPrefabPath = PrefabsDir + "/Enemy_Runner.prefab";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";
        private const string RootName = "M2_Enemies";

        /// <summary>Muri alti 4 m che spezzano la linea di vista (centro, dimensioni). Il perimetro è a ±80 m.</summary>
        private static readonly (Vector3 center, Vector3 size)[] CoverWalls =
        {
            // Nord
            (new Vector3(-40f, 2f, 45f), new Vector3(14f, 4f, 1f)),
            (new Vector3(-25f, 2f, 60f), new Vector3(1f, 4f, 14f)),
            (new Vector3(0f, 2f, 55f), new Vector3(10f, 4f, 1f)),
            (new Vector3(25f, 2f, 50f), new Vector3(16f, 4f, 1f)),
            (new Vector3(45f, 2f, 62f), new Vector3(1f, 4f, 12f)),
            // Est
            (new Vector3(60f, 2f, 20f), new Vector3(1f, 4f, 16f)),
            (new Vector3(55f, 2f, -20f), new Vector3(12f, 4f, 1f)),
            (new Vector3(65f, 2f, -45f), new Vector3(1f, 4f, 14f)),
            // Ovest
            (new Vector3(-55f, 2f, 25f), new Vector3(1f, 4f, 16f)),
            (new Vector3(-50f, 2f, -25f), new Vector3(12f, 4f, 1f)),
            (new Vector3(-62f, 2f, -50f), new Vector3(1f, 4f, 12f)),
            // Sud, ai lati del poligono di tiro
            (new Vector3(-30f, 2f, -55f), new Vector3(10f, 4f, 1f)),
            (new Vector3(30f, 2f, -50f), new Vector3(10f, 4f, 1f)),
            // Centro: un "isolato" a L e un muro lungo
            (new Vector3(-30f, 2f, 15f), new Vector3(10f, 4f, 1f)),
            (new Vector3(-25.5f, 2f, 20f), new Vector3(1f, 4f, 9f)),
            (new Vector3(35f, 2f, 32f), new Vector3(14f, 4f, 1f)),
        };

        /// <summary>Ripari già presenti nella Greybox (M1) dietro cui ha senso spawnare.</summary>
        private static readonly (Vector3 center, Vector3 size)[] ExistingBlockers =
        {
            (new Vector3(-8f, 2f, 30f), new Vector3(10f, 4f, 1f)),   // Cover_06
            (new Vector3(-20f, 2f, -30f), new Vector3(8f, 4f, 1f)),  // LoS_Wall
            (new Vector3(14f, 1.5f, 14f), new Vector3(1f, 3f, 6f)),  // Cover_03
            (new Vector3(30f, 1.5f, 10f), new Vector3(8f, 3f, 8f)),  // Platform
        };

        [MenuItem("FpsWave/Setup/6. Build Enemies (M2)")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
            EditorSceneManager.OpenScene(ProjectSetupTools.BootScenePath);
        }

        /// <summary>Genera prefab e scena. Lascia aperta la scena Greybox.</summary>
        public static void Build()
        {
            Material body = LitMaterial("M_Enemy_Runner", new Color(0.55f, 0.13f, 0.1f), 0.35f);
            Material wall = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_Grey_Wall.mat");
            if (wall == null)
            {
                Debug.LogError("[FpsWave] Materiali greybox mancanti: eseguire prima 'FpsWave/Setup/3. Build M1 Content'.");
                return;
            }

            NetworkObject runner = CreateRunnerPrefab(body).GetComponent<NetworkObject>();
            AddEnemyTargetToPlayer();
            BuildGreyboxContent(runner, wall);
            AssetDatabase.SaveAssets();

            // I prefab con NetworkObject vanno in DefaultPrefabObjects (FishNet lo fa da solo; qui lo forziamo).
            if (!EditorApplication.ExecuteMenuItem("Tools/Fish-Networking/Utility/Refresh Default Prefabs"))
                Debug.LogWarning("[FpsWave] Menu FishNet 'Refresh Default Prefabs' non trovato: verificare che Enemy_Runner sia in DefaultPrefabObjects.");
            Debug.Log("[FpsWave] Contenuto M2 generato: prefab Enemy_Runner, EnemyTarget sul Player, muri, nodi di spawn ed EnemyDirector nella Greybox.");
        }

        // ------------------------------------------------------------------ Prefab Runner

        private static GameObject CreateRunnerPrefab(Material bodyMaterial)
        {
            var root = new GameObject("Enemy_Runner");
            NetworkObject nob = ObjectFactory.AddComponent<NetworkObject>(root);
            nob.SetDefaultDespawnType(DespawnType.Pool);

            var netTransform = ObjectFactory.AddComponent<NetworkTransform>(root);
            ConfigureEnemyNetworkTransform(netTransform);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.agentTypeID = 0;
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.speed = 6f;
            agent.enabled = false; // lo attiva solo il server (EnemyAgent.OnStartServer)

            var health = ObjectFactory.AddComponent<Health>(root);
            var enemy = ObjectFactory.AddComponent<EnemyAgent>(root);
            var visual = ObjectFactory.AddComponent<EnemyVisual>(root);

            // Il modello è un figlio solo grafico: EnemyVisual lo inclina e lo fa oscillare.
            // Le hitbox stanno in un figlio separato e fermo, così l'animazione cosmetica non sposta i collider
            // (server e client vedono le stesse zone colpibili).
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            var hitboxes = new GameObject("Hitboxes").transform;
            hitboxes.SetParent(root.transform, false);

            var bodyPos = new Vector3(0f, 0.85f, 0f);
            var bodyScale = new Vector3(0.55f, 0.75f, 0.45f);
            var headPos = new Vector3(0f, 1.62f, 0.08f);
            var headScale = Vector3.one * 0.32f;

            GameObject torso = Primitive(PrimitiveType.Capsule, "Body", model, bodyPos, bodyScale, bodyMaterial);
            GameObject head = Primitive(PrimitiveType.Sphere, "Head", model, headPos, headScale, bodyMaterial);
            Object.DestroyImmediate(torso.GetComponent<Collider>());
            Object.DestroyImmediate(head.GetComponent<Collider>());

            var bodyZone = HitboxOnly(PrimitiveType.Capsule, "Body_Hit", hitboxes, bodyPos, bodyScale).AddComponent<HitZone>();
            bodyZone.EditorSetup(1f, false);
            var headZone = HitboxOnly(PrimitiveType.Sphere, "Head_Hit", hitboxes, headPos, headScale).AddComponent<HitZone>();
            headZone.EditorSetup(1f, true);

            // Braccia protese in avanti: sagoma riconoscibile da lontano, senza collider.
            GameObject armL = Primitive(PrimitiveType.Cube, "Arm_L", model, new Vector3(-0.32f, 1.2f, 0.3f),
                new Vector3(0.12f, 0.12f, 0.7f), bodyMaterial);
            GameObject armR = Primitive(PrimitiveType.Cube, "Arm_R", model, new Vector3(0.32f, 1.2f, 0.3f),
                new Vector3(0.12f, 0.12f, 0.7f), bodyMaterial);
            Object.DestroyImmediate(armL.GetComponent<Collider>());
            Object.DestroyImmediate(armR.GetComponent<Collider>());

            // HP e velocità da DESIGN_GAMEPLAY.md §4 (Runner: 60 HP, 6 m/s).
            health.EditorSetup(60f, new[] { bodyZone, headZone });
            enemy.EditorSetup(moveSpeed: 6f, attackDamage: 6f, attackInterval: 1f, attackRange: 1.9f);
            visual.EditorSetup(model, new Renderer[]
            {
                torso.GetComponent<Renderer>(), head.GetComponent<Renderer>(),
                armL.GetComponent<Renderer>(), armR.GetComponent<Renderer>(),
            });

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            SetLayerRecursively(root, GameLayers.Hittable);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RunnerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Server-authoritative, 15 Hz (ogni 2 tick a 30 Hz), solo posizione + yaw (NETCODE.md §3).
        /// </summary>
        private static void ConfigureEnemyNetworkTransform(NetworkTransform netTransform)
        {
            var so = new SerializedObject(netTransform);
            SetBool(so, "_clientAuthoritative", false);
            SetBool(so, "_sendToOwner", false);
            SetBool(so, "_synchronizeScale", false);
            SetBool(so, "_synchronizeParent", false);
            SetBool(so, "_rotationSnapping.X", true);
            SetBool(so, "_rotationSnapping.Z", true);
            SerializedProperty interval = so.FindProperty("_interval");
            if (interval != null)
                interval.intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Player

        private static void AddEnemyTargetToPlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (!root.TryGetComponent(out EnemyTarget target))
                {
                    target = ObjectFactory.AddComponent<EnemyTarget>(root);
                    target.EditorSetup(root.GetComponent<PlayerMotor>());
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ Scena Greybox

        private static void BuildGreyboxContent(NetworkObject runnerPrefab, Material wallMaterial)
        {
            Scene scene = EditorSceneManager.OpenScene(M1ContentBuilder.GreyboxScenePath, OpenSceneMode.Single);
            foreach (GameObject existing in scene.GetRootGameObjects())
                if (existing.name == RootName)
                    Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            var walls = new GameObject("CoverWalls").transform;
            walls.SetParent(root.transform, false);
            for (int i = 0; i < CoverWalls.Length; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"EnemyCover_{i:00}";
                go.transform.SetParent(walls, false);
                go.transform.localPosition = CoverWalls[i].center;
                go.transform.localScale = CoverWalls[i].size;
                go.GetComponent<MeshRenderer>().sharedMaterial = wallMaterial;
                go.layer = GameLayers.Environment;
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            walls.gameObject.layer = GameLayers.Environment;

            // Nodi ai due lati di ogni riparo: a runtime il director usa solo quelli coperti rispetto ai player.
            Physics.SyncTransforms();
            var nodesRoot = new GameObject("EnemySpawnNodes").transform;
            nodesRoot.SetParent(root.transform, false);
            var positions = new List<Vector3>(128);
            foreach ((Vector3 center, Vector3 size) in CoverWalls)
                AddNodesAround(center, size, positions);
            foreach ((Vector3 center, Vector3 size) in ExistingBlockers)
                AddNodesAround(center, size, positions);

            int placed = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                if (!IsFreeSpot(positions[i]))
                    continue;
                var node = new GameObject($"Node_{placed:000}");
                node.transform.SetParent(nodesRoot, false);
                node.transform.position = positions[i];
                node.AddComponent<EnemySpawnNode>();
                placed++;
            }

            var director = root.AddComponent<EnemyDirector>();
            director.EditorSetup(runnerPrefab, new Vector3(0f, 5f, 0f), new Vector3(166f, 24f, 166f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, M1ContentBuilder.GreyboxScenePath);
            Debug.Log($"[FpsWave] Greybox: {CoverWalls.Length} muri di copertura, {placed} nodi di spawn.");
        }

        /// <summary>Due o tre nodi per lato lungo del riparo, a 1.6 m dalla faccia.</summary>
        private static void AddNodesAround(Vector3 center, Vector3 size, List<Vector3> output)
        {
            bool alongX = size.x >= size.z;
            float length = alongX ? size.x : size.z;
            float halfThickness = (alongX ? size.z : size.x) * 0.5f;
            float offset = halfThickness + 1.6f;
            float end = Mathf.Max(0f, length * 0.5f - 1.5f);
            int steps = length >= 12f ? 3 : 2;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int s = 0; s < steps; s++)
                {
                    float along = steps == 1 ? 0f : Mathf.Lerp(-end, end, s / (float)(steps - 1));
                    Vector3 p = alongX
                        ? new Vector3(center.x + along, 0f, center.z + side * offset)
                        : new Vector3(center.x + side * offset, 0f, center.z + along);
                    output.Add(p);
                }
            }
        }

        /// <summary>Scarta i nodi fuori dal perimetro, nel poligono di tiro o dentro altra geometria.</summary>
        private static bool IsFreeSpot(Vector3 p)
        {
            if (Mathf.Abs(p.x) > 77f || Mathf.Abs(p.z) > 77f)
                return false;
            if (Mathf.Abs(p.x) < 9f && p.z > -64f && p.z < -13f)
                return false; // corsia del poligono e zona di spawn dei player
            return !Physics.CheckBox(p + Vector3.up * 1f, new Vector3(0.45f, 0.85f, 0.45f), Quaternion.identity,
                1 << GameLayers.Environment, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ Utilità

        private static Material LitMaterial(string name, Color color, float smoothness)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Primitiva con il solo collider (niente mesh): per le hitbox.</summary>
        private static GameObject HitboxOnly(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            return go;
        }

        private static void SetBool(SerializedObject so, string property, bool value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
                p.boolValue = value;
            else
                Debug.LogWarning($"[FpsWave] Proprietà '{property}' non trovata su {so.targetObject.GetType().Name}.");
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
