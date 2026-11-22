using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Object;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Enemies;
using HrdnLab.FpsWave.Player;
using HrdnLab.FpsWave.UI;
using HrdnLab.FpsWave.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Genera in modo ripetibile il contenuto nemici: per ogni voce del <see cref="Catalog"/> una scheda
    /// <see cref="EnemyDefinition"/> (`Data/Enemies/`) e un prefab di rete in pool (`Prefabs/Enemies/`);
    /// sul Player i componenti EnemyTarget e DamageNumbers; nella Greybox muri di copertura, nodi di spawn ed EnemyDirector.
    /// Non rigenera il Player (le armi di Setup/4 restano): aggiunge solo i componenti mancanti.
    /// Viene chiamato anche da "3. Build M1 Content", così una rigenerazione completa non perde i nemici.
    /// </summary>
    public static class EnemyContentBuilder
    {
        private const string MaterialsDir = "Assets/_Project/Art/Materials";
        private const string EnemyDataDir = "Assets/_Project/Data/Enemies";
        private const string EnemyPrefabsDir = "Assets/_Project/Prefabs/Enemies";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string RootName = "M2_Enemies";

        // =========================================================================================
        // CATALOGO DEI NEMICI: un tipo = una voce. Statistiche da DESIGN_GAMEPLAY.md §4.
        // Per aggiungere un nemico (Brute, Spitter...) si aggiunge qui una voce e si esegue Setup/6:
        // nascono la scheda Data/Enemies/Enemy_<Nome>.asset e il prefab Prefabs/Enemies/Enemy_<Nome>.prefab,
        // e il director inizia a spawnarlo secondo SpawnWeight. I valori si ritoccano poi nella scheda.
        // Comportamenti speciali (carica del Brute, attacco a distanza) andranno in componenti propri
        // in Scripts/Enemies/Types/, aggiunti al prefab da questa voce.
        // =========================================================================================
        private sealed class EnemyTypeSpec
        {
            public string Id;
            public string Name;
            public float MaxHealth;
            public float MoveSpeed;
            public float AttackDamage;
            public float AttackInterval;
            public float AttackRange;
            public float StopDistance;
            public float SpawnWeight;
            public Vector2Int GroupSize;
            public int BudgetCost;
            public int FirstWave;
            // Sagoma (greybox finché non arrivano i modelli veri)
            public Color BodyColor;
            public Vector3 BodyScale;   // capsula del corpo: larghezza, mezza altezza, profondità
            public float HeadSize;
            public float AgentRadius;
            public float AgentHeight;
            // Modello vero (opzionale): cartella con <Nome>.fbx e Textures/<Nome>_BaseColor|_Normal|_MetallicSmoothness.
            // Binari fuori dal repo (docs/ASSETS.md): se mancano si usa la sagoma greybox qui sopra.
            public string ModelDir;
            public float ModelYaw;
            // Corazza a piastre (opzionale): ogni piastra assorbe i colpi finché non si rompe.
            public PlateSpec[] Plates;
            public Color ArmorColor;
            // Punto debole luminoso (opzionale), di solito sotto una piastra.
            public float CoreSize;
            public Vector3 CorePosition;
            // Comportamenti speciali
            public bool Charge;
            public int PoolPrewarm = 24;
        }

        private sealed class PlateSpec
        {
            public string Name;
            public Vector3 Position;
            public Vector3 Size;
            public float Health;
        }

        private static readonly EnemyTypeSpec[] Catalog =
        {
            new EnemyTypeSpec
            {
                Id = "runner", Name = "Runner",
                MaxHealth = 60f, MoveSpeed = 6f,
                AttackDamage = 6f, AttackInterval = 1f, AttackRange = 1.9f, StopDistance = 1.4f,
                SpawnWeight = 1f, GroupSize = new Vector2Int(2, 4), BudgetCost = 1, FirstWave = 1,
                BodyColor = new Color(0.55f, 0.13f, 0.1f), BodyScale = new Vector3(0.55f, 0.75f, 0.45f), HeadSize = 0.32f,
                AgentRadius = 0.35f, AgentHeight = 1.8f,
                ModelDir = "Assets/ThirdParty/_Own/Runner",
            },
            new EnemyTypeSpec
            {
                // Tank lento con corazza a piastre e carica (DESIGN_GAMEPLAY §4). Le piastre si rompono e scoprono
                // il nucleo luminoso sul petto (punto debole). FirstWave 3 vale da M3: per ora il peso lo rende raro.
                Id = "brute", Name = "Brute",
                MaxHealth = 600f, MoveSpeed = 3.5f,
                AttackDamage = 25f, AttackInterval = 1.6f, AttackRange = 2.4f, StopDistance = 1.9f,
                SpawnWeight = 0.12f, GroupSize = new Vector2Int(1, 1), BudgetCost = 6, FirstWave = 3,
                BodyColor = new Color(0.32f, 0.1f, 0.07f), BodyScale = new Vector3(0.95f, 1.0f, 0.75f), HeadSize = 0.42f,
                AgentRadius = 0.6f, AgentHeight = 2.5f,
                ArmorColor = new Color(0.2f, 0.22f, 0.25f),
                Plates = new[]
                {
                    new PlateSpec { Name = "Chest", Position = new Vector3(0f, 1.45f, 0.42f), Size = new Vector3(0.72f, 0.62f, 0.1f), Health = 150f },
                    new PlateSpec { Name = "Back", Position = new Vector3(0f, 1.4f, -0.42f), Size = new Vector3(0.78f, 0.78f, 0.1f), Health = 120f },
                    new PlateSpec { Name = "Shoulder_L", Position = new Vector3(-0.56f, 1.78f, 0.04f), Size = new Vector3(0.36f, 0.18f, 0.48f), Health = 80f },
                    new PlateSpec { Name = "Shoulder_R", Position = new Vector3(0.56f, 1.78f, 0.04f), Size = new Vector3(0.36f, 0.18f, 0.48f), Health = 80f },
                    new PlateSpec { Name = "Helmet", Position = new Vector3(0f, 2.42f, 0.06f), Size = new Vector3(0.5f, 0.2f, 0.5f), Health = 90f },
                },
                CoreSize = 0.28f, CorePosition = new Vector3(0f, 1.45f, 0.3f),
                Charge = true,
                PoolPrewarm = 4,
            },
        };

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

        [MenuItem("FpsWave/Setup/6. Build Enemies")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
            EditorSceneManager.OpenScene(ProjectSetupTools.BootScenePath);
        }

        /// <summary>Genera schede, prefab, componenti del Player e contenuto della Greybox. Lascia aperta la Greybox.</summary>
        public static void Build()
        {
            Material wall = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_Grey_Wall.mat");
            if (wall == null)
            {
                Debug.LogError("[FpsWave] Materiali greybox mancanti: eseguire prima 'FpsWave/Setup/3. Build M1 Content'.");
                return;
            }
            EnsureFolder(EnemyDataDir);
            EnsureFolder(EnemyPrefabsDir);

            var definitions = new EnemyDefinition[Catalog.Length];
            for (int i = 0; i < Catalog.Length; i++)
                definitions[i] = BuildEnemyType(Catalog[i]);

            EnsurePlayerComponents();
            BuildGreyboxContent(definitions, wall);
            AssetDatabase.SaveAssets();

            // I prefab con NetworkObject vanno in DefaultPrefabObjects (FishNet lo fa da solo; qui lo forziamo).
            if (!EditorApplication.ExecuteMenuItem("Tools/Fish-Networking/Utility/Refresh Default Prefabs"))
                Debug.LogWarning("[FpsWave] Menu FishNet 'Refresh Default Prefabs' non trovato: verificare che i prefab dei nemici siano in DefaultPrefabObjects.");
            Debug.Log($"[FpsWave] Nemici generati: {Catalog.Length} tipi, componenti del Player, muri, nodi di spawn ed EnemyDirector nella Greybox.");
        }

        // ------------------------------------------------------------------ Tipi di nemico

        private static EnemyDefinition BuildEnemyType(EnemyTypeSpec spec)
        {
            // Scheda: si riusa l'asset esistente (stesso GUID), si riscrivono i valori del catalogo.
            string definitionPath = $"{EnemyDataDir}/Enemy_{spec.Name}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }
            definition.EditorSetup(spec.Id, spec.Name, spec.MaxHealth, spec.MoveSpeed, spec.AttackDamage, spec.AttackInterval,
                spec.AttackRange, spec.StopDistance, spec.SpawnWeight, spec.GroupSize, spec.BudgetCost, spec.FirstWave,
                spec.AgentRadius, spec.AgentHeight);
            definition.EditorSetPoolPrewarm(spec.PoolPrewarm);

            Material material = LitMaterial($"M_Enemy_{spec.Name}", spec.BodyColor, 0.35f);
            GameObject prefab = CreateEnemyPrefab(spec, definition, material);
            definition.EditorSetPrefab(prefab.GetComponent<NetworkObject>());
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static GameObject CreateEnemyPrefab(EnemyTypeSpec spec, EnemyDefinition definition, Material bodyMaterial)
        {
            var root = new GameObject($"Enemy_{spec.Name}");
            NetworkObject nob = ObjectFactory.AddComponent<NetworkObject>(root);
            nob.SetDefaultDespawnType(DespawnType.Pool);

            var netTransform = ObjectFactory.AddComponent<NetworkTransform>(root);
            ConfigureEnemyNetworkTransform(netTransform);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.agentTypeID = 0;
            agent.radius = spec.AgentRadius;
            agent.height = spec.AgentHeight;
            agent.baseOffset = 0f;
            agent.speed = spec.MoveSpeed;
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

            Vector3 bodyScale = spec.BodyScale;
            float bodyHeight = bodyScale.y * 2f;               // la capsula di Unity è alta 2 a scala 1
            var bodyPos = new Vector3(0f, bodyScale.y + 0.1f, 0f);
            var headPos = new Vector3(0f, bodyPos.y + bodyHeight * 0.5f + spec.HeadSize * 0.4f, 0.08f);
            Vector3 headScale = Vector3.one * spec.HeadSize;
            float armY = bodyPos.y + bodyHeight * 0.23f;
            float armX = bodyScale.x * 0.58f;

            Renderer[] bodyRenderers;
            bool realModel = TryBuildRealModel(spec, model, out Renderer[] modelRenderers, out Vector3 modelHead, out float modelHeadSize);
            if (realModel)
            {
                bodyRenderers = modelRenderers;
                headPos = modelHead;
                headScale = Vector3.one * modelHeadSize;
                bodyScale.y = Mathf.Max(0.3f, (headPos.y - modelHeadSize * 0.5f - 0.1f) * 0.5f);
                bodyPos = new Vector3(0f, bodyScale.y + 0.1f, 0f);
            }
            else
            {
                GameObject torso = Primitive(PrimitiveType.Capsule, "Body", model, bodyPos, bodyScale, bodyMaterial);
                GameObject head = Primitive(PrimitiveType.Sphere, "Head", model, headPos, headScale, bodyMaterial);
                Object.DestroyImmediate(torso.GetComponent<Collider>());
                Object.DestroyImmediate(head.GetComponent<Collider>());

                // Braccia protese in avanti: sagoma riconoscibile da lontano, senza collider.
                float armThickness = 0.12f * bodyScale.x / 0.55f;
                var armSize = new Vector3(armThickness, armThickness, 0.7f * Mathf.Max(1f, bodyScale.y / 0.75f));
                GameObject armL = Primitive(PrimitiveType.Cube, "Arm_L", model, new Vector3(-armX, armY, 0.3f), armSize, bodyMaterial);
                GameObject armR = Primitive(PrimitiveType.Cube, "Arm_R", model, new Vector3(armX, armY, 0.3f), armSize, bodyMaterial);
                Object.DestroyImmediate(armL.GetComponent<Collider>());
                Object.DestroyImmediate(armR.GetComponent<Collider>());
                bodyRenderers = new Renderer[]
                {
                    torso.GetComponent<Renderer>(), head.GetComponent<Renderer>(),
                    armL.GetComponent<Renderer>(), armR.GetComponent<Renderer>(),
                };
            }

            var bodyZone = HitboxOnly(PrimitiveType.Capsule, "Body_Hit", hitboxes, bodyPos, bodyScale).AddComponent<HitZone>();
            bodyZone.EditorSetup(1f, false);
            var headZone = HitboxOnly(PrimitiveType.Sphere, "Head_Hit", hitboxes, headPos, headScale).AddComponent<HitZone>();
            headZone.EditorSetup(1f, true);

            var zones = new List<HitZone> { bodyZone, headZone };

            // Corazza a piastre: visuale nel modello, collider (con HitZone e ArmorPlate) tra le hitbox.
            if (spec.Plates != null && spec.Plates.Length > 0)
            {
                Material armorMaterial = LitMaterial($"M_Enemy_{spec.Name}_Armor", spec.ArmorColor, 0.55f);
                armorMaterial.SetFloat("_Metallic", 0.85f);
                var plates = new List<ArmorPlate>();
                foreach (PlateSpec plateSpec in spec.Plates)
                {
                    GameObject visualPlate = Primitive(PrimitiveType.Cube, $"Plate_{plateSpec.Name}", model, plateSpec.Position, plateSpec.Size, armorMaterial);
                    Object.DestroyImmediate(visualPlate.GetComponent<Collider>());
                    GameObject plateHit = HitboxOnly(PrimitiveType.Cube, $"Plate_{plateSpec.Name}_Hit", hitboxes, plateSpec.Position, plateSpec.Size);
                    var plateZone = plateHit.AddComponent<HitZone>();
                    plateZone.EditorSetup(1f, false);
                    var plate = plateHit.AddComponent<ArmorPlate>();
                    plate.EditorSetup(plateSpec.Health, 0.15f, new[] { visualPlate.GetComponent<Renderer>() }, spec.ArmorColor);
                    zones.Add(plateZone);
                    plates.Add(plate);
                }
                ObjectFactory.AddComponent<EnemyArmor>(root).EditorSetup(plates.ToArray());
            }

            // Nucleo luminoso: punto debole (danno x2 con il moltiplicatore weakspot dell'arma).
            if (spec.CoreSize > 0f)
            {
                Material coreMaterial = LitMaterial($"M_Enemy_{spec.Name}_Core", new Color(1f, 0.45f, 0.1f), 0.8f);
                coreMaterial.EnableKeyword("_EMISSION");
                coreMaterial.SetColor("_EmissionColor", new Color(1f, 0.42f, 0.08f) * 2.2f);
                coreMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                GameObject core = Primitive(PrimitiveType.Sphere, "Core", model, spec.CorePosition, Vector3.one * spec.CoreSize, coreMaterial);
                Object.DestroyImmediate(core.GetComponent<Collider>());
                var coreZone = HitboxOnly(PrimitiveType.Sphere, "Core_Hit", hitboxes, spec.CorePosition, Vector3.one * spec.CoreSize).AddComponent<HitZone>();
                coreZone.EditorSetup(1f, true);
                zones.Add(coreZone);
            }

            if (spec.Charge)
                ObjectFactory.AddComponent<BruteCharge>(root);

            health.EditorSetup(spec.MaxHealth, zones.ToArray());
            enemy.EditorSetup(definition);
            if (realModel)
                visual.EditorSetup(model, bodyRenderers, Color.white, new Color(1f, 0.72f, 0.6f), new Color(0.32f, 0.3f, 0.3f), spec.MoveSpeed);
            else
                visual.EditorSetup(model, bodyRenderers, spec.BodyColor, spec.MoveSpeed);

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            SetLayerRecursively(root, GameLayers.Hittable);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{EnemyPrefabsDir}/Enemy_{spec.Name}.prefab");
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

        /// <summary>Aggiunge al Player solo ciò che manca: EnemyTarget (bersaglio dei nemici) e DamageNumbers (numeri di danno).</summary>
        private static void EnsurePlayerComponents()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                bool changed = false;
                if (!root.TryGetComponent(out EnemyTarget target))
                {
                    target = ObjectFactory.AddComponent<EnemyTarget>(root);
                    target.EditorSetup(root.GetComponent<PlayerMotor>());
                    changed = true;
                }
                if (!root.TryGetComponent(out DamageNumbers numbers))
                {
                    numbers = root.AddComponent<DamageNumbers>();
                    numbers.EditorSetup(root.GetComponent<PlayerWeapons>());
                    changed = true;
                }
                // Fase 1: tuta (energia e abilità), a terra e rianimazione, ping di squadra.
                if (!root.TryGetComponent(out SuitAbilities _))
                {
                    ObjectFactory.AddComponent<SuitAbilities>(root);
                    changed = true;
                }
                if (!root.TryGetComponent(out PlayerLife _))
                {
                    ObjectFactory.AddComponent<PlayerLife>(root);
                    changed = true;
                }
                if (!root.TryGetComponent(out PlayerPing _))
                {
                    ObjectFactory.AddComponent<PlayerPing>(root);
                    changed = true;
                }
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ Scena Greybox

        private static void BuildGreyboxContent(EnemyDefinition[] enemyTypes, Material wallMaterial)
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
            director.EditorSetup(enemyTypes, new Vector3(0f, 5f, 0f), new Vector3(166f, 24f, 166f));

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
                    float along = Mathf.Lerp(-end, end, s / (float)(steps - 1));
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

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

        // ------------------------------------------------------------------ Modelli veri

        /// <summary>
        /// Se nella cartella del tipo c'è il modello (statico, senza scheletro), lo mette sotto <paramref name="model"/>
        /// come semplici MeshFilter/MeshRenderer (niente prefab annidato: su un PC senza i binari il prefab resta valido
        /// e il nemico è solo invisibile), girato di <see cref="EnemyTypeSpec.ModelYaw"/>, alto quanto l'agente, piedi a 0.
        /// Ricava anche la testa (vertici più alti) per la hitbox del punto debole.
        /// </summary>
        private static bool TryBuildRealModel(EnemyTypeSpec spec, Transform model, out Renderer[] renderers, out Vector3 headCenter, out float headSize)
        {
            renderers = null;
            headCenter = default;
            headSize = spec.HeadSize;
            if (string.IsNullOrEmpty(spec.ModelDir))
                return false;
            string fbx = $"{spec.ModelDir}/{spec.Name}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (asset == null)
            {
                Debug.LogWarning($"[FpsWave] {spec.Name}: modello {fbx} assente (docs/ASSETS.md), uso la sagoma greybox.");
                return false;
            }
            ConfigureModelImporter(fbx);
            Material material = ModelMaterial(spec);

            // Parti del modello con le loro trasformazioni, nello spazio del modello girato.
            Quaternion yaw = Quaternion.Euler(0f, spec.ModelYaw, 0f);
            // La rotazione di conversione degli assi dell'FBX (es. -90 su X) sta spesso sulla radice importata:
            // si tiene, misurando tutto nello spazio del mondo con la radice nell'origine.
            var temp = Object.Instantiate(asset);
            temp.transform.position = Vector3.zero;
            var parts = new List<(Mesh mesh, Matrix4x4 m)>();
            foreach (MeshFilter mf in temp.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh != null)
                    parts.Add((mf.sharedMesh, Matrix4x4.Rotate(yaw) * mf.transform.localToWorldMatrix));
            }
            foreach (SkinnedMeshRenderer smr in temp.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh != null)
                    parts.Add((smr.sharedMesh, Matrix4x4.Rotate(yaw) * smr.transform.localToWorldMatrix));
            }
            Object.DestroyImmediate(temp);
            if (parts.Count == 0)
                return false;

            // Limiti e vertici per scala, posizione e testa.
            var points = new List<Vector3>();
            int triangles = 0;
            foreach (var (mesh, m) in parts)
            {
                Vector3[] v = mesh.vertices;
                for (int i = 0; i < v.Length; i++)
                    points.Add(m.MultiplyPoint3x4(v[i]));
                triangles += (int)(mesh.GetIndexCount(0) / 3);
                for (int sub = 1; sub < mesh.subMeshCount; sub++)
                    triangles += (int)(mesh.GetIndexCount(sub) / 3);
            }
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 p in points)
                bounds.Encapsulate(p);
            float scale = spec.AgentHeight / Mathf.Max(0.01f, bounds.size.y);
            var offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

            // Testa: i vertici nel 9% più alto; centro e grandezza (sfera che li contiene quasi tutti).
            float topBand = bounds.max.y - bounds.size.y * 0.09f;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Vector3 p in points)
            {
                if (p.y >= topBand) { sum += p; count++; }
            }
            Vector3 headTop = count > 0 ? sum / count : bounds.center;
            headSize = Mathf.Clamp(bounds.size.y * scale * 0.15f, 0.22f, 0.4f);
            headCenter = new Vector3((headTop.x + offset.x) * scale, spec.AgentHeight - headSize * 0.5f, (headTop.z + offset.z) * scale);

            var list = new List<Renderer>();
            for (int i = 0; i < parts.Count; i++)
            {
                var (mesh, m) = parts[i];
                var go = new GameObject(parts.Count == 1 ? "Mesh" : $"Mesh_{i}");
                go.transform.SetParent(model, false);
                // m = R*T*S dello spazio del modello: lo si porta nello spazio del nemico (offset e scala uniforme).
                Matrix4x4 final = Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Translate(offset) * m;
                go.transform.localPosition = final.GetColumn(3);
                go.transform.localRotation = final.rotation;
                go.transform.localScale = final.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int k = 0; k < materials.Length; k++)
                    materials[k] = material;
                mr.sharedMaterials = materials;
                list.Add(mr);
            }
            renderers = list.ToArray();
            Debug.Log($"[FpsWave] {spec.Name}: modello vero ({triangles} triangoli, {parts.Count} parti, scala {scale:0.###}), testa a {headCenter}.");
            return true;
        }

        private static void ConfigureModelImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                return;
            bool changed = importer.animationType != ModelImporterAnimationType.None
                           || importer.importAnimation || importer.importCameras || importer.importLights
                           || importer.materialImportMode != ModelImporterMaterialImportMode.None
                           || importer.meshCompression != ModelImporterMeshCompression.Medium;
            if (!changed)
                return;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.SaveAndReimport();
        }

        private static Material ModelMaterial(EnemyTypeSpec spec)
        {
            string tex = $"{spec.ModelDir}/Textures/{spec.Name}";
            Texture2D baseMap = LoadTexture($"{tex}_BaseColor", false, true);
            Texture2D normal = LoadTexture($"{tex}_Normal", true, false);
            Texture2D metallic = LoadTexture($"{tex}_MetallicSmoothness", false, false);
            Material mat = LitMaterial($"M_Enemy_{spec.Name}_Model", Color.white, 0.5f);
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", Color.white);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (metallic != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", 1f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Carica una texture (png o jpg) e la importa per un nemico in massa: 2048 px, compressa, mip.</summary>
        private static Texture2D LoadTexture(string pathNoExt, bool normalMap, bool srgb)
        {
            foreach (string ext in new[] { ".png", ".jpg", ".jpeg", ".tga" })
            {
                string path = pathNoExt + ext;
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;
                TextureImporterType type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.textureType != type || importer.sRGBTexture != srgb || importer.maxTextureSize != 2048)
                {
                    importer.textureType = type;
                    importer.sRGBTexture = srgb;
                    importer.maxTextureSize = 2048;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
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
