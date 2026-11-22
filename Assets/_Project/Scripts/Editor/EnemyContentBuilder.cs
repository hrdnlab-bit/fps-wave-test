using System.Collections.Generic;
using System.Linq;
using FishNet.Component.Transforming;
using FishNet.Object;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Enemies;
using HrdnLab.FpsWave.Player;
using HrdnLab.FpsWave.UI;
using HrdnLab.FpsWave.Weapons;
using UnityEditor;
using UnityEditor.Animations;
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
        private const string AnimationsDir = "Assets/_Project/Art/Animations";
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
            // Velocità naturali delle clip di corsa (m/s in Unity = unità del modello da tools/autorig x scala):
            // oltre, la clip accelera (piedi senza scivolare). Run2 è la seconda andatura a caso, Jog la corsetta.
            public float RunClipSpeed = 4f;
            public float Run2ClipSpeed = 4f;
            public float JogClipSpeed = 2.5f;
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
                ModelYaw = 180f, // l'FBX da Blender guarda -Z dopo la conversione degli assi
                // Mixamo: Sprint 2,817, Fast Run 2,583, Zombie Running 1,413 unità/s x scala 1,799
                RunClipSpeed = 5.07f, Run2ClipSpeed = 4.65f, JogClipSpeed = 2.54f,
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
            bool realModel = TryBuildRealModel(spec, model, out Renderer[] modelRenderers, out Animator modelAnimator,
                out Vector3 modelHead, out float modelHeadSize, out Vector3 modelChest);
            if (realModel)
            {
                bodyRenderers = modelRenderers;
                headPos = modelHead;
                headScale = Vector3.one * modelHeadSize;
                bodyScale.y = Mathf.Max(0.3f, (headPos.y - modelHeadSize * 0.5f - 0.1f) * 0.5f);
                bodyPos = new Vector3(modelChest.x, bodyScale.y + 0.1f, modelChest.z);
                if (modelAnimator != null)
                {
                    var gore = modelAnimator.gameObject.AddComponent<EnemyGore>();
                    gore.EditorSetup(modelAnimator.GetComponentInChildren<SkinnedMeshRenderer>(true));
                    int attacks = modelAnimator.runtimeAnimatorController.animationClips.Count(c => c.name.StartsWith("Attack"));
                    visual.EditorSetAnimator(modelAnimator, spec.RunClipSpeed, spec.Run2ClipSpeed, Mathf.Max(1, attacks), gore);
                    enemy.EditorSetCorpseSeconds(3f); // caduta Mixamo ~2 s, poi qualche istante a terra
                }
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
        /// Se nella cartella del tipo c'è il modello, lo mette sotto <paramref name="model"/> girato di
        /// <see cref="EnemyTypeSpec.ModelYaw"/>, alto quanto l'agente, piedi a 0.
        /// Senza scheletro: semplici MeshFilter/MeshRenderer. Con scheletro: copia spacchettata della gerarchia
        /// (ossa + SkinnedMeshRenderer + Animator con controller generato dalle clip Idle/Run/Attack/Death).
        /// In entrambi i casi niente prefab annidato: su un PC senza i binari il prefab resta valido (nemico invisibile).
        /// La testa (hitbox del punto debole) si ricava dai vertici più alti e, con le animazioni, dalla posa media di corsa.
        /// </summary>
        private static bool TryBuildRealModel(EnemyTypeSpec spec, Transform model, out Renderer[] renderers, out Animator animator,
            out Vector3 headCenter, out float headSize, out Vector3 chest)
        {
            renderers = null;
            animator = null;
            headCenter = default;
            chest = Vector3.zero;
            headSize = spec.HeadSize;
            if (string.IsNullOrEmpty(spec.ModelDir))
                return false;
            string fbx = $"{spec.ModelDir}/{spec.Name}.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
            {
                Debug.LogWarning($"[FpsWave] {spec.Name}: modello {fbx} assente (docs/ASSETS.md), uso la sagoma greybox.");
                return false;
            }
            ConfigureModelImporter(fbx);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            Material material = ModelMaterial(spec);
            bool skinned = asset.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = spec.Name + "_Model";
            inst.transform.SetParent(model, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0f, spec.ModelYaw, 0f) * inst.transform.localRotation;

            // Vertici in posa di riposo (= posa di bind), nello spazio del nemico.
            var points = new List<Vector3>();
            int triangles = 0;
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr)
                    mesh = smr.sharedMesh;
                else if (r.TryGetComponent(out MeshFilter mf))
                    mesh = mf.sharedMesh;
                if (mesh == null)
                    continue;
                Matrix4x4 m = model.worldToLocalMatrix * r.transform.localToWorldMatrix;
                foreach (Vector3 v in mesh.vertices)
                    points.Add(m.MultiplyPoint3x4(v));
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    triangles += (int)(mesh.GetIndexCount(sub) / 3);
            }
            if (points.Count == 0)
            {
                Object.DestroyImmediate(inst);
                return false;
            }
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 p in points)
                bounds.Encapsulate(p);
            float scale = spec.AgentHeight / Mathf.Max(0.01f, bounds.size.y);
            var offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            inst.transform.localScale *= scale;
            inst.transform.localPosition = offset * scale;

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
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var slots = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < slots.Length; k++)
                    slots[k] = material;
                r.sharedMaterials = slots;
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.quality = SkinQuality.Bone2; // decine a schermo
                    smr.updateWhenOffscreen = false;
                    smr.skinnedMotionVectors = false;
                    Bounds lb = smr.localBounds; // largo abbastanza anche per il corpo steso a terra
                    lb.Expand(lb.size.magnitude * 0.8f);
                    smr.localBounds = lb;
                }
                list.Add(r);
            }
            renderers = list.ToArray();

            animator = inst.GetComponent<Animator>();
            if (skinned && animator != null)
            {
                AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__")).ToArray();
                AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n);
                var set = new ClipSet
                {
                    Idle = Clip("Idle"), Jog = Clip("Jog"), Run = Clip("Run"), Run2 = Clip("Run2") ?? Clip("Run"),
                    Attacks = new[] { Clip("Attack"), Clip("Attack2"), Clip("Attack3") }.Where(c => c != null).ToArray(),
                    Hit = Clip("Hit"),
                    Deaths = new[] { Clip("DeathBack") ?? Clip("Death"), Clip("DeathForward") ?? Clip("Death"), Clip("DeathHead") ?? Clip("Death") },
                };
                AnimationClip run = set.Run;
                AnimationClip idle = set.Idle;
                if (set.Idle != null && set.Run != null && set.Attacks.Length > 0 && set.Deaths.All(d => d != null))
                {
                    animator.runtimeAnimatorController = BuildController(spec, set);
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                    // Hitbox fisse (uguali sul server): testa e busto nella posa media di corsa.
                    Transform head = FindDeep(inst.transform, "Head"), spine = FindDeep(inst.transform, "Spine1");
                    if (head != null && spine != null)
                    {
                        Vector3 headLocal = head.InverseTransformPoint(model.TransformPoint(headCenter));
                        Vector3 h = Vector3.zero, c = Vector3.zero;
                        const int samples = 8;
                        for (int i = 0; i < samples; i++)
                        {
                            run.SampleAnimation(inst, run.length * i / samples);
                            h += model.InverseTransformPoint(head.TransformPoint(headLocal));
                            c += model.InverseTransformPoint(spine.position);
                        }
                        headCenter = h / samples;
                        chest = c / samples;
                        chest.y = 0f;
                    }
                    idle.SampleAnimation(inst, 0f);
                }
                else
                {
                    Debug.LogWarning($"[FpsWave] {spec.Name}: scheletro senza le clip Idle/Run/Attack/Death, resta fermo.");
                    animator.enabled = false;
                    animator = null;
                }
            }
            else if (animator != null)
            {
                Object.DestroyImmediate(animator);
                animator = null;
            }

            Debug.Log($"[FpsWave] {spec.Name}: modello vero ({triangles} triangoli, {(skinned ? "con scheletro" : "statico")}, " +
                      $"scala {scale:0.###}), testa a {headCenter}.");
            return true;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private sealed class ClipSet
        {
            public AnimationClip Idle, Jog, Run, Run2, Hit;
            public AnimationClip[] Attacks, Deaths;

            public AnimationClip[] All => new[] { Idle, Jog, Run, Run2, Hit }.Concat(Attacks).Concat(Deaths)
                .Where(c => c != null).Distinct().ToArray();
        }

        /// <summary>
        /// Controller generato. Locomotion: blend sulla velocità (m/s) Idle -> Jog -> corsa, la corsa è un blend su
        /// Variant (Run/Run2, scelto a caso per nemico) e accelera con RunRate. Attacchi: trigger Attack + AttackIndex.
        /// Hit: trigger (reazione al colpo). Morte: bool Dead + DeathType (0 indietro, 1 avanti, 2 testa) da qualsiasi stato.
        /// Si ricrea solo se clip o parametri non coincidono, per non sporcare git a ogni Setup.
        /// </summary>
        private static AnimatorController BuildController(EnemyTypeSpec spec, ClipSet set)
        {
            EnsureFolder(AnimationsDir);
            string path = $"{AnimationsDir}/{spec.Name}.controller";
            AnimationClip[] all = set.All;
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null && existing.parameters.Any(p => p.name == "DeathType")
                && existing.animationClips.Distinct().Count() == all.Length && all.All(c => existing.animationClips.Contains(c))
                && existing.layers[0].stateMachine.defaultState?.motion is BlendTree bt
                && bt.children.Length > 0 && Mathf.Approximately(bt.children[^1].threshold, spec.RunClipSpeed))
                return existing;
            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
                { name = "RunRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter("Variant", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("DeathType", AnimatorControllerParameterType.Int);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(set.Idle, 0f);
            if (set.Jog != null)
                tree.AddChild(set.Jog, spec.JogClipSpeed);
            BlendTree runs = tree.CreateBlendTreeChild(spec.RunClipSpeed);
            runs.name = "Runs";
            runs.blendType = BlendTreeType.Simple1D;
            runs.blendParameter = "Variant";
            runs.useAutomaticThresholds = false;
            runs.AddChild(set.Run, 0f);
            runs.AddChild(set.Run2, 1f);
            locomotion.speedParameterActive = true; // RunRate: la corsa accelera oltre la velocità della clip
            locomotion.speedParameter = "RunRate";
            sm.defaultState = locomotion;

            for (int i = 0; i < set.Attacks.Length; i++)
            {
                AnimatorState attack = sm.AddState(i == 0 ? "Attack" : $"Attack{i + 1}");
                attack.motion = set.Attacks[i];
                attack.speed = 1.5f; // le clip Mixamo hanno caricamento e recupero lunghi
                AnimatorStateTransition go = locomotion.AddTransition(attack);
                go.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                go.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");
                go.hasExitTime = false;
                go.duration = 0.1f;
                AnimatorStateTransition back = attack.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = 0.78f;
                back.duration = 0.25f;
            }

            if (set.Hit != null)
            {
                AnimatorState hit = sm.AddState("Hit");
                hit.motion = set.Hit;
                hit.speed = 1.4f;
                AnimatorStateTransition go = locomotion.AddTransition(hit);
                go.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                go.hasExitTime = false;
                go.duration = 0.08f;
                AnimatorStateTransition back = hit.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = 0.6f;
                back.duration = 0.2f;
            }

            string[] deathNames = { "DeathBack", "DeathForward", "DeathHead" };
            for (int i = 0; i < 3; i++)
            {
                AnimatorState death = sm.AddState(deathNames[i]);
                death.motion = set.Deaths[i];
                death.speed = 1.2f;
                AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
                toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                toDeath.AddCondition(AnimatorConditionMode.Equals, i, "DeathType");
                toDeath.canTransitionToSelf = false;
                toDeath.hasExitTime = false;
                toDeath.duration = 0.12f;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Generic con Avatar dal modello (serve allo SkinnedMeshRenderer e alle clip), assi già convertiti,
        /// niente materiali importati; le clip dell'FBX ("Armature|Run") prendono il nome breve e Idle/Run sono in loop.
        /// </summary>
        private static void ConfigureModelImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                return;
            bool changed = importer.animationType != ModelImporterAnimationType.Generic
                           || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                           || !importer.importAnimation || !importer.bakeAxisConversion
                           || importer.importCameras || importer.importLights
                           || importer.materialImportMode != ModelImporterMaterialImportMode.None
                           || importer.meshCompression != ModelImporterMeshCompression.Medium
                           || !importer.isReadable;
            if (changed)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.bakeAxisConversion = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importBlendShapes = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.meshCompression = ModelImporterMeshCompression.Medium;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                importer.isReadable = true; // EnemyGore ricava i pezzi degli arti dalla mesh
                importer.SaveAndReimport();
            }

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                return;
            bool rename = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string shortName = clip.takeName.Contains("|") ? clip.takeName[(clip.takeName.LastIndexOf('|') + 1)..] : clip.takeName;
                clip.name = shortName;
                clip.loopTime = shortName is "Idle" or "Run" or "Run2" or "Jog";
                clip.loopPose = false;
                clip.lockRootRotation = false;
                clip.lockRootHeightY = false;
                clip.lockRootPositionXZ = false;
            }
            ModelImporterClipAnimation[] current = importer.clipAnimations;
            if (current == null || current.Length != clips.Length)
                rename = true;
            else
                for (int i = 0; i < clips.Length; i++)
                    rename |= current[i].name != clips[i].name || current[i].loopTime != clips[i].loopTime;
            if (rename)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
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
