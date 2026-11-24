using System.Collections.Generic;
using System.Linq;
using HrdnLab.FpsWave.Weapons;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Costruisce i modelli in prima persona realistici (M1.6) dagli asset di terze parti:
    /// impostazioni di import, materiali URP, Animator Controller, prefab allineato alla camera e collegamento
    /// alla <see cref="WeaponDefinition"/>. Ripetibile: rieseguirlo sovrascrive i risultati in modo deterministico.
    /// </summary>
    public static class WeaponViewModelBuilder
    {
        private const string M4Dir = "Assets/ThirdParty/Fab/M4Animations";
        private const string M4Fbx = M4Dir + "/M4.fbx";
        private const string MaterialsDir = "Assets/_Project/Art/Materials/Weapons";
        private const string AnimationsDir = "Assets/_Project/Art/Animations";
        private const string ViewModelsDir = "Assets/_Project/Prefabs/ViewModels";
        private const string RifleAsset = "Assets/_Project/Data/Weapons/AssaultRifle.asset";
        private const string PistolAsset = "Assets/_Project/Data/Weapons/Pistol.asset";
        private const string PistolDir = "Assets/ThirdParty/Fab/Pistol2";
        private const string PistolFbx = PistolDir + "/Pistol2.fbx";
        private const string PistolSourcePref = "FpsWave.PistolSourceDir";
        private const float PistolLength = 0.217f; // Beretta M92: 217 mm
        private static readonly string[] PistolParts = { "Body", "Slide", "Bullet" };
        private const string LitShader = "Universal Render Pipeline/Lit";
        // Armi sci-fi (Fab): il fucile di M@xim montato sulle braccia animate del pacchetto M4, la pistola Magma di AF Creations.
        private const string SciRifleDir = "Assets/ThirdParty/Fab/SciFiRifle";
        private const string SciRifleFbx = SciRifleDir + "/SciRifle.fbx";
        private const float HoloEyeRelief = 0.12f; // distanza occhio-vetro del mirino olografico mirando (m)
        private const string SciPistolDir = "Assets/ThirdParty/Fab/SciFiPistol";
        private const string SciPistolFbx = SciPistolDir + "/SciFiPistol.fbx";
        private const float SciPistolLength = 0.24f;
        private static readonly string[] M4Parts = { "Main", "Safety", "Mag1", "Mag2", "Bolt", "BoltCatch", "Trigger" };

        [MenuItem("FpsWave/Setup/4. Build Weapon View Models")]
        public static void BuildAll()
        {
            EnsureFolder(MaterialsDir);
            EnsureFolder(AnimationsDir);
            EnsureFolder(ViewModelsDir);

            // Import del fucile sci-fi prima di creare il controller: un reimport a metà costruzione lascia riferimenti non validi.
            if (System.IO.File.Exists(SciRifleFbx))
                ConfigureSciRifleModel();

            if (System.IO.File.Exists(M4Fbx))
            {
                ConfigureTextures(M4Dir);
                var materials = CreateM4Materials();
                ConfigureModel(M4Fbx, materials);
                BuildM4Prefab();
                Debug.Log("[FpsWave] View model M4 costruito e collegato a AssaultRifle.");
            }
            else
            {
                Debug.LogWarning($"[FpsWave] {M4Fbx} non trovato: estrai il pacchetto Fab 'M4 - FPS Weapon Animations Pack FREE' in {M4Dir}.");
            }

            if (System.IO.File.Exists(SciPistolFbx))
            {
                BuildSciPistolPrefab();
                Debug.Log("[FpsWave] View model pistola sci-fi (Magma) costruito e collegato a Pistol.");
            }
            else if (ImportPistolFiles())
            {
                ConfigureTextures(PistolDir, maxSize: 2048);
                Dictionary<string, Material> pistolMats = CreatePistolMaterials();
                ConfigurePistolModel(pistolMats);
                BuildPistolPrefab(pistolMats);
                Debug.Log("[FpsWave] View model pistola costruito e collegato a Pistol.");
            }
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- Pistola (FPS Guns 4K - Pistol 2)

        /// <summary>
        /// Copia il pacchetto dalla cartella estratta (default: Download) e unisce Metallic + Roughness in una
        /// mappa MetallicSmoothness per URP. Le texture 4K vengono poi importate a 2K (budget VRAM).
        /// </summary>
        private static bool ImportPistolFiles()
        {
            if (System.IO.File.Exists(PistolFbx))
                return true;
            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            string source = EditorPrefs.GetString(PistolSourcePref,
                System.IO.Path.Combine(home, "Downloads", "FPS Guns 4K - Pistol 2"));
            string sourceFbx = System.IO.Path.Combine(source, "Models", "Pistol 2.fbx");
            if (!System.IO.File.Exists(sourceFbx))
            {
                Debug.LogWarning($"[FpsWave] Pistola: {sourceFbx} non trovato. Estrai il pacchetto Fab 'FPS Guns 4K - Pistol 2' in Download " +
                                 $"(o imposta EditorPrefs '{PistolSourcePref}').");
                return false;
            }

            System.IO.Directory.CreateDirectory(PistolDir);
            System.IO.File.Copy(sourceFbx, PistolFbx, true);
            foreach (string part in PistolParts)
            {
                string dir = System.IO.Path.Combine(source, "Textures", "4K", $"Pistol 2 {part}");
                System.IO.File.Copy(System.IO.Path.Combine(dir, $"{part}_Base_Color.png"), $"{PistolDir}/{part}_BaseColor.png", true);
                System.IO.File.Copy(System.IO.Path.Combine(dir, $"{part}_Normal_OpenGL.png"), $"{PistolDir}/{part}_Normal.png", true);
                System.IO.File.Copy(System.IO.Path.Combine(dir, $"{part}_Mixed_AO.png"), $"{PistolDir}/{part}_AO.png", true);
                PackMetallicSmoothness(System.IO.Path.Combine(dir, $"{part}_Metallic.png"),
                    System.IO.Path.Combine(dir, $"{part}_Roughness.png"), $"{PistolDir}/{part}_MetalSmooth.png");
            }
            AssetDatabase.Refresh();
            return true;
        }

        private static void PackMetallicSmoothness(string metallicPath, string roughnessPath, string outPath)
        {
            var metal = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var rough = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            metal.LoadImage(System.IO.File.ReadAllBytes(metallicPath));
            rough.LoadImage(System.IO.File.ReadAllBytes(roughnessPath));
            Color32[] m = metal.GetPixels32();
            Color32[] r = rough.width == metal.width && rough.height == metal.height ? rough.GetPixels32() : null;
            for (int i = 0; i < m.Length; i++)
            {
                byte roughness = r != null ? r[i].r : (byte)128;
                m[i] = new Color32(m[i].r, 0, 0, (byte)(255 - roughness));
            }
            metal.SetPixels32(m);
            metal.Apply(false);
            System.IO.File.WriteAllBytes(outPath, metal.EncodeToPNG());
            Object.DestroyImmediate(metal);
            Object.DestroyImmediate(rough);
        }

        private static Dictionary<string, Material> CreatePistolMaterials()
        {
            var result = new Dictionary<string, Material>();
            foreach (string part in PistolParts)
            {
                Material mat = LoadOrCreate($"Pistol_{part}");
                SetCommon(mat, $"{PistolDir}/{part}_BaseColor.png", $"{PistolDir}/{part}_Normal.png");
                mat.SetFloat("_WorkflowMode", 1f);
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PistolDir}/{part}_MetalSmooth.png"));
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", 1f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.DisableKeyword("_SPECULAR_SETUP");
                mat.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PistolDir}/{part}_AO.png"));
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.EnableKeyword("_OCCLUSIONMAP");
                EditorUtility.SetDirty(mat);
                result[part] = mat;
            }
            return result;
        }

        private static void ConfigurePistolModel(Dictionary<string, Material> mats)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(PistolFbx);
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            // I nomi dei materiali nel file hanno un namespace Maya; si mappano entrambe le varianti.
            string[,] map = { { "Baretta:Body", "Body" }, { "Body", "Body" }, { "Lower_BodySG", "Body" },
                              { "Baretta:Slide", "Slide" }, { "Slide", "Slide" },
                              { "Baretta:Bullet", "Bullet" }, { "Bullet", "Bullet" } };
            for (int i = 0; i < map.GetLength(0); i++)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), map[i, 0]), mats[map[i, 1]]);
            importer.SaveAndReimport();
        }

        private static void BuildPistolPrefab(Dictionary<string, Material> mats)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PistolFbx);
            var root = new GameObject("VM_Pistol");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(model);
            rig.transform.SetParent(root.transform, false);

            // Scala: lunghezza reale dell'arma.
            Bounds b = WorldBounds(rig.transform);
            float length = Mathf.Max(b.size.x, b.size.z);
            rig.transform.localScale *= PistolLength / Mathf.Max(0.0001f, length);

            // Orientamento: canna verso +Z (il mirino anteriore indica il davanti), caricatore verso il basso.
            Transform frontSight = FindByName(rig.transform, "Front_Sight_");
            Transform slide = FindByName(rig.transform, "Slide1");
            Transform magazine = FindByName(rig.transform, "Magazine");
            b = WorldBounds(rig.transform);
            Vector3 axis = b.size.x > b.size.z ? Vector3.right : Vector3.forward;
            Vector3 front = frontSight != null ? WorldBounds(frontSight).center : b.center + axis;
            if (Vector3.Dot(front - b.center, axis) < 0f) axis = -axis;
            rig.transform.rotation = Quaternion.FromToRotation(axis, Vector3.forward) * rig.transform.rotation;
            if (slide != null && magazine != null && WorldBounds(magazine).center.y > WorldBounds(slide).center.y)
                rig.transform.rotation = Quaternion.AngleAxis(180f, Vector3.forward) * rig.transform.rotation;

            // Origine sulla tacca di mira: in ADS basta portarla sull'asse della camera.
            Transform rearSight = FindByName(rig.transform, "Rear_Sight");
            b = WorldBounds(rig.transform);
            Bounds sightBounds = rearSight != null ? WorldBounds(rearSight) : b;
            Vector3 sight = new(b.center.x, sightBounds.max.y - 0.002f, sightBounds.center.z);
            rig.transform.position -= sight;

            Transform barrel = FindByName(rig.transform, "Barrel") ?? slide;
            Vector3[] barrelVerts = WorldVertices(barrel);
            Vector3 muzzlePoint = barrelVerts.Length > 0 ? MuzzlePoint(barrelVerts) : new Vector3(0f, -0.01f, WorldBounds(rig.transform).max.z);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(barrel != null ? barrel : rig.transform, true);
            muzzle.position = muzzlePoint + Vector3.forward * 0.01f;
            muzzle.rotation = Quaternion.identity;

            // I nomi dei materiali nell'FBX non sempre coincidono con le rimappature: si assegnano per parte.
            foreach (Renderer r in rig.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Material[] slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string n = slots[i] != null ? slots[i].name : r.name;
                    slots[i] = n.Contains("Slide") ? mats["Slide"] : n.Contains("Bullet") ? mats["Bullet"] : mats["Body"];
                }
                r.sharedMaterials = slots;
            }

            bool hasSlide = slide != null;
            float slideTravel = 0.022f;
            if (slide != null && slide.parent != null)
                slideTravel /= Mathf.Max(0.0001f, slide.parent.lossyScale.x);

            float scale = rig.transform.localScale.x;
            var vm = root.AddComponent<WeaponViewModel>();
            vm.EditorSetupProcedural(rig.transform, slide, muzzle, slideTravel);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ViewModelsDir}/VM_Pistol.prefab");
            Object.DestroyImmediate(root);

            var pistol = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(PistolAsset);
            if (pistol != null)
            {
                pistol.EditorSetViewModel(prefab, new Vector3(0.13f, -0.105f, 0.30f), new Vector3(0f, 0f, 0.24f));
                EditorUtility.SetDirty(pistol);
            }
            Debug.Log($"[FpsWave] VM_Pistol: scala {scale:F4}, volata {muzzlePoint:F3}, carrello {(hasSlide ? "sì" : "no")}.");
        }

        private static Transform FindByName(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static Bounds WorldBounds(Transform t)
        {
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        // ---------------------------------------------------------------- Texture e materiali

        private static void ConfigureTextures(string dir, int maxSize = 4096)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                bool isNormal = file.EndsWith("_N") || file.EndsWith("_Normal");
                bool isLinearData = file.EndsWith("_MetalSmooth") || file.EndsWith("_AO");
                // La mappa specular è un colore (sRGB); la smoothness nel canale alfa è sempre lineare.
                TextureImporterType type = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                bool srgb = !isNormal && !isLinearData;
                if (importer.textureType == type && importer.sRGBTexture == srgb && importer.streamingMipmaps
                    && importer.maxTextureSize == maxSize)
                    continue;
                importer.maxTextureSize = maxSize;
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.streamingMipmaps = true; // texture streaming (docs/TECH_BUDGETS.md)
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, Material> CreateM4Materials()
        {
            string packed = M4Dir + "/Packed";
            return new Dictionary<string, Material>
            {
                ["Hand_D"] = SpecularMaterial("M4_Hand", $"{M4Dir}/Hand_D.jpg", $"{M4Dir}/Hand_N.png", $"{packed}/Hand_SpecSmooth.png"),
                ["Glove_D"] = SpecularMaterial("M4_Glove", $"{M4Dir}/Glove_D.png", $"{M4Dir}/Glove_N.png", $"{packed}/Glove_SpecSmooth.png"),
                ["M4_D"] = MetallicMaterial("M4_Weapon", $"{M4Dir}/M4_D.png", $"{M4Dir}/M4_N.png", 0.55f, 0.42f),
                ["Bullet5_56x45mm_D"] = MetallicMaterial("M4_Bullet", $"{M4Dir}/Bullet5.56x45mm_D.png", $"{M4Dir}/Bullet5.56x45mm_N.png", 0.9f, 0.6f),
            };
        }

        private static Material LoadOrCreate(string name)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(LitShader)) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader.name != LitShader)
            {
                mat.shader = Shader.Find(LitShader);
            }
            return mat;
        }

        private static Material SpecularMaterial(string name, string albedo, string normal, string specSmooth)
        {
            Material mat = LoadOrCreate(name);
            SetCommon(mat, albedo, normal);
            mat.SetFloat("_WorkflowMode", 0f); // 0 = Specular
            mat.SetColor("_SpecColor", Color.white);
            mat.SetTexture("_SpecGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(specSmooth));
            mat.SetFloat("_SmoothnessTextureChannel", 0f); // alfa della mappa specular
            mat.SetFloat("_Smoothness", 1f);
            mat.EnableKeyword("_SPECULAR_SETUP");
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material MetallicMaterial(string name, string albedo, string normal, float metallic, float smoothness)
        {
            Material mat = LoadOrCreate(name);
            SetCommon(mat, albedo, normal);
            mat.SetFloat("_WorkflowMode", 1f); // 1 = Metallic
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            mat.DisableKeyword("_SPECULAR_SETUP");
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetCommon(Material mat, string albedo, string normal)
        {
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            mat.SetColor("_BaseColor", Color.white);
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
            mat.SetTexture("_BumpMap", n);
            if (n != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1f);
            mat.enableInstancing = true;
        }

        // ---------------------------------------------------------------- Modello e animazioni

        private static void ConfigureModel(string path, Dictionary<string, Material> materials)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = true; // serve al builder per calcolare volata e mirino dai vertici
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = clip.name == "Idle";
                clip.lockRootRotation = false;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static AnimationClip FindClip(Object[] assets, string name) =>
            assets.OfType<AnimationClip>().FirstOrDefault(c => c.name == name && !c.name.StartsWith("__preview__"));

        private static AnimatorController BuildController(Object[] assets)
        {
            string path = $"{AnimationsDir}/M4_ViewModel.controller";
            // Ricostruito sul posto (non cancellato e ricreato): stesso oggetto, i prefab che lo usano restano collegati.
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }
            else
            {
                foreach (AnimatorControllerParameter p in controller.parameters)
                    controller.RemoveParameter(p);
                AnimatorStateMachine old = controller.layers[0].stateMachine;
                foreach (ChildAnimatorState st in old.states)
                    old.RemoveState(st.state);
                foreach (AnimatorStateTransition t in old.anyStateTransitions)
                    old.RemoveAnyStateTransition(t);
            }
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = WeaponViewModel.ReloadSpeedParameter,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            });

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idle = sm.AddState(WeaponViewModel.IdleState);
            idle.motion = FindClip(assets, "Idle");
            sm.defaultState = idle;

            AddOneShot(sm, idle, WeaponViewModel.DrawState, FindClip(assets, "Draw"), null);
            AddOneShot(sm, idle, WeaponViewModel.FireState, FindClip(assets, "Fire"), null);
            AddOneShot(sm, idle, WeaponViewModel.ReloadState, FindClip(assets, "Reload"), WeaponViewModel.ReloadSpeedParameter);
            AddOneShot(sm, idle, WeaponViewModel.ReloadEmptyState, FindClip(assets, "Reload_Empty"), WeaponViewModel.ReloadSpeedParameter);
            // Cambio arma: l'arma scende (Holster) e resta giù finché il modello non viene sostituito.
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = WeaponViewModel.HolsterSpeedParameter,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            });
            AnimatorState holster = sm.AddState(WeaponViewModel.HolsterState);
            holster.motion = FindClip(assets, "Holster");
            holster.speedParameter = WeaponViewModel.HolsterSpeedParameter;
            holster.speedParameterActive = true;
            return controller;
        }

        private static void AddOneShot(AnimatorStateMachine sm, AnimatorState idle, string name, Motion clip, string speedParameter)
        {
            AnimatorState state = sm.AddState(name);
            state.motion = clip;
            if (speedParameter != null)
            {
                state.speedParameter = speedParameter;
                state.speedParameterActive = true;
            }
            AnimatorStateTransition back = state.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.duration = 0.08f;
            back.hasFixedDuration = true;
        }

        private static void BuildM4Prefab()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(M4Fbx);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(M4Fbx);
            AnimatorController controller = BuildController(assets);

            bool sciFi = System.IO.File.Exists(SciRifleFbx);
            var root = new GameObject(sciFi ? "VM_SciRifle" : "VM_M4");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(model);
            rig.transform.SetParent(root.transform, false);

            // Posa di riferimento: primo frame di Idle.
            AnimationClip idle = FindClip(assets, "Idle");
            if (idle != null)
                idle.SampleAnimation(rig, 0f);
            if (sciFi)
            {
                PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                if (!AttachSciRifle(rig.transform))
                    sciFi = false;
            }

            Transform cameraBone = FindDeep(rig.transform, "FPS_Camera");
            Transform weaponMesh = FindDeep(rig.transform, "Main");
            Vector3[] weaponVerts = WorldVertices(weaponMesh);
            if (cameraBone == null || weaponVerts.Length == 0)
            {
                Object.DestroyImmediate(root);
                Debug.LogError("[FpsWave] M4.fbx: osso FPS_Camera o mesh Main non trovati.");
                return;
            }

            // Orientamento: la canna deve puntare verso +Z del root (avanti della camera).
            Vector3 eye = cameraBone.position;
            Vector3 barrel = BarrelDirection(weaponVerts, eye);
            Quaternion align = Quaternion.FromToRotation(barrel, Vector3.forward);
            rig.transform.localRotation = align * rig.transform.localRotation;
            // La camera del rig coincide con la camera del player (origine del root).
            if (idle != null)
                idle.SampleAnimation(rig, 0f);
            rig.transform.position -= cameraBone.position;

            Quaternion cameraRest = cameraBone.rotation; // root in identità: rotazione rispetto al root
            weaponVerts = WorldVertices(weaponMesh);
            Vector3 muzzlePoint = MuzzlePoint(weaponVerts);
            Vector3 sightPoint = sciFi ? ScopeSightPoint(weaponVerts, muzzlePoint) : SightPoint(weaponVerts, muzzlePoint);
            // Con il mirino olografico si mira al centro del vetro (il reticolo è lì).
            Transform lensT = sciFi ? FindDeep(weaponMesh, "Lens") : null;
            bool holo = lensT != null && lensT.TryGetComponent(out Renderer lensRenderer);
            if (holo)
                sightPoint = lensT.GetComponent<Renderer>().bounds.center;

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(weaponMesh, true);
            muzzle.position = muzzlePoint + Vector3.forward * 0.01f;
            muzzle.rotation = Quaternion.identity;

            if (!rig.TryGetComponent(out Animator animator))
                animator = rig.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{AnimationsDir}/M4_ViewModel.controller") ?? controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (Renderer r in rig.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                if (r is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true; // il rig è a pochi cm dalla camera
            }

            var vm = root.AddComponent<WeaponViewModel>();
            vm.EditorSetup(animator, muzzle, ClipLength(assets, "Reload"), ClipLength(assets, "Reload_Empty"),
                rig.transform, cameraBone, cameraRest);
            vm.EditorSetHolsterClipLength(ClipLength(assets, "Holster"));

            string prefabPath = $"{ViewModelsDir}/{root.name}.prefab";
            EditorUtility.SetDirty(animator.runtimeAnimatorController);
            AssetDatabase.SaveAssets();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            // In ADS il mirino va sull'asse della camera; un piccolo arretramento evita di avere l'arma "in faccia".
            Vector3 ads = new(-sightPoint.x, -sightPoint.y, -0.02f);
            // Mirino olografico: il vetro a 12 cm dall'occhio, così la finestra riempie bene il centro dello schermo.
            if (holo) // (il root e il vetro sono già distrutti qui: si usa il flag)
                ads.z = HoloEyeRelief - sightPoint.z;
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RifleAsset);
            if (rifle != null)
            {
                rifle.EditorSetViewModel(prefab, Vector3.zero, ads);
                EditorUtility.SetDirty(rifle);
            }
            else
            {
                Debug.LogWarning($"[FpsWave] {RifleAsset} non trovato: esegui prima 'FpsWave/Setup/3. Build M1 Content'.");
            }

            Debug.Log($"[FpsWave] VM_M4: volata {muzzlePoint:F3}, mirino {sightPoint:F3}, ADS {ads:F3}, " +
                      $"ricarica {ClipLength(assets, "Reload"):F2}s / vuoto {ClipLength(assets, "Reload_Empty"):F2}s.");
        }

        // ---------------------------------------------------------------- Fucile sci-fi sulle braccia M4

        /// <summary>
        /// Sostituisce l'M4 con il fucile sci-fi sugli stessi ossi (Main_j, Trigger_j, Mag1_j, Mag2_j), così le clip del
        /// pacchetto (estrazione, sparo, ricarica, ricarica a vuoto, holster) restano valide. Allineamento nella posa Idle:
        /// grilletto sul grilletto, canna sulla canna, alto dal caricatore al corpo, stessa lunghezza di presa (grilletto -> calcio).
        /// Le parti prendono i nomi dell'M4 (Main, Trigger, Mag1, Mag2): il resto del builder e quello delle armi in terza persona non cambiano.
        /// </summary>
        private static bool AttachSciRifle(Transform rig)
        {
            Transform m4Main = FindDeep(rig, "Main");
            Transform m4Trigger = FindByName(rig, "Trigger");
            Transform m4Mag = FindByName(rig, "Mag1");
            Transform mag2Bone = FindByName(rig, "Mag2_j");
            Transform cam = FindDeep(rig, "FPS_Camera");
            if (m4Main == null || m4Trigger == null || m4Mag == null || mag2Bone == null || cam == null)
            {
                Debug.LogWarning("[FpsWave] Fucile sci-fi: ossi o parti dell'M4 non trovati, resta l'M4.");
                return false;
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(SciRifleFbx);
            var sci = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(sci, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            sci.transform.position = Vector3.zero;
            Transform body = FindByName(sci.transform, "SciRifle_Body");
            Transform trig = FindByName(sci.transform, "SciRifle_Trigger");
            Transform mag = FindByName(sci.transform, "SciRifle_Mag");
            Transform lens = FindByName(sci.transform, "SciRifle_Lens"); // vetro del mirino olografico (può mancare)
            if (body == null || trig == null || mag == null)
            {
                Object.DestroyImmediate(sci);
                Debug.LogWarning("[FpsWave] SciRifle.fbx: parti SciRifle_Body/Trigger/Mag non trovate, resta l'M4.");
                return false;
            }

            // Riferimenti sull'M4
            Vector3[] mv = WorldVertices(m4Main);
            Vector3 axis = BarrelDirection(mv, cam.position);
            Vector3 trigM4 = WorldBounds(m4Trigger).center;
            Vector3 upM4 = Vector3.ProjectOnPlane(Average(mv) - WorldBounds(m4Mag).center, axis).normalized;
            float lopM4 = Vector3.Dot(trigM4, axis) - mv.Min(v => Vector3.Dot(v, axis));

            // Riferimenti sul fucile sci-fi: volata = estremità con la sezione più piccola
            Vector3[] bv = WorldVertices(body);
            var bb = new Bounds(bv[0], Vector3.zero);
            foreach (Vector3 v in bv) bb.Encapsulate(v);
            int li = bb.size.x >= bb.size.y && bb.size.x >= bb.size.z ? 0 : bb.size.y >= bb.size.z ? 1 : 2;
            Vector3 longAxis = Vector3.zero; longAxis[li] = 1f;
            Vector3 bAxis = EndSection(bv, longAxis, bb, true) < EndSection(bv, longAxis, bb, false) ? longAxis : -longAxis;
            Vector3 tc = WorldBounds(trig).center;
            Vector3 bUp = Vector3.ProjectOnPlane(Average(bv) - WorldBounds(mag).center, bAxis).normalized;
            float lopB = Vector3.Dot(tc, bAxis) - bv.Min(v => Vector3.Dot(v, bAxis));
            float scale = lopM4 / Mathf.Max(1e-4f, lopB);
            float length = bb.size[li] * scale;
            if (length > 1f) { scale /= length; length = 1f; }

            Quaternion r = Quaternion.LookRotation(axis, upM4) * Quaternion.Inverse(Quaternion.LookRotation(bAxis, bUp));
            sci.transform.localScale *= scale;
            sci.transform.rotation = r * sci.transform.rotation;
            sci.transform.position = trigM4 - r * (tc * scale);

            // Caricatore nuovo della ricarica: stessa posa rispetto a Mag2_j di quello inserito rispetto a Mag1_j.
            Transform mag1Bone = m4Mag.parent;
            Matrix4x4 m2 = mag2Bone.localToWorldMatrix * mag1Bone.worldToLocalMatrix * mag.localToWorldMatrix;
            Transform mag2 = Object.Instantiate(mag.gameObject).transform;
            mag2.localScale = mag.lossyScale;
            mag2.SetPositionAndRotation(m2.GetColumn(3), m2.rotation);

            Transform mainBone = m4Main.parent, trigBone = m4Trigger.parent;
            if (lens != null)
            {
                lens.SetParent(body, true); // segue il corpo dell'arma in tutte le animazioni
                lens.name = "Lens";
                if (lens.TryGetComponent(out Renderer lr))
                {
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                }
            }
            body.SetParent(mainBone, true);
            trig.SetParent(trigBone, true);
            mag.SetParent(mag1Bone, true);
            mag2.SetParent(mag2Bone, true);
            Object.DestroyImmediate(sci);

            // Via le parti dell'M4 (e i proiettili figli), poi le parti nuove ne prendono i nomi.
            var old = rig.GetComponentsInChildren<Transform>(true)
                .Where(t => t != null && t != body && t != trig && t != mag && t != mag2
                            && (M4Parts.Contains(t.name) || t.name.StartsWith("Bullet5")) && t.GetComponent<Renderer>() != null)
                .ToList();
            foreach (Transform t in old)
                if (t != null) Object.DestroyImmediate(t.gameObject);
            body.name = "Main";
            trig.name = "Trigger";
            mag.name = "Mag1";
            mag2.name = "Mag2";
            foreach (Renderer rr in new[] { body, trig, mag, mag2 }.Select(t => t.GetComponent<Renderer>()))
            {
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rr.receiveShadows = true;
            }
            Debug.Log($"[FpsWave] Fucile sci-fi montato: lunghezza {length:F2} m, scala {scale:F4}.");
            return true;
        }

        /// <summary>
        /// Mira attraverso il cannocchiale (fucile sci-fi): nella metà centrale dell'arma, fascia attorno all'asse della canna,
        /// punto più alto (cima del cannocchiale) meno il raggio del tubo: l'occhio va al centro del tubo.
        /// </summary>
        private static Vector3 ScopeSightPoint(Vector3[] verts, Vector3 muzzle)
        {
            float minZ = verts.Min(v => v.z);
            float length = muzzle.z - minZ;
            var band = verts.Where(v => Mathf.Abs(v.x - muzzle.x) < 0.012f && v.z > minZ + 0.3f * length && v.z < minZ + 0.7f * length).ToArray();
            if (band.Length == 0) return SightPoint(verts, muzzle);
            float topY = band.Max(v => v.y);
            // Tubo: vertici vicini alla cima, raggio = metà dell'altezza della fascia più alta e stretta.
            var top = band.Where(v => v.y > topY - 0.05f).ToArray();
            float bottom = top.Min(v => v.y);
            float radius = Mathf.Clamp((topY - bottom) * 0.5f, 0.008f, 0.025f);
            float z = top.Average(v => v.z);
            return new Vector3(muzzle.x, topY - radius, z);
        }

        /// <summary>Area della sezione delle due estremità (8% della lunghezza) lungo l'asse.</summary>
        private static float EndSection(Vector3[] verts, Vector3 axis, Bounds b, bool positiveEnd)
        {
            float min = Vector3.Dot(b.min, axis), max = Vector3.Dot(b.max, axis), len = max - min;
            float lim = positiveEnd ? max - 0.08f * len : min + 0.08f * len;
            var pts = verts.Where(v => positiveEnd ? Vector3.Dot(v, axis) >= lim : Vector3.Dot(v, axis) <= lim).ToArray();
            if (pts.Length == 0) return float.MaxValue;
            var e = new Bounds(pts[0], Vector3.zero);
            foreach (Vector3 v in pts) e.Encapsulate(v);
            // Prodotto delle due dimensioni perpendicolari all'asse.
            float area = 1f;
            for (int i = 0; i < 3; i++)
                if (Mathf.Abs(axis[i]) < 0.5f) area *= Mathf.Max(1e-4f, e.size[i]);
            return area;
        }

        private static Vector3 Average(Vector3[] v)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 p in v) sum += p;
            return v.Length > 0 ? sum / v.Length : Vector3.zero;
        }

        private static void ConfigureSciRifleModel()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(SciRifleFbx);
            if (importer == null)
                return;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var pair in CreateSciRifleMaterials())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
        }

        /// <summary>Palette del fucile (materiali a tinta unita nel .blend originale): bianco perla, grafite, rosso, luci ambra.</summary>
        private static Dictionary<string, Material> CreateSciRifleMaterials()
        {
            Material Flat(string name, Color color, float metallic, float smoothness, Color emission = default)
            {
                Material mat = LoadOrCreate($"SciRifle_{name}");
                mat.SetTexture("_BaseMap", null);
                mat.SetTexture("_BumpMap", null);
                mat.DisableKeyword("_NORMALMAP");
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_WorkflowMode", 1f);
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);
                mat.DisableKeyword("_SPECULAR_SETUP");
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                if (emission.maxColorComponent > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.black);
                }
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                return mat;
            }
            return new Dictionary<string, Material>
            {
                ["SR_White"] = Flat("White", new Color(0.86f, 0.87f, 0.88f), 0.15f, 0.62f),
                ["SR_Grey"] = Flat("Grey", new Color(0.42f, 0.43f, 0.45f), 0.7f, 0.55f),
                ["SR_Gunmetal"] = Flat("Gunmetal", new Color(0.16f, 0.17f, 0.18f), 0.85f, 0.5f),
                ["SR_Dark"] = Flat("Dark", new Color(0.035f, 0.035f, 0.04f), 0.35f, 0.42f),
                ["SR_Red"] = Flat("Red", new Color(0.62f, 0.06f, 0.04f), 0.3f, 0.55f),
                ["SR_Brass"] = Flat("Brass", new Color(0.8f, 0.5f, 0.12f), 1f, 0.8f),
                ["SR_Glow"] = Flat("Glow", new Color(1f, 0.55f, 0.08f), 0f, 0.8f, new Color(1f, 0.5f, 0.05f) * 4f),
                ["SR_Lens"] = HoloLensMaterial(),
            };
        }

        /// <summary>
        /// Vetro del mirino olografico: Unlit trasparente additivo con un reticolo luminoso generato qui (anello, punto
        /// centrale e quattro tacche) su un velo azzurrino appena visibile. Additivo: non scurisce mai la scena.
        /// </summary>
        private static Material HoloLensMaterial()
        {
            string texPath = $"{MaterialsDir}/SciRifle_Reticle.png";
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            var px = new Color32[size * size];
            Vector2 c = new(size * 0.5f, size * 0.5f);
            float ring = size * 0.30f;          // raggio dell'anello
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - c;
                    float r = d.magnitude;
                    float a = 0f;
                    a = Mathf.Max(a, 1f - Mathf.Clamp01((Mathf.Abs(r - ring) - 1.5f) / 1.5f));        // anello
                    a = Mathf.Max(a, 1f - Mathf.Clamp01((r - 4.5f) / 1.5f));                          // punto
                    bool onAxis = Mathf.Abs(d.x) < 2.2f || Mathf.Abs(d.y) < 2.2f;
                    if (onAxis && r > ring + 6f && r < ring + 22f) a = Mathf.Max(a, 1f);             // tacche esterne
                    float edge = Mathf.Clamp01(1f - Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) / (size * 0.5f));
                    var glass = new Color(0.03f, 0.07f, 0.09f) * Mathf.SmoothStep(0f, 1f, edge * 6f); // velo, più tenue ai bordi
                    var glow = new Color(1f, 0.16f, 0.08f) * a;                                       // rosso-arancio
                    px[y * size + x] = (Color32)new Color(glass.r + glow.r, glass.g + glow.g, glass.b + glow.b, 1f);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(texPath) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.SaveAndReimport();
            }

            const string unlit = "Universal Render Pipeline/Unlit";
            string path = $"{MaterialsDir}/SciRifle_Lens.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(unlit)) { name = "SciRifle_Lens" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find(unlit);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", new Color(2.4f, 0.55f, 0.55f)); // HDR rosso: il reticolo resta rosso e fa un filo di bloom
            mat.SetFloat("_Surface", 1f);     // trasparente
            mat.SetFloat("_Blend", 2f);       // additivo
            mat.SetFloat("_Cull", 0f);        // visibile da entrambi i lati
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- Pistola sci-fi (Magma, AF Creations)

        private static void BuildSciPistolPrefab()
        {
            ConfigureTextures(SciPistolDir, maxSize: 2048);
            Dictionary<string, Material> mats = CreateSciPistolMaterials();
            var importer = (ModelImporter)AssetImporter.GetAtPath(SciPistolFbx);
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var pair in mats)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SciPistolFbx);
            var root = new GameObject("VM_SciPistol");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(model);
            rig.transform.SetParent(root.transform, false);
            Renderer[] renderers = rig.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Material[] slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string n = slots[i] != null ? slots[i].name : "";
                    slots[i] = n.Contains("White") ? mats["White"] : mats["Black.001"];
                }
                r.sharedMaterials = slots;
            }

            // Scala sulla lunghezza voluta, poi orientamento: canna verso +Z, impugnatura in basso.
            Bounds b = WorldBounds(rig.transform);
            float length = Mathf.Max(b.size.x, b.size.z);
            rig.transform.localScale *= SciPistolLength / Mathf.Max(0.0001f, length);
            Vector3[] verts = renderers.SelectMany(r => WorldVertices(r.transform)).ToArray();
            b = WorldBounds(rig.transform);
            if (b.size.y > Mathf.Max(b.size.x, b.size.z))
            {
                // Asse lungo verticale (assi dell'FBX non convertiti): si porta orizzontale.
                rig.transform.rotation = Quaternion.AngleAxis(90f, Vector3.right) * rig.transform.rotation;
                verts = renderers.SelectMany(r => WorldVertices(r.transform)).ToArray();
                b = WorldBounds(rig.transform);
            }
            Vector3 axis = b.size.x > b.size.z ? Vector3.right : Vector3.forward;
            // L'impugnatura è la parte bassa: la volata è l'estremità opposta lungo l'asse lungo.
            float lowY = b.min.y + b.size.y * 0.25f;
            Vector3 grip = Average(verts.Where(v => v.y < lowY).ToArray());
            if (Vector3.Dot(grip - b.center, axis) > 0f) axis = -axis;
            rig.transform.rotation = Quaternion.FromToRotation(axis, Vector3.forward) * rig.transform.rotation;
            verts = renderers.SelectMany(r => WorldVertices(r.transform)).ToArray();
            b = WorldBounds(rig.transform);

            // Origine sulla linea di mira: centro della parte alta, nel quarto posteriore (tacca).
            float rearZ = b.min.z + b.size.z * 0.2f;
            var rear = verts.Where(v => v.z < rearZ + b.size.z * 0.1f && v.z > rearZ - b.size.z * 0.1f).ToArray();
            float topY = rear.Length > 0 ? rear.Max(v => v.y) : b.max.y;
            Vector3 sight = new(b.center.x, topY - 0.002f, rearZ);
            rig.transform.position -= sight;
            verts = renderers.SelectMany(r => WorldVertices(r.transform)).ToArray();
            b = WorldBounds(rig.transform);

            // Volata: canna superiore (la pistola ha due canne), punta più avanzata nella metà alta.
            float midY = b.center.y;
            float maxZ = verts.Max(v => v.z);
            var tip = verts.Where(v => v.z > maxZ - 0.012f && v.y > midY - b.size.y * 0.1f).ToArray();
            if (tip.Length == 0) tip = verts.Where(v => v.z > maxZ - 0.012f).ToArray();
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(rig.transform, true);
            muzzle.position = new Vector3(tip.Average(v => v.x), tip.Average(v => v.y), maxZ + 0.01f);
            muzzle.rotation = Quaternion.identity;

            var vm = root.AddComponent<WeaponViewModel>();
            vm.EditorSetupProcedural(rig.transform, null, muzzle, 0f);
            Vector3 muzzlePos = muzzle.position;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ViewModelsDir}/VM_SciPistol.prefab");
            Object.DestroyImmediate(root);

            var pistol = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(PistolAsset);
            if (pistol != null)
            {
                pistol.EditorSetViewModel(prefab, new Vector3(0.13f, -0.1f, 0.3f), new Vector3(0f, 0f, 0.26f));
                EditorUtility.SetDirty(pistol);
            }
            Debug.Log($"[FpsWave] VM_SciPistol: lunghezza {b.size.z:F3} m, volata {muzzlePos:F3}.");
        }

        /// <summary>
        /// Materiali PBR della Magma: la mappa ORM (R occlusione, G ruvidità, B metallo) diventa MetallicSmoothness per URP
        /// (R metallo, A levigatezza) più una mappa AO; l'emissiva va moltiplicata (consiglio dell'autore).
        /// </summary>
        private static Dictionary<string, Material> CreateSciPistolMaterials()
        {
            var result = new Dictionary<string, Material>();
            foreach (string part in new[] { "White", "Black.001" })
            {
                string prefix = $"{SciPistolDir}/SciFiPistol_{part}";
                string metalSmooth = $"{prefix}_MetalSmooth.png", ao = $"{prefix}_AO.png";
                if (!System.IO.File.Exists(metalSmooth) || !System.IO.File.Exists(ao))
                {
                    SplitOrm($"{prefix}_OcclusionRoughnessMetallic.png", metalSmooth, ao);
                    AssetDatabase.ImportAsset(metalSmooth);
                    AssetDatabase.ImportAsset(ao);
                    ConfigureTextures(SciPistolDir, maxSize: 2048);
                }
                Material mat = LoadOrCreate($"SciPistol_{part.Replace(".001", "")}");
                SetCommon(mat, $"{prefix}_BaseColor.png", $"{prefix}_Normal.png");
                mat.SetFloat("_WorkflowMode", 1f);
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metalSmooth));
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", 1f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.DisableKeyword("_SPECULAR_SETUP");
                mat.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ao));
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.EnableKeyword("_OCCLUSIONMAP");
                var emissive = AssetDatabase.LoadAssetAtPath<Texture2D>($"{prefix}_Emissive.png");
                mat.SetTexture("_EmissionMap", emissive);
                mat.SetColor("_EmissionColor", emissive != null ? Color.white * 6f : Color.black);
                if (emissive != null) mat.EnableKeyword("_EMISSION"); else mat.DisableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(mat);
                result[part] = mat;
            }
            result["Material"] = result["Black.001"];
            return result;
        }

        private static void SplitOrm(string ormPath, string metalSmoothOut, string aoOut)
        {
            var orm = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            orm.LoadImage(System.IO.File.ReadAllBytes(ormPath));
            Color32[] src = orm.GetPixels32();
            var ms = new Color32[src.Length];
            var ao = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                ms[i] = new Color32(src[i].b, 0, 0, (byte)(255 - src[i].g));
                ao[i] = new Color32(src[i].r, src[i].r, src[i].r, 255);
            }
            var t = new Texture2D(orm.width, orm.height, TextureFormat.RGBA32, false, true);
            t.SetPixels32(ms); t.Apply(false);
            System.IO.File.WriteAllBytes(metalSmoothOut, t.EncodeToPNG());
            t.SetPixels32(ao); t.Apply(false);
            System.IO.File.WriteAllBytes(aoOut, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            Object.DestroyImmediate(orm);
        }

        private static float ClipLength(Object[] assets, string name)
        {
            AnimationClip clip = FindClip(assets, name);
            return clip != null ? clip.length : 1f;
        }

        // ---------------------------------------------------------------- Geometria

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name && (name == "FPS_Camera" || t.TryGetComponent(out MeshFilter _) || t.TryGetComponent(out SkinnedMeshRenderer _)))
                    return t;
            return null;
        }

        private static Vector3[] WorldVertices(Transform t)
        {
            if (t == null) return System.Array.Empty<Vector3>();
            Mesh mesh = t.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            if (mesh == null)
            {
                if (!t.TryGetComponent(out SkinnedMeshRenderer skinned)) return System.Array.Empty<Vector3>();
                mesh = new Mesh();
                skinned.BakeMesh(mesh, true);
                Vector3[] baked = mesh.vertices;
                for (int i = 0; i < baked.Length; i++) baked[i] = t.TransformPoint(baked[i]);
                Object.DestroyImmediate(mesh);
                return baked;
            }
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = t.TransformPoint(v[i]);
            return v;
        }

        /// <summary>Asse orizzontale più lungo della mesh dell'arma, orientato verso l'estremità più lontana dall'occhio.</summary>
        private static Vector3 BarrelDirection(Vector3[] verts, Vector3 eye)
        {
            var b = new Bounds(verts[0], Vector3.zero);
            foreach (Vector3 v in verts) b.Encapsulate(v);
            Vector3 axis = b.size.x > b.size.z ? Vector3.right : Vector3.forward;
            float max = float.MinValue, min = float.MaxValue;
            foreach (Vector3 v in verts)
            {
                float d = Vector3.Dot(v, axis);
                if (d > max) max = d;
                if (d < min) min = d;
            }
            float eyeD = Vector3.Dot(eye, axis);
            return Mathf.Abs(max - eyeD) >= Mathf.Abs(min - eyeD) ? axis : -axis;
        }

        private static Vector3 MuzzlePoint(Vector3[] verts)
        {
            float maxZ = verts.Max(v => v.z);
            var tip = verts.Where(v => v.z > maxZ - 0.01f).ToArray();
            return new Vector3(tip.Average(v => v.x), tip.Average(v => v.y), maxZ);
        }

        /// <summary>Linea di mira: punto più alto dell'arma vicino all'asse della canna, escluso il primo tratto di volata.</summary>
        private static Vector3 SightPoint(Vector3[] verts, Vector3 muzzle)
        {
            float minZ = verts.Min(v => v.z);
            float length = muzzle.z - minZ;
            var near = verts.Where(v => Mathf.Abs(v.x - muzzle.x) < 0.006f && v.z < muzzle.z - 0.05f * length).ToArray();
            if (near.Length == 0) return muzzle;
            float topY = near.Max(v => v.y);
            return new Vector3(muzzle.x, topY - 0.004f, muzzle.z);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
