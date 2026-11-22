using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Smembramento cosmetico (solo client): alla morte stacca l'arto più vicino al punto del colpo letale
    /// (testa sul colpo alla testa). L'arto sulla mesh si "chiude" scalando l'osso a zero (riapplicato dopo
    /// l'Animator, in LateUpdate), al suo posto vola un pezzo fisico ricavato dalla mesh (cache per mesh e osso),
    /// sul taglio resta un moncone e parte uno spruzzo di sangue. Niente oggetti di rete: ogni client lo calcola
    /// dal punto locale che il server manda con la morte (<see cref="EnemyAgent.ClientKilled"/>).
    /// </summary>
    public sealed class EnemyGore : MonoBehaviour
    {
        // Arti staccabili e osso "punta" per la distanza dal colpo (null = lungo l'asse dell'osso).
        private static readonly string[] Limbs = { "Head", "LeftArm", "LeftForeArm", "RightArm", "RightForeArm", "LeftUpLeg", "LeftLeg", "RightUpLeg", "RightLeg" };
        private static readonly string[] LimbEnds = { null, "LeftForeArm", "LeftHand", "RightForeArm", "RightHand", "LeftLeg", "LeftFoot", "RightLeg", "RightFoot" };
        private const float MaxLimbDistance = 0.3f;
        private const float GibLifetime = 7f;
        private const float BleedSeconds = 1.4f;

        private static readonly Dictionary<(Mesh, int), Mesh> s_gibCache = new();
        private static Material s_stumpMaterial;
        private static ParticleSystem s_blood;

        [SerializeField] private SkinnedMeshRenderer _skin;

        private Transform[] _limbs;
        private Transform[] _ends;
        private int[] _boneIndex;
        private readonly List<Transform> _cut = new();
        private readonly List<GameObject> _stumps = new();
        private float _bleedUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_gibCache.Clear();
            s_stumpMaterial = null;
            s_blood = null;
        }

        private void Awake() => Resolve();

        private void Resolve()
        {
            if (_limbs != null || _skin == null)
                return;
            _limbs = new Transform[Limbs.Length];
            _ends = new Transform[Limbs.Length];
            _boneIndex = new int[Limbs.Length];
            Transform[] bones = _skin.bones;
            for (int i = 0; i < Limbs.Length; i++)
            {
                _boneIndex[i] = -1;
                for (int b = 0; b < bones.Length; b++)
                {
                    if (bones[b] == null)
                        continue;
                    if (bones[b].name == Limbs[i]) { _limbs[i] = bones[b]; _boneIndex[i] = b; }
                    if (LimbEnds[i] != null && bones[b].name == LimbEnds[i]) _ends[i] = bones[b];
                }
            }
        }

        /// <summary>Riporta il corpo intero (riuso dal pool).</summary>
        public void ResetGore()
        {
            foreach (Transform bone in _cut)
            {
                if (bone != null)
                    bone.localScale = Vector3.one;
            }
            _cut.Clear();
            foreach (GameObject stump in _stumps)
            {
                if (stump != null)
                    stump.SetActive(false);
            }
            _bleedUntil = 0f;
        }

        /// <summary>Colpo letale: stacca l'arto più vicino al punto (o la testa), sangue in ogni caso.</summary>
        public void Kill(Vector3 worldPoint, Vector3 shotDirection, bool head)
        {
            Resolve();
            if (_limbs == null)
                return;
            int limb = head ? 0 : NearestLimb(worldPoint);
            Blood(worldPoint, shotDirection, limb >= 0 ? 40 : 18);
            if (limb >= 0)
                Sever(limb, shotDirection);
        }

        private int NearestLimb(Vector3 point)
        {
            int best = -1;
            float bestDist = MaxLimbDistance * Mathf.Max(0.5f, transform.lossyScale.y);
            for (int i = 1; i < _limbs.Length; i++) // la testa solo con il colpo alla testa
            {
                if (_limbs[i] == null || _ends[i] == null || _cut.Contains(_limbs[i]))
                    continue;
                float d = DistanceToSegment(point, _limbs[i].position, _ends[i].position);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        private void Sever(int limb, Vector3 shotDirection)
        {
            Transform bone = _limbs[limb];
            if (bone == null || _cut.Contains(bone))
                return;

            Mesh gib = GibMesh(_skin, _boneIndex[limb]);
            if (gib != null)
            {
                var piece = new GameObject($"Gib_{Limbs[limb]}") { layer = 2 }; // Ignore Raycast: i colpi lo attraversano
                piece.transform.SetPositionAndRotation(bone.position, bone.rotation);
                piece.transform.localScale = bone.lossyScale;
                piece.AddComponent<MeshFilter>().sharedMesh = gib;
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _skin.sharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                piece.AddComponent<BoxCollider>();
                var body = piece.AddComponent<Rigidbody>();
                body.mass = limb == 0 ? 4f : 3f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                Vector3 push = shotDirection.sqrMagnitude > 0.01f ? shotDirection.normalized : -transform.forward;
                body.linearVelocity = push * Random.Range(3.5f, 6f) + Vector3.up * Random.Range(2f, 3.5f) + Random.insideUnitSphere;
                body.angularVelocity = Random.insideUnitSphere * 10f;
                Destroy(piece, GibLifetime);
            }

            // Moncone sul taglio, attaccato all'osso padre (segue la caduta del corpo).
            if (bone.parent != null)
            {
                GameObject stump = TakeStump();
                stump.transform.SetParent(bone.parent, false);
                stump.transform.position = bone.position;
                float radius = (limb == 0 ? 0.075f : limb is 5 or 7 ? 0.075f : 0.05f) * transform.lossyScale.y;
                Vector3 parentScale = bone.parent.lossyScale;
                stump.transform.localScale = new Vector3(radius / Mathf.Max(1e-4f, parentScale.x),
                    radius / Mathf.Max(1e-4f, parentScale.y), radius / Mathf.Max(1e-4f, parentScale.z)) * 2f;
                stump.SetActive(true);
            }

            _cut.Add(bone);
            _bleedUntil = Time.time + BleedSeconds;
            Blood(bone.position, Vector3.up + shotDirection.normalized * 0.5f, 30);
        }

        private GameObject TakeStump()
        {
            foreach (GameObject s in _stumps)
            {
                if (s != null && !s.activeSelf)
                    return s;
            }
            GameObject stump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stump.name = "Stump";
            Destroy(stump.GetComponent<Collider>());
            if (s_stumpMaterial == null)
            {
                s_stumpMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
                s_stumpMaterial.SetColor("_BaseColor", new Color(0.32f, 0.02f, 0.02f));
                s_stumpMaterial.SetFloat("_Smoothness", 0.75f);
            }
            stump.GetComponent<Renderer>().sharedMaterial = s_stumpMaterial;
            stump.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            _stumps.Add(stump);
            return stump;
        }

        private void LateUpdate()
        {
            if (_cut.Count == 0)
                return;
            // Dopo l'Animator: l'arto staccato resta chiuso sul giunto.
            foreach (Transform bone in _cut)
            {
                if (bone != null)
                    bone.localScale = Vector3.one * 0.0001f;
            }
            if (Time.time < _bleedUntil)
            {
                foreach (Transform bone in _cut)
                {
                    if (bone != null && bone.parent != null)
                        Blood(bone.position, (bone.position - bone.parent.position).normalized + Vector3.up * 0.3f, 2);
                }
            }
        }

        /// <summary>Mesh dell'arto (osso + discendenti) nello spazio di bind dell'osso radice. Serve la mesh leggibile.</summary>
        private static Mesh GibMesh(SkinnedMeshRenderer skin, int rootBone)
        {
            Mesh src = skin != null ? skin.sharedMesh : null;
            if (src == null || rootBone < 0 || !src.isReadable)
                return null;
            var key = (src, rootBone);
            if (s_gibCache.TryGetValue(key, out Mesh cached))
                return cached;

            Transform[] bones = skin.bones;
            Transform root = bones[rootBone];
            var inSet = new bool[bones.Length];
            for (int b = 0; b < bones.Length; b++)
                inSet[b] = bones[b] != null && (bones[b] == root || bones[b].IsChildOf(root));

            BoneWeight[] weights = src.boneWeights;
            Vector3[] vertices = src.vertices;
            Vector3[] normals = src.normals;
            Vector2[] uv = src.uv;
            int[] triangles = src.triangles;
            Matrix4x4 bind = src.bindposes[rootBone];

            var remap = new Dictionary<int, int>();
            var outV = new List<Vector3>();
            var outN = new List<Vector3>();
            var outUv = new List<Vector2>();
            var outT = new List<int>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (!inSet[weights[a].boneIndex0] || !inSet[weights[b].boneIndex0] || !inSet[weights[c].boneIndex0])
                    continue;
                foreach (int v in new[] { a, b, c })
                {
                    if (!remap.TryGetValue(v, out int n))
                    {
                        n = outV.Count;
                        remap[v] = n;
                        outV.Add(bind.MultiplyPoint3x4(vertices[v]));
                        outN.Add(normals.Length > v ? bind.MultiplyVector(normals[v]).normalized : Vector3.up);
                        outUv.Add(uv.Length > v ? uv[v] : Vector2.zero);
                    }
                    outT.Add(n);
                }
            }
            Mesh gib = null;
            if (outT.Count > 0)
            {
                gib = new Mesh { name = $"{src.name}_gib_{root.name}" };
                gib.SetVertices(outV);
                gib.SetNormals(outN);
                gib.SetUVs(0, outUv);
                gib.SetTriangles(outT, 0);
                gib.RecalculateBounds();
            }
            s_gibCache[key] = gib;
            return gib;
        }

        private static void Blood(Vector3 position, Vector3 direction, int count)
        {
            if (s_blood == null)
                s_blood = CreateBlood();
            var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = false };
            Vector3 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                p.velocity = (dir + Random.insideUnitSphere * 0.6f) * Random.Range(1.5f, 4.5f);
                p.startSize = Random.Range(0.02f, 0.05f);
                p.startLifetime = Random.Range(0.5f, 1.1f);
                s_blood.Emit(p, 1);
            }
        }

        private static ParticleSystem CreateBlood()
        {
            var go = new GameObject("[EnemyBlood]");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.6f;
            main.maxParticles = 4000;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.02f, 0.02f), new Color(0.25f, 0.01f, 0.01f));
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            ParticleSystem.CollisionModule collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.dampen = 0.9f;
            collision.bounce = 0.05f;
            collision.lifetimeLoss = 0.4f;
            collision.collidesWith = 1 << 6 | 1;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            // Goccia morbida: texture radiale generata, materiale trasparente (alpha blend, niente scrittura nello Z).
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetTexture("_BaseMap", DropTexture());
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        private static Texture2D DropTexture()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BloodDrop" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1f, d)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

#if UNITY_EDITOR
        public void EditorSetup(SkinnedMeshRenderer skin) => _skin = skin;
#endif
    }
}
