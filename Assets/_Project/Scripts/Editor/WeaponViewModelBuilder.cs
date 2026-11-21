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

        [MenuItem("FpsWave/Setup/4. Build Weapon View Models")]
        public static void BuildAll()
        {
            EnsureFolder(MaterialsDir);
            EnsureFolder(AnimationsDir);
            EnsureFolder(ViewModelsDir);

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

            if (ImportPistolFiles())
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
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
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

            var root = new GameObject("VM_M4");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(model);
            rig.transform.SetParent(root.transform, false);

            // Posa di riferimento: primo frame di Idle.
            AnimationClip idle = FindClip(assets, "Idle");
            if (idle != null)
                idle.SampleAnimation(rig, 0f);

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
            Vector3 sightPoint = SightPoint(weaponVerts, muzzlePoint);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(weaponMesh, true);
            muzzle.position = muzzlePoint + Vector3.forward * 0.01f;
            muzzle.rotation = Quaternion.identity;

            if (!rig.TryGetComponent(out Animator animator))
                animator = rig.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
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

            string prefabPath = $"{ViewModelsDir}/VM_M4.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            // In ADS il mirino va sull'asse della camera; un piccolo arretramento evita di avere l'arma "in faccia".
            Vector3 ads = new(-sightPoint.x, -sightPoint.y, -0.02f);
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
