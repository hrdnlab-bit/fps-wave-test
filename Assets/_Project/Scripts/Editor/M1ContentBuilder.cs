using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Object;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Net;
using HrdnLab.FpsWave.Player;
using HrdnLab.FpsWave.UI;
using HrdnLab.FpsWave.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Genera in modo ripetibile il contenuto di M1: layer, materiali greybox, armi di base,
    /// prefab di player e bersaglio, scena Greybox e collegamenti nella scena Boot.
    /// Rieseguirlo sovrascrive i file generati (non quelli fatti a mano).
    /// </summary>
    public static class M1ContentBuilder
    {
        private const string MaterialsDir = "Assets/_Project/Art/Materials";
        private const string WeaponsDir = "Assets/_Project/Data/Weapons";
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string ScenesDir = "Assets/_Project/Scenes";
        public const string GreyboxScenePath = ScenesDir + "/Greybox.unity";

        private sealed class Materials
        {
            public Material Ground, Wall, Cover, Accent, Dummy, PlayerBody, Placeholder, Tracer, Impact;
        }

        [MenuItem("FpsWave/Setup/3. Build M1 Content")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolders();
            EnsureLayers();
            Materials mats = CreateMaterials();
            WeaponDefinition rifle = CreateWeapon("AssaultRifle", "rifle_ar", "Fucile d'assalto (test)", WeaponSlot.Primary,
                damage: 18f, rpm: 600f, automatic: true, magazine: 30, reload: 2.2f, range: 120f,
                hipSpread: 2.0f, adsSpread: 0.25f, spreadPerShot: 0.25f, maxBloom: 4f,
                recoilPitch: 0.9f, recoilYaw: 0.35f, adsFov: 0.75f, adsTime: 0.22f, size: new Vector3(0.06f, 0.1f, 0.62f));
            WeaponDefinition pistol = CreateWeapon("Pistol", "pistol_base", "Pistola", WeaponSlot.Secondary,
                damage: 20f, rpm: 360f, automatic: false, magazine: 12, reload: 1.4f, range: 60f,
                hipSpread: 1.2f, adsSpread: 0.3f, spreadPerShot: 0.6f, maxBloom: 3f,
                recoilPitch: 1.6f, recoilYaw: 0.4f, adsFov: 0.85f, adsTime: 0.15f, size: new Vector3(0.05f, 0.11f, 0.22f));

            GameObject dummyPrefab = CreateDummyPrefab(mats);
            GameObject playerPrefab = CreatePlayerPrefab(mats, new[] { rifle, pistol });
            PlayerCharacterBuilder.TryAttachToPlayer(); // corpo AIRO, se già costruito con Setup/6
            CreateGreyboxScene(mats, dummyPrefab);
            WireBootScene(playerPrefab.GetComponent<NetworkObject>());

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ProjectSetupTools.BootScenePath, true),
                new EditorBuildSettingsScene(GreyboxScenePath, true),
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ProjectSetupTools.BootScenePath);
            Debug.Log("[FpsWave] Contenuto M1 generato: layer, materiali, armi, prefab Player/TrainingDummy, scena Greybox, Boot aggiornata.");
        }

        // ------------------------------------------------------------------ Progetto

        private static void EnsureFolders()
        {
            foreach (string dir in new[] { MaterialsDir, WeaponsDir, PrefabsDir, ScenesDir })
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string parent = System.IO.Path.GetDirectoryName(dir)!.Replace('\\', '/');
                    string leaf = System.IO.Path.GetFileName(dir);
                    if (!AssetDatabase.IsValidFolder(parent))
                    {
                        string grand = System.IO.Path.GetDirectoryName(parent)!.Replace('\\', '/');
                        AssetDatabase.CreateFolder(grand, System.IO.Path.GetFileName(parent));
                    }
                    AssetDatabase.CreateFolder(parent, leaf);
                }
            }
        }

        private static void EnsureLayers()
        {
            Object tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var tagManager = new SerializedObject(tagManagerAsset);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SetLayer(layers, GameLayers.Environment, GameLayers.EnvironmentName);
            SetLayer(layers, GameLayers.Player, GameLayers.PlayerName);
            SetLayer(layers, GameLayers.Hittable, GameLayers.HittableName);
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != name)
                Debug.LogWarning($"[FpsWave] Il layer {index} si chiamava '{layer.stringValue}', rinominato in '{name}'.");
            layer.stringValue = name;
        }

        // ------------------------------------------------------------------ Materiali

        private static Materials CreateMaterials()
        {
            return new Materials
            {
                Ground = LitMaterial("M_Grey_Ground", new Color(0.32f, 0.33f, 0.34f), 0.15f),
                Wall = LitMaterial("M_Grey_Wall", new Color(0.55f, 0.56f, 0.57f), 0.1f),
                Cover = LitMaterial("M_Grey_Cover", new Color(0.62f, 0.45f, 0.28f), 0.2f),
                Accent = LitMaterial("M_Grey_Accent", new Color(0.2f, 0.38f, 0.55f), 0.3f),
                Dummy = LitMaterial("M_Dummy", new Color(0.85f, 0.85f, 0.82f), 0.25f),
                PlayerBody = LitMaterial("M_PlayerBody", new Color(0.25f, 0.45f, 0.75f), 0.3f),
                Placeholder = LitMaterial("M_WeaponPlaceholder", new Color(0.12f, 0.12f, 0.13f), 0.55f),
                Tracer = UnlitMaterial("M_Tracer", new Color(1f, 0.85f, 0.45f)),
                Impact = UnlitMaterial("M_Impact", new Color(0.05f, 0.05f, 0.05f)),
            };
        }

        private static Material LitMaterial(string name, Color color, float smoothness)
        {
            Material mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material UnlitMaterial(string name, Color color)
        {
            Material mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Unlit");
            mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        // ------------------------------------------------------------------ Armi

        private static WeaponDefinition CreateWeapon(string fileName, string id, string displayName, WeaponSlot slot,
            float damage, float rpm, bool automatic, int magazine, float reload, float range, float hipSpread, float adsSpread,
            float spreadPerShot, float maxBloom, float recoilPitch, float recoilYaw, float adsFov, float adsTime, Vector3 size)
        {
            string path = $"{WeaponsDir}/{fileName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.EditorSetup(id, displayName, slot, damage, rpm, automatic, magazine, reload, range, hipSpread, adsSpread,
                spreadPerShot, maxBloom, recoilPitch, recoilYaw, adsFov, adsTime, size);
            EditorUtility.SetDirty(def);
            return def;
        }

        // ------------------------------------------------------------------ Prefab

        private static GameObject CreateDummyPrefab(Materials mats)
        {
            var root = new GameObject("TrainingDummy");
            ObjectFactory.AddComponent<NetworkObject>(root);
            var health = ObjectFactory.AddComponent<Health>(root);
            var dummy = ObjectFactory.AddComponent<TrainingDummy>(root);

            GameObject body = Primitive(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.75f, 0f),
                new Vector3(0.6f, 0.75f, 0.6f), mats.Dummy);
            var bodyZone = body.AddComponent<HitZone>();
            bodyZone.EditorSetup(1f, false);

            GameObject head = Primitive(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.68f, 0f),
                Vector3.one * 0.34f, mats.Dummy);
            var headZone = head.AddComponent<HitZone>();
            headZone.EditorSetup(1f, true);

            GameObject stand = Primitive(PrimitiveType.Cylinder, "Stand", root.transform, new Vector3(0f, 0.05f, 0f),
                new Vector3(0.7f, 0.05f, 0.7f), mats.Accent);
            Object.DestroyImmediate(stand.GetComponent<Collider>());

            health.EditorSetup(150f, new[] { bodyZone, headZone });
            dummy.EditorSetup(new Renderer[] { body.GetComponent<Renderer>(), head.GetComponent<Renderer>() });
            SetLayerRecursively(root, GameLayers.Hittable);

            return SavePrefab(root, $"{PrefabsDir}/TrainingDummy.prefab");
        }

        private static GameObject CreatePlayerPrefab(Materials mats, WeaponDefinition[] loadout)
        {
            var root = new GameObject("Player");
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.radius = 0.35f;
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.04f;

            ObjectFactory.AddComponent<NetworkObject>(root);
            var netTransform = ObjectFactory.AddComponent<NetworkTransform>(root);
            ConfigureNetworkTransform(netTransform);

            var input = ObjectFactory.AddComponent<PlayerInputReader>(root);
            var motor = ObjectFactory.AddComponent<PlayerMotor>(root);
            var look = ObjectFactory.AddComponent<PlayerLook>(root);
            var weapons = ObjectFactory.AddComponent<PlayerWeapons>(root);
            var health = ObjectFactory.AddComponent<Health>(root);
            var view = ObjectFactory.AddComponent<PlayerView>(root);
            var hud = root.AddComponent<PlayerHud>();

            GameObject body = Primitive(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.9f, 0f),
                new Vector3(0.7f, 0.9f, 0.7f), mats.PlayerBody);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.62f, 0f);
            var camera = pivot.gameObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 600f;
            camera.fieldOfView = 90f;
            var listener = pivot.gameObject.AddComponent<AudioListener>();

            var viewModelRoot = new GameObject("ViewModelRoot").transform;
            viewModelRoot.SetParent(pivot, false);
            viewModelRoot.localPosition = new Vector3(0.22f, -0.2f, 0.42f);

            var lightGo = new GameObject("MuzzleLight");
            lightGo.transform.SetParent(viewModelRoot, false);
            var muzzleLight = lightGo.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.range = 5f;
            muzzleLight.intensity = 4f;
            muzzleLight.color = new Color(1f, 0.75f, 0.4f);
            muzzleLight.shadows = LightShadows.None;
            muzzleLight.enabled = false;

            motor.EditorSetup(input, pivot);
            look.EditorSetup(input, pivot, camera);
            weapons.EditorSetup(loadout, startingWeapon: 1, input, look, motor, viewModelRoot, muzzleLight,
                mats.Tracer, mats.Impact, mats.Placeholder);
            health.EditorSetup(100f, System.Array.Empty<HitZone>());
            hud.EditorSetup(weapons, health);
            view.EditorSetup(camera, listener, new Renderer[] { body.GetComponent<Renderer>() }, viewModelRoot.gameObject,
                new Behaviour[] { hud });

            SetLayerRecursively(root, GameLayers.Player);
            return SavePrefab(root, $"{PrefabsDir}/Player.prefab");
        }

        private static void ConfigureNetworkTransform(NetworkTransform netTransform)
        {
            var so = new SerializedObject(netTransform);
            SetBool(so, "_clientAuthoritative", true);
            SetBool(so, "_sendToOwner", false);
            SetBool(so, "_synchronizeScale", false);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(SerializedObject so, string property, bool value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
                p.boolValue = value;
            else
                Debug.LogWarning($"[FpsWave] Proprietà '{property}' non trovata su {so.targetObject.GetType().Name}.");
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ Scena Greybox

        private static void CreateGreyboxScene(Materials mats, GameObject dummyPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            var env = new GameObject("Environment").transform;

            // Terreno e perimetro (160 x 160 m).
            Block(env, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(160f, 1f, 160f), mats.Ground);
            Block(env, "Wall_N", new Vector3(0f, 3f, 80f), new Vector3(160f, 6f, 1f), mats.Wall);
            Block(env, "Wall_S", new Vector3(0f, 3f, -80f), new Vector3(160f, 6f, 1f), mats.Wall);
            Block(env, "Wall_E", new Vector3(80f, 3f, 0f), new Vector3(1f, 6f, 160f), mats.Wall);
            Block(env, "Wall_W", new Vector3(-80f, 3f, 0f), new Vector3(1f, 6f, 160f), mats.Wall);

            // Poligono di tiro: corsia con bersagli a 10, 20, 35 e 50 m dalla linea di spawn (z = -60).
            Block(env, "Range_Line", new Vector3(0f, 0.02f, -57f), new Vector3(14f, 0.04f, 0.3f), mats.Accent);
            Block(env, "Range_Wall_L", new Vector3(-8f, 1f, -35f), new Vector3(0.5f, 2f, 40f), mats.Wall);
            Block(env, "Range_Wall_R", new Vector3(8f, 1f, -35f), new Vector3(0.5f, 2f, 40f), mats.Wall);
            var dummies = new GameObject("TrainingDummies").transform;
            float[] distances = { 10f, 20f, 35f, 50f };
            float[] lanes = { -4f, 0f, 4f };
            foreach (float d in distances)
                foreach (float x in lanes)
                    if (d < 30f || Mathf.Approximately(x, 0f) || Mathf.Approximately(x, 4f))
                        PlaceDummy(dummyPrefab, dummies, new Vector3(x, 0f, -60f + d));

            // Test della linea di vista: un muro con un bersaglio dietro.
            Block(env, "LoS_Wall", new Vector3(-20f, 2f, -30f), new Vector3(8f, 4f, 1f), mats.Wall);
            PlaceDummy(dummyPrefab, dummies, new Vector3(-20f, 0f, -26f));

            // Campo di cover di varie altezze (mezza altezza, piena, casse).
            var cover = new (Vector3 pos, Vector3 size)[]
            {
                (new Vector3(-12f, 0.5f, 5f), new Vector3(3f, 1f, 1f)),
                (new Vector3(-4f, 1f, 10f), new Vector3(2f, 2f, 2f)),
                (new Vector3(6f, 0.6f, 4f), new Vector3(4f, 1.2f, 1f)),
                (new Vector3(14f, 1.5f, 14f), new Vector3(1f, 3f, 6f)),
                (new Vector3(-18f, 0.75f, 20f), new Vector3(1.5f, 1.5f, 1.5f)),
                (new Vector3(0f, 0.5f, 22f), new Vector3(6f, 1f, 1f)),
                (new Vector3(-8f, 2f, 30f), new Vector3(10f, 4f, 1f)),
                (new Vector3(10f, 0.75f, 32f), new Vector3(1.5f, 1.5f, 1.5f)),
                (new Vector3(11.6f, 0.75f, 32f), new Vector3(1.5f, 1.5f, 1.5f)),
                (new Vector3(10.8f, 2.25f, 32f), new Vector3(1.5f, 1.5f, 1.5f)),
            };
            for (int i = 0; i < cover.Length; i++)
                Block(env, $"Cover_{i:00}", cover[i].pos, cover[i].size, mats.Cover);

            // Verticalità: piattaforma a 3 m con rampa (pendenza ~17°).
            Block(env, "Platform", new Vector3(30f, 1.5f, 10f), new Vector3(8f, 3f, 8f), mats.Wall);
            GameObject ramp = Block(env, "Ramp", new Vector3(30f, 1.45f, 1.3f), new Vector3(4f, 0.4f, 10.2f), mats.Accent);
            ramp.transform.rotation = Quaternion.Euler(-17.5f, 0f, 0f);
            Block(env, "Platform_Rail", new Vector3(30f, 3.5f, 14f), new Vector3(8f, 1f, 0.2f), mats.Accent);

            // Corridoio stretto per provare crouch e passaggi bassi (soffitto a 1.4 m).
            Block(env, "Tunnel_Roof", new Vector3(-35f, 1.6f, 0f), new Vector3(3f, 0.4f, 10f), mats.Wall);
            Block(env, "Tunnel_L", new Vector3(-36.7f, 0.7f, 0f), new Vector3(0.4f, 1.4f, 10f), mats.Wall);
            Block(env, "Tunnel_R", new Vector3(-33.3f, 0.7f, 0f), new Vector3(0.4f, 1.4f, 10f), mats.Wall);

            SetLayerRecursively(env.gameObject, GameLayers.Environment);
            foreach (Transform child in env)
                GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            var spawns = new GameObject("PlayerSpawns").transform;
            // Allineati alle corsie dei bersagli (x = 0, -4, 4) per poter mirare in modo ripetibile nei test.
            float[] spawnX = { 0f, -4f, 4f, 2f };
            for (int i = 0; i < spawnX.Length; i++)
            {
                var sp = new GameObject($"Spawn_{i}");
                sp.transform.SetParent(spawns, false);
                sp.transform.position = new Vector3(spawnX[i], 0.05f, -62f);
                sp.transform.rotation = Quaternion.identity;
                sp.AddComponent<PlayerSpawnPoint>();
            }

            AssignSceneIds(scene, GreyboxScenePath);
            EditorSceneManager.SaveScene(scene, GreyboxScenePath);
        }

        /// <summary>
        /// FishNet assegna gli ID degli oggetti di rete di scena solo quando la scena viene salvata dall'interfaccia.
        /// Per le scene generate da script li assegniamo qui: stabili (derivati da percorso scena + indice) e unici.
        /// </summary>
        private static void AssignSceneIds(Scene scene, string scenePath)
        {
            var used = new System.Collections.Generic.HashSet<ulong>();
            int index = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NetworkObject nob in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    ulong id = StableHash($"{scenePath}#{index++}");
                    while (id == 0 || !used.Add(id))
                        id = StableHash($"{scenePath}#{index++}");
                    nob.SetSceneId(id);
                    EditorUtility.SetDirty(nob);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(nob);
                }
            }
        }

        private static ulong StableHash(string text)
        {
            // FNV-1a 64 bit: deterministico tra macchine e versioni di .NET (a differenza di string.GetHashCode).
            ulong hash = 14695981039346656037UL;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        private static void PlaceDummy(GameObject prefab, Transform parent, Vector3 position)
        {
            var dummy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            dummy.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
        }

        private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
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

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        // ------------------------------------------------------------------ Boot

        private static void WireBootScene(NetworkObject playerPrefab)
        {
            Scene boot = EditorSceneManager.OpenScene(ProjectSetupTools.BootScenePath, OpenSceneMode.Single);
            NetworkManager manager = Object.FindFirstObjectByType<NetworkManager>();
            if (manager == null)
            {
                Debug.LogError("[FpsWave] NetworkManager non trovato nella scena Boot: eseguire prima 'Create Boot Scene'.");
                return;
            }
            GameObject go = manager.gameObject;
            if (!go.TryGetComponent(out GameSession session))
                session = go.AddComponent<GameSession>();
            session.EditorSetup(manager, "Greybox", "Boot");
            if (!go.TryGetComponent(out PlayerSpawnService spawner))
                spawner = go.AddComponent<PlayerSpawnService>();
            spawner.EditorSetup(manager, playerPrefab);
            EditorUtility.SetDirty(session);
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(boot);
            EditorSceneManager.SaveScene(boot);
        }
    }
}
