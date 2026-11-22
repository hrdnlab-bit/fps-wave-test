using System.Collections.Generic;
using System.Linq;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Player;
using HrdnLab.FpsWave.Player.Character;
using HrdnLab.FpsWave.Weapons;
using UnityEditor;
using UnityEngine;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Personaggio del player (modello AIRO, asset nostro in <c>Assets/ThirdParty/_Own/AIRO</c>): import del modello e
    /// delle texture, materiale URP, prefab del personaggio con <see cref="CharacterRig"/>, modelli delle armi in terza
    /// persona con i punti di presa delle mani e collegamento al prefab Player. Ripetibile e deterministico.
    /// Prerequisiti: <c>Setup/3</c> (Player, armi) e <c>Setup/4</c> (view model delle armi).
    /// </summary>
    public static class PlayerCharacterBuilder
    {
        // Modello del player: il nuovo "Pilot" (Tripo + rig automatico, Matteo) se i binari ci sono, altrimenti AIRO.
        // Stesse ossa (nomi stile Mixamo) e stesse texture (<prefisso>BaseColor/Normal/MetallicSmoothness/AO/Emission).
        private const string PilotDir = "Assets/ThirdParty/_Own/Pilot";
        private const string LegacyAiroDir = "Assets/ThirdParty/_Own/AIRO";
        private static bool UsePilot => System.IO.File.Exists(PilotDir + "/Pilot.fbx");
        private static string AiroDir => UsePilot ? PilotDir : LegacyAiroDir;
        private static string AiroFbx => UsePilot ? PilotDir + "/Pilot.fbx" : LegacyAiroDir + "/AIRO_Pilot.fbx";
        private static string AiroTextures => AiroDir + "/Textures";
        private static string TexturePrefix => UsePilot ? "Pilot_" : "AIRO_";
        private const string ArtDir = "Assets/_Project/Art/Characters/AIRO";
        private const string MeshesDir = "Assets/_Project/Art/Meshes/Weapons";
        private const string CharactersPrefabDir = "Assets/_Project/Prefabs/Characters";
        private const string WeaponsPrefabDir = "Assets/_Project/Prefabs/Weapons";
        private const string SettingsAsset = "Assets/_Project/Data/Characters/AIRO_Animation.asset";
        private const string CharacterPrefab = CharactersPrefabDir + "/AIRO_Character.prefab";
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";
        private const string RifleAsset = "Assets/_Project/Data/Weapons/AssaultRifle.asset";
        private const string PistolAsset = "Assets/_Project/Data/Weapons/Pistol.asset";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const float HeadBoneHeight = 1.58f;

        // Parti del fucile da tenere nel modello in terza persona (il resto sono braccia e caricatore di scorta).
        private static readonly string[] RifleParts = { "Main", "Bolt", "BoltCatch", "Mag1", "Safety", "Trigger" };

        [MenuItem("FpsWave/Setup/9. Build Player Character (AIRO)")]
        public static void BuildAll()
        {
            if (!System.IO.File.Exists(AiroFbx))
            {
                Debug.LogError($"[FpsWave] {AiroFbx} non trovato: copia AIRO_Pilot.fbx e la cartella Textures (vedi docs/ASSETS.md).");
                return;
            }
            EnsureFolder(ArtDir);
            EnsureFolder(MeshesDir);
            EnsureFolder(CharactersPrefabDir);
            EnsureFolder(WeaponsPrefabDir);
            EnsureFolder("Assets/_Project/Data/Characters");

            ConfigureTextures();
            ConfigureModel();
            Material material = CreateMaterial();
            CharacterAnimationSettings settings = LoadOrCreateSettings();
            GameObject character = BuildCharacterPrefab(material, settings);
            BuildWorldWeapon(RifleAsset, "WM_AssaultRifle", CharacterGrip.Rifle);
            BuildWorldWeapon(PistolAsset, "WM_Pistol", CharacterGrip.Pistol);
            AttachToPlayer(character);
            AssetDatabase.SaveAssets();
            Debug.Log("[FpsWave] Personaggio AIRO costruito e collegato al Player (corpo, IK, armi in terza persona).");
        }

        /// <summary>Usato da <c>Setup/3</c>: rimette il personaggio sul Player rigenerato, se già costruito.</summary>
        public static void TryAttachToPlayer()
        {
            var character = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab);
            if (character == null)
                return;
            BuildWorldWeapon(RifleAsset, "WM_AssaultRifle", CharacterGrip.Rifle);
            BuildWorldWeapon(PistolAsset, "WM_Pistol", CharacterGrip.Pistol);
            AttachToPlayer(character);
        }

        // ---------------------------------------------------------------- Import

        private static void ConfigureTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { AiroTextures }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                bool normal = file.EndsWith("_Normal");
                bool linear = file.EndsWith("_MetallicSmoothness") || file.EndsWith("_AO");
                bool big = file.EndsWith("_BaseColor") || normal;
                int size = big ? 4096 : 2048;
                TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                bool srgb = !normal && !linear;
                if (importer.textureType == type && importer.sRGBTexture == srgb && importer.maxTextureSize == size
                    && importer.streamingMipmaps)
                    continue;
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = size;
                importer.streamingMipmaps = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureModel()
        {
            if (AssetImporter.GetAtPath(AiroFbx) is not ModelImporter importer)
                return;
            // Generic senza Avatar: serve per avere uno SkinnedMeshRenderer (con "None" la mesh diventa statica).
            bool changed = importer.animationType != ModelImporterAnimationType.Generic
                           || importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar
                           || importer.importAnimation
                           || importer.materialImportMode != ModelImporterMaterialImportMode.None
                           || !importer.bakeAxisConversion
                           || importer.importBlendShapes
                           || importer.importCameras || importer.importLights;
            if (!changed)
                return;
            importer.animationType = ModelImporterAnimationType.Generic; // animazione procedurale: nessuna clip
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.bakeAxisConversion = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static Material CreateMaterial()
        {
            string path = $"{ArtDir}/M_AIRO_Pilot.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(LitShader)) { name = "M_AIRO_Pilot" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find(LitShader);
            Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{AiroTextures}/{TexturePrefix}{n}.png");

            mat.SetFloat("_WorkflowMode", 1f);
            mat.SetTexture("_BaseMap", Tex("BaseColor"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_MetallicGlossMap", Tex("MetallicSmoothness"));
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f); // alfa della mappa metallica
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetTexture("_BumpMap", Tex("Normal"));
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_OcclusionMap", Tex("AO"));
            mat.SetFloat("_OcclusionStrength", 1f);
            mat.EnableKeyword("_OCCLUSIONMAP");
            mat.SetTexture("_EmissionMap", Tex("Emission"));
            mat.SetColor("_EmissionColor", Color.white * 1.6f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static CharacterAnimationSettings LoadOrCreateSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<CharacterAnimationSettings>(SettingsAsset);
            if (s != null)
                return s; // non si sovrascrivono le regolazioni fatte a mano
            s = ScriptableObject.CreateInstance<CharacterAnimationSettings>();
            AssetDatabase.CreateAsset(s, SettingsAsset);
            return s;
        }

        // ---------------------------------------------------------------- Personaggio

        private static GameObject BuildCharacterPrefab(Material material, CharacterAnimationSettings settings)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AiroFbx);
            var root = new GameObject("AIRO_Character");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root.transform, false);

            Transform T(string n) => FindByName(instance.transform, n);
            Transform head = T("Head");

            // Orientamento: davanti = +Z (punte dei piedi), sinistra = -X.
            Transform leftFoot = T("LeftFoot"), leftToe = T("LeftToeBase");
            if (leftFoot != null && leftToe != null && leftToe.position.z < leftFoot.position.z)
                instance.transform.localRotation = Quaternion.AngleAxis(180f, Vector3.up) * instance.transform.localRotation;
            // Scala: l'osso della testa all'altezza prevista (1,8 m di statura con il casco).
            if (head != null && head.position.y > 0.01f)
            {
                float k = HeadBoneHeight / head.position.y;
                if (Mathf.Abs(k - 1f) > 0.05f)
                    instance.transform.localScale *= k;
            }
            // Piedi a terra: dai vertici veri (i bounds dello SkinnedMeshRenderer sono larghi e alzerebbero il corpo).
            float minY = instance.GetComponentsInChildren<Renderer>(true).Min(LowestVertexY);
            instance.transform.position -= Vector3.up * minY;

            // L'Animator aggiunto dall'import Generic non serve: le ossa sono mosse da CharacterRig.
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (!renderers.Any(r => r is SkinnedMeshRenderer))
                Debug.LogError("[FpsWave] AIRO_Pilot.fbx importato senza SkinnedMeshRenderer: controlla le impostazioni Rig del modello.");
            foreach (Renderer r in renderers)
            {
                Material[] slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer skinned)
                {
                    skinned.quality = SkinQuality.Bone4;
                    skinned.updateWhenOffscreen = false;
                    skinned.skinnedMotionVectors = false;
                }
            }

            CharacterRig.Limb Limb(string upper, string lower, string end) =>
                new() { Upper = T(upper), Lower = T(lower), End = T(end) };
            Transform[] Fingers(string side) =>
                new[] { "Index", "Middle", "Ring", "Pinky", "Thumb" }.Select(f => T($"{side}Hand{f}1")).Where(t => t != null).ToArray();

            SkinnedMeshRenderer body = renderers.OfType<SkinnedMeshRenderer>().FirstOrDefault();
            Mesh firstPerson = body != null ? BuildFirstPersonMesh(body) : null;

            var rig = root.AddComponent<CharacterRig>();
            rig.EditorSetup(settings, T("Hips"), T("Spine"), T("Spine1"), T("Spine2"), T("Neck"), head,
                T("LeftShoulder"), T("RightShoulder"),
                Limb("LeftUpLeg", "LeftLeg", "LeftFoot"), Limb("RightUpLeg", "RightLeg", "RightFoot"),
                Limb("LeftArm", "LeftForeArm", "LeftHand"), Limb("RightArm", "RightForeArm", "RightHand"),
                Fingers("Left"), Fingers("Right"), renderers, body, firstPerson);

            string missing = string.Join(", ", new[] { "Hips", "Spine1", "Head", "LeftUpLeg", "RightUpLeg", "LeftHand", "RightHand" }
                .Where(n => T(n) == null));
            if (missing.Length > 0)
                Debug.LogError($"[FpsWave] AIRO_Pilot.fbx: ossa mancanti: {missing}");

            SetLayerRecursively(root, GameLayers.Player);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CharacterPrefab);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Ossa nascoste in prima persona: i triangoli che ne dipendono in modo significativo vengono tolti.
        private static readonly string[] FirstPersonHidden = { "Head", "Neck", "Shoulder", "Arm", "Hand" };
        private const float FirstPersonCutHeight = 1.22f;

        /// <summary>Copia della mesh senza testa, collo e braccia (stessi vertici e pesi, meno triangoli).</summary>
        private static Mesh BuildFirstPersonMesh(SkinnedMeshRenderer body)
        {
            Mesh source = body.sharedMesh;
            string path = $"{ArtDir}/AIRO_Pilot_FirstPerson.asset";
            // Si aggiorna l'asset sul posto (stessi vertici, triangoli diversi): così il riferimento nel prefab resta valido.
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null || mesh.vertexCount != source.vertexCount || AssetDatabase.AssetPathToGUID(path) != StableGuid(path))
            {
                var copy = Object.Instantiate(source);
                copy.name = source.name + "_FirstPerson";
                mesh = CreateMeshAssetWithStableGuid(copy, path);
            }
            Transform[] bones = body.bones;
            var hidden = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                hidden[i] = bones[i] != null && FirstPersonHidden.Any(h => bones[i].name.Contains(h));

            var bonesPerVertex = source.GetBonesPerVertex();
            var weights = source.GetAllBoneWeights();
            var hiddenWeight = new float[source.vertexCount];
            int w = 0;
            for (int v = 0; v < source.vertexCount; v++)
            {
                int count = bonesPerVertex[v];
                for (int k = 0; k < count; k++, w++)
                    if (hidden[weights[w].boneIndex]) hiddenWeight[v] += weights[w].weight;
            }

            // In prima persona si vede il corpo dalla vita in giù (fascia, cintura, gambe, stivali): sopra ci sono
            // piastra, cinghie e tubi a pochi centimetri dalla camera, che coprirebbero la visuale.
            Vector3[] vertices = source.vertices;
            Matrix4x4 toWorld = body.transform.localToWorldMatrix;
            var cut = new bool[source.vertexCount];
            for (int v = 0; v < cut.Length; v++)
            {
                Vector3 p = toWorld.MultiplyPoint3x4(vertices[v]);
                cut[v] = hiddenWeight[v] > 0.05f || p.y > FirstPersonCutHeight || Mathf.Abs(p.x) > 0.26f;
            }

            int removed = 0;
            mesh.subMeshCount = source.subMeshCount;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] tris = source.GetTriangles(sub);
                var kept = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    if (cut[tris[t]] || cut[tris[t + 1]] || cut[tris[t + 2]])
                    {
                        removed++;
                        continue;
                    }
                    kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]);
                }
                mesh.SetTriangles(kept, sub, false);
            }
            EditorUtility.SetDirty(mesh);
            Debug.Log($"[FpsWave] Mesh in prima persona: {removed} triangoli tolti (testa, collo, braccia).");
            return mesh;
        }

        // ---------------------------------------------------------------- Armi in terza persona

        private static void BuildWorldWeapon(string definitionPath, string name, CharacterGrip grip)
        {
            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(definitionPath);
            if (definition == null || definition.ViewModelPrefab == null)
            {
                Debug.LogWarning($"[FpsWave] {definitionPath}: arma o view model mancante, esegui Setup/3 e Setup/4.");
                return;
            }

            // Il view model ha l'arma già orientata (canna +Z, alto +Y) nello spazio della propria radice.
            var vm = (GameObject)PrefabUtility.InstantiatePrefab(definition.ViewModelPrefab);
            vm.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = vm.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                AnimationClip idle = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.Contains("Idle"));
                if (idle != null) idle.SampleAnimation(animator.gameObject, 0f);
            }
            vm.TryGetComponent(out WeaponViewModel viewModel);
            Vector3 muzzle = viewModel != null && viewModel.Muzzle != null ? viewModel.Muzzle.position : Vector3.forward * 0.5f;

            List<Renderer> parts = vm.GetComponentsInChildren<Renderer>(true)
                .Where(r => grip == CharacterGrip.Pistol || RifleParts.Contains(r.name)).ToList();
            if (parts.Count == 0)
            {
                Object.DestroyImmediate(vm);
                Debug.LogError($"[FpsWave] {name}: nessuna parte dell'arma trovata nel view model.");
                return;
            }

            // Impugnatura (origine del modello): dietro e sotto il grilletto per il fucile, sopra il caricatore per la pistola.
            Vector3 contact;
            Renderer trigger = parts.FirstOrDefault(r => r.name == "Trigger");
            Renderer magazine = vm.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith("Magazine"));
            if (grip == CharacterGrip.Rifle && trigger != null)
                contact = trigger.bounds.center + new Vector3(0f, -0.045f, -0.035f);
            else if (magazine != null)
                contact = new Vector3(magazine.bounds.center.x, magazine.bounds.center.y + magazine.bounds.extents.y * 0.5f, magazine.bounds.center.z);
            else
                contact = parts.Aggregate(parts[0].bounds, (b, r) => { b.Encapsulate(r.bounds); return b; }).center;

            Mesh mesh = CombineParts(parts, contact, out Material[] materials);
            mesh.name = name;
            string meshPath = $"{MeshesDir}/{name}.asset";
            string prefabPath = $"{WeaponsPrefabDir}/{name}.prefab";
            // Mesh ricreata da zero (asset derivato, fuori dal repo: vedi docs/ASSETS.md) con GUID fisso, così i prefab
            // nel repo la ritrovano su ogni PC; il prefab viene sovrascritto e mantiene il proprio GUID.
            mesh = CreateMeshAssetWithStableGuid(mesh, meshPath);
            Object.DestroyImmediate(vm);

            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            Vector3 muzzleLocal = muzzle - contact;
            Bounds b = mesh.bounds;
            float length = b.max.z - b.min.z;

            Transform Child(string n, Vector3 pos, Vector3 fingers, Vector3 palm)
            {
                var t = new GameObject(n).transform;
                t.SetParent(root.transform, false);
                t.localPosition = pos;
                t.localRotation = Quaternion.LookRotation(fingers.normalized, palm.normalized);
                return t;
            }

            Vector3 fwd = Vector3.forward, up = Vector3.up, right = Vector3.right;
            Transform rightHand, leftHand;
            if (grip == CharacterGrip.Rifle)
            {
                Vector3 f = fwd * 0.91f - up * 0.42f;           // dita in avanti e in basso lungo l'impugnatura
                rightHand = Child("RightHand", -f.normalized * 0.08f + right * 0.022f, f, -right);
                Vector3 lf = fwd * 0.45f + right * 0.89f;        // mano sotto il paramano, dita verso destra
                Vector3 handguard = new(0f, muzzleLocal.y - 0.035f, muzzleLocal.z - 0.38f * length);
                leftHand = Child("LeftHand", handguard - lf.normalized * 0.075f - up * 0.03f, lf, up);
            }
            else
            {
                Vector3 f = fwd * 0.9f - up * 0.42f;
                rightHand = Child("RightHand", -f.normalized * 0.075f + right * 0.02f, f, -right);
                Vector3 lf = fwd * 0.75f - up * 0.45f + right * 0.45f; // la sinistra avvolge la destra
                leftHand = Child("LeftHand", new Vector3(-0.045f, -0.025f, -0.035f), lf, right);
            }
            var muzzleT = new GameObject("Muzzle").transform;
            muzzleT.SetParent(root.transform, false);
            muzzleT.localPosition = muzzleLocal;

            var model = root.AddComponent<WorldWeaponModel>();
            model.EditorSetup(grip, rightHand, leftHand, muzzleT);
            SetLayerRecursively(root, GameLayers.Player);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            if (prefab == null || prefab.GetComponent<MeshFilter>().sharedMesh == null)
                Debug.LogError($"[FpsWave] {name}: il prefab dell'arma non ha la mesh, ricontrolla {meshPath}.");
            else
                Debug.Log($"[FpsWave] {name}: arma in terza persona pronta ({mesh.vertexCount} vertici, {mesh.subMeshCount} materiali).");

            definition.EditorSetWorldModel(prefab);
            EditorUtility.SetDirty(definition);
        }

        /// <summary>Unisce le parti in una mesh statica (un submesh per materiale) con origine nel punto di impugnatura.</summary>
        private static Mesh CombineParts(List<Renderer> parts, Vector3 origin, out Material[] materials)
        {
            Matrix4x4 toLocal = Matrix4x4.Translate(-origin);
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var temp = new List<Mesh>();
            foreach (Renderer r in parts)
            {
                Mesh source;
                Matrix4x4 matrix;
                if (r is SkinnedMeshRenderer skinned)
                {
                    source = new Mesh();
                    skinned.BakeMesh(source, true);
                    temp.Add(source);
                    matrix = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                }
                else if (r.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                {
                    source = filter.sharedMesh;
                    matrix = r.localToWorldMatrix;
                }
                else continue;

                Material[] mats = r.sharedMaterials;
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    Material m = mats.Length > 0 ? mats[Mathf.Min(sub, mats.Length - 1)] : null;
                    if (m == null) continue;
                    if (!byMaterial.TryGetValue(m, out List<CombineInstance> list))
                        byMaterial[m] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = source, subMeshIndex = sub, transform = toLocal * matrix });
                }
            }

            var groups = new List<CombineInstance>();
            materials = byMaterial.Keys.ToArray();
            foreach (Material m in materials)
            {
                var merged = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                merged.CombineMeshes(byMaterial[m].ToArray(), true, true);
                temp.Add(merged);
                groups.Add(new CombineInstance { mesh = merged, transform = Matrix4x4.identity });
            }
            var result = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            result.CombineMeshes(groups.ToArray(), false, true);
            result.RecalculateBounds();
            result.UploadMeshData(false);
            foreach (Mesh t in temp) Object.DestroyImmediate(t);
            return result;
        }

        // ---------------------------------------------------------------- Player

        private static void AttachToPlayer(GameObject characterPrefab)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                Transform old = contents.transform.Find("Body");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                old = contents.transform.Find("Character");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var character = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab, contents.transform);
                character.name = "Character";
                character.transform.localPosition = Vector3.zero;
                character.transform.localRotation = Quaternion.identity;
                // Riferimento esplicito alla mesh in prima persona: evita override vuote rimaste da esecuzioni precedenti.
                var characterRig = character.GetComponent<CharacterRig>();
                var fpMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{ArtDir}/AIRO_Pilot_FirstPerson.asset");
                if (characterRig != null)
                {
                    characterRig.EditorSetFirstPersonMesh(fpMesh);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(characterRig);
                }
                if (fpMesh == null)
                    Debug.LogError("[FpsWave] Mesh in prima persona mancante: il proprietario vedrebbe l'interno del casco.");

                Transform anchor = contents.transform.Find("WeaponAnchor3P");
                if (anchor == null)
                {
                    anchor = new GameObject("WeaponAnchor3P").transform;
                    anchor.SetParent(contents.transform, false);
                }

                var motor = contents.GetComponent<PlayerMotor>();
                var look = contents.GetComponent<PlayerLook>();
                var weapons = contents.GetComponent<PlayerWeapons>();
                if (!contents.TryGetComponent(out PlayerCharacter pc))
                    pc = contents.AddComponent<PlayerCharacter>();
                pc.EditorSetup(character.GetComponent<CharacterRig>(), motor, look, weapons, anchor);

                // Il corpo non è più gestito da PlayerView (che lo mostrerebbe solo come ombra).
                if (contents.TryGetComponent(out PlayerView view))
                {
                    var so = new SerializedObject(view);
                    SerializedProperty body = so.FindProperty("_bodyRenderers");
                    if (body != null) body.arraySize = 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                SetLayerRecursively(contents, GameLayers.Player);
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ---------------------------------------------------------------- Utilità

        // ---------------------------------------------------------------- Mesh derivate

        /// <summary>GUID deterministico dal percorso: le mesh derivate (fuori dal repo) hanno lo stesso GUID su ogni PC.</summary>
        private static string StableGuid(string path)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("fpswave-derived:" + path));
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }

        /// <summary>Ricrea l'asset della mesh e ne fissa il GUID (riscrivendo il .meta prima che altri lo usino).</summary>
        private static Mesh CreateMeshAssetWithStableGuid(Mesh mesh, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            string guid = StableGuid(path);
            if (AssetDatabase.AssetPathToGUID(path) != guid)
            {
                string metaPath = path + ".meta";
                string meta = System.IO.File.ReadAllText(metaPath);
                meta = System.Text.RegularExpressions.Regex.Replace(meta, @"guid: [0-9a-f]{32}", "guid: " + guid);
                System.IO.File.WriteAllText(metaPath, meta);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (AssetDatabase.AssetPathToGUID(path) != guid)
                Debug.LogWarning($"[FpsWave] {path}: GUID non fissato ({AssetDatabase.AssetPathToGUID(path)}), i prefab funzionano ma cambieranno su altri PC.");
            return saved;
        }

        private static float LowestVertexY(Renderer renderer)
        {
            Mesh mesh = null;
            bool baked = false;
            if (renderer is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                mesh = new Mesh();
                smr.BakeMesh(mesh, false);
                baked = true;
            }
            else if (renderer.TryGetComponent(out MeshFilter mf))
                mesh = mf.sharedMesh;
            if (mesh == null)
                return renderer.bounds.min.y;
            float min = float.MaxValue;
            foreach (Vector3 v in mesh.vertices)
                min = Mathf.Min(min, renderer.transform.TransformPoint(v).y);
            if (baked)
                Object.DestroyImmediate(mesh);
            return min;
        }

        private static Transform FindByName(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
