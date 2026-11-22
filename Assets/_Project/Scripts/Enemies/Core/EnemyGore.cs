using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Smembramento cosmetico (solo client), guidato dalla maschera degli arti decisa dal server
    /// (<see cref="EnemyAgent.ClientLimbsChanged"/>): per ogni bit nuovo l'arto sulla mesh si chiude scalando l'osso
    /// a zero (riapplicato dopo l'Animator, in LateUpdate), al suo posto vola un pezzo fisico ricavato dalla mesh
    /// (cache per mesh e osso) e sul taglio resta un moncone vero: carne frastagliata, pelle strappata e osso che
    /// sporge, dimensionati sulla sezione dell'arto (anche sull'estremità del pezzo che vola).
    /// Testa: esplode (schegge di cranio, pezzi di carne, nube di sangue) oppure vola via intera.
    /// Niente oggetti di rete: ogni client lo calcola dalla maschera e dal punto locale del colpo.
    /// </summary>
    public sealed class EnemyGore : MonoBehaviour
    {
        // Ordine = indice arto di EnemyHitRegions (bit della maschera). Osso "punta" per l'asse e la distanza dal colpo.
        private static readonly string[] Limbs = { "Head", "LeftArm", "LeftForeArm", "RightArm", "RightForeArm", "LeftUpLeg", "LeftLeg", "RightUpLeg", "RightLeg" };
        private static readonly string[] LimbEnds = { null, "LeftForeArm", "LeftHand", "RightForeArm", "RightHand", "LeftLeg", "LeftFoot", "RightLeg", "RightFoot" };
        private const float GibLifetime = 7f;
        private const float ChunkLifetime = 5f;
        private const float BleedSeconds = 3f;
        private const int CapVariants = 4;
        // Raggio tipico della sezione al taglio (m, nemico alto 1.8): il valore misurato sulla mesh resta in questo intorno.
        private static readonly float[] LimbRadius = { 0.05f, 0.042f, 0.034f, 0.042f, 0.034f, 0.062f, 0.046f, 0.062f, 0.046f };

        private struct GibInfo
        {
            public Mesh Mesh;
            public Vector3 Axis;    // asse dell'arto nello spazio dell'osso (dal giunto verso la punta)
            public float Radius;    // raggio della sezione al giunto, unità dell'osso
        }

        private static readonly Dictionary<(Mesh, int, int), GibInfo> s_gibCache = new();
        private static Mesh[] s_capMeshes;
        private static Mesh s_chunkMesh;
        private static Material[] s_capMaterials; // pelle strappata, carne, osso
        private static ParticleSystem s_blood;

        [SerializeField] private SkinnedMeshRenderer _skin;

        private Transform[] _limbs;
        private Transform[] _ends;
        private int[] _boneIndex;
        private ushort _applied;
        private readonly List<Transform> _cut = new();
        private readonly List<GameObject> _stumps = new();
        private float _bleedUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_gibCache.Clear();
            s_capMeshes = null;
            s_chunkMesh = null;
            s_capMaterials = null;
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
                {
                    stump.transform.SetParent(transform, false);
                    stump.SetActive(false);
                }
            }
            _applied = 0;
            _bleedUntil = 0f;
        }

        /// <summary>Sangue sul punto del colpo (ogni morte, anche al busto).</summary>
        public void Impact(Vector3 worldPoint, Vector3 shotDirection, int count = 24) =>
            Blood(worldPoint, -shotDirection + Random.insideUnitSphere * 0.3f, count, 1f);

        /// <summary>
        /// Applica la maschera degli arti persi: solo i bit non ancora applicati. Con <paramref name="effects"/> pezzi
        /// che volano e sangue, senza (chi entra a partita in corso) solo arti chiusi e monconi.
        /// </summary>
        public void ApplyMask(ushort mask, Vector3 worldPoint, Vector3 shotDirection, bool effects)
        {
            Resolve();
            if (_limbs == null)
                return;
            ushort fresh = (ushort)(mask & ~_applied);
            if (fresh == 0)
                return;
            Vector3 push = shotDirection.sqrMagnitude > 0.01f ? shotDirection.normalized : -transform.forward;
            // Prima i genitori (braccio prima dell'avambraccio): così il pezzo del braccio include l'avambraccio.
            for (int limb = 0; limb < Limbs.Length; limb++)
            {
                if ((fresh & (1 << limb)) == 0)
                    continue;
                if (limb == 0 && (mask & (1 << EnemyHitRegions.HeadExplodedBit)) != 0)
                    ExplodeHead(push, effects);
                else
                    Sever(limb, push, effects, limb == 0 ? 1.6f : 1f);
            }
            _applied |= mask;
        }

        private bool IsCut(Transform bone)
        {
            for (Transform t = bone; t != null && t != transform; t = t.parent)
            {
                if (_cut.Contains(t))
                    return true;
            }
            return false;
        }

        private void Sever(int limb, Vector3 push, bool effects, float force)
        {
            Transform bone = _limbs[limb];
            if (bone == null || IsCut(bone))
                return;

            // Se un figlio è già staccato (avambraccio), il pezzo del braccio non lo ripete.
            int exclude = -1;
            for (int k = 0; k < Limbs.Length; k++)
            {
                if (k != limb && _limbs[k] != null && _cut.Contains(_limbs[k]) && _limbs[k].IsChildOf(bone))
                    exclude = _boneIndex[k];
            }
            GibInfo info = Gib(_skin, _boneIndex[limb], exclude, _ends[limb] != null ? System.Array.IndexOf(_skin.bones, _ends[limb]) : -1);
            float typical = LimbRadius[limb] * transform.lossyScale.y;
            float measured = info.Radius > 0f ? info.Radius * bone.lossyScale.x : typical;
            float worldRadius = Mathf.Clamp(measured, typical * 0.7f, typical * 1.15f);
            Vector3 worldAxis = bone.TransformDirection(info.Axis).normalized;

            if (effects && info.Mesh != null)
            {
                var piece = new GameObject($"Gib_{Limbs[limb]}") { layer = 2 }; // Ignore Raycast: i colpi lo attraversano
                piece.transform.SetPositionAndRotation(bone.position, bone.rotation);
                piece.transform.localScale = bone.lossyScale;
                piece.AddComponent<MeshFilter>().sharedMesh = info.Mesh;
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _skin.sharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                piece.AddComponent<BoxCollider>();
                var body = piece.AddComponent<Rigidbody>();
                body.mass = limb == 0 ? 4f : 3f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.linearVelocity = (push * Random.Range(3f, 5.5f) + Vector3.up * Random.Range(1.5f, 3.5f) + Random.insideUnitSphere) * force;
                body.angularVelocity = Random.insideUnitSphere * 12f;
                // Moncone anche sull'estremità tagliata del pezzo (guarda verso il corpo).
                AttachCap(piece.transform, Vector3.zero, -info.Axis, worldRadius / Mathf.Max(1e-4f, bone.lossyScale.x), true);
                Destroy(piece, GibLifetime);
            }

            // Moncone sul corpo: figlio dell'osso padre (segue animazione e caduta), orientato lungo l'arto perso.
            if (bone.parent != null)
            {
                Transform parent = bone.parent;
                Vector3 localPos = parent.InverseTransformPoint(bone.position);
                Vector3 localAxis = parent.InverseTransformDirection(worldAxis);
                Vector3 ps = parent.lossyScale;
                AttachCap(parent, localPos, localAxis, worldRadius / Mathf.Max(1e-4f, ps.x), false);
            }

            _cut.Add(bone);
            bone.localScale = Vector3.one * 0.0001f;
            if (effects)
            {
                _bleedUntil = Time.time + BleedSeconds;
                Blood(bone.position, worldAxis + Vector3.up * 0.4f, 45, 1.2f);
                Blood(bone.position, push, 20, 1f);
            }
        }

        private void ExplodeHead(Vector3 push, bool effects)
        {
            Transform head = _limbs[0];
            if (head == null || IsCut(head))
                return;
            Vector3 center = head.position + Vector3.up * 0.09f * transform.lossyScale.y;
            float s = transform.lossyScale.y;
            if (effects)
            {
                EnsureCapAssets();
                // Schegge di cranio e brandelli di carne, spinti dal colpo e verso l'alto.
                int chunks = Random.Range(9, 14);
                for (int i = 0; i < chunks; i++)
                {
                    bool skull = i % 3 == 0;
                    var chunk = new GameObject(skull ? "Gib_Skull" : "Gib_Flesh") { layer = 2 };
                    chunk.transform.SetPositionAndRotation(center + Random.insideUnitSphere * 0.07f * s, Random.rotation);
                    float size = Random.Range(0.025f, skull ? 0.06f : 0.05f) * s;
                    chunk.transform.localScale = new Vector3(size, size * Random.Range(0.4f, 0.9f), size * Random.Range(0.6f, 1.2f));
                    chunk.AddComponent<MeshFilter>().sharedMesh = skull ? s_chunkMesh : s_capMeshes[Random.Range(0, CapVariants)];
                    var r = chunk.AddComponent<MeshRenderer>();
                    r.sharedMaterials = skull ? new[] { s_capMaterials[2] } : s_capMaterials;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    chunk.AddComponent<SphereCollider>().radius = 0.5f;
                    var body = chunk.AddComponent<Rigidbody>();
                    body.mass = 0.2f;
                    body.linearVelocity = push * Random.Range(2f, 5f) + Vector3.up * Random.Range(2f, 4.5f) + Random.insideUnitSphere * 3f;
                    body.angularVelocity = Random.insideUnitSphere * 20f;
                    Destroy(chunk, ChunkLifetime);
                }
                // Nube e spruzzi: tanti schizzi in tutte le direzioni, di più lungo il tiro.
                Blood(center, Vector3.up, 90, 1.6f, 0.9f);
                Blood(center, push, 70, 1.8f);
                _bleedUntil = Time.time + BleedSeconds;
            }

            // Collo: moncone frastagliato con la vertebra che sporge.
            if (head.parent != null)
            {
                Transform parent = head.parent;
                Vector3 axis = (head.position - parent.position).normalized;
                AttachCap(parent, parent.InverseTransformPoint(head.position), parent.InverseTransformDirection(axis),
                    LimbRadius[0] * s / Mathf.Max(1e-4f, parent.lossyScale.x), false);
            }
            _cut.Add(head);
            head.localScale = Vector3.one * 0.0001f;
        }

        /// <summary>Moncone (carne, pelle, osso) figlio di <paramref name="parent"/>, cupola lungo <paramref name="localAxis"/>.</summary>
        private void AttachCap(Transform parent, Vector3 localPos, Vector3 localAxis, float localRadius, bool onGib)
        {
            EnsureCapAssets();
            GameObject cap = onGib ? NewCap() : TakeStump();
            cap.GetComponent<MeshFilter>().sharedMesh = s_capMeshes[Random.Range(0, CapVariants)];
            cap.transform.SetParent(parent, false);
            cap.transform.localPosition = localPos;
            Vector3 axis = localAxis.sqrMagnitude > 1e-6f ? localAxis.normalized : Vector3.up;
            cap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float r = Mathf.Max(1e-4f, localRadius) * 0.92f; // appena dentro la pelle: niente bordo che sporge
            cap.transform.localScale = new Vector3(r, r, r);
            cap.SetActive(true);
        }

        private GameObject TakeStump()
        {
            foreach (GameObject s in _stumps)
            {
                if (s != null && !s.activeSelf)
                    return s;
            }
            GameObject stump = NewCap();
            _stumps.Add(stump);
            return stump;
        }

        private static GameObject NewCap()
        {
            var cap = new GameObject("Stump") { layer = 2 };
            cap.AddComponent<MeshFilter>();
            var r = cap.AddComponent<MeshRenderer>();
            r.sharedMaterials = s_capMaterials;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return cap;
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
            if (Time.time < _bleedUntil && Random.value < 0.6f)
            {
                foreach (Transform bone in _cut)
                {
                    if (bone != null && bone.parent != null)
                        Blood(bone.position, (bone.position - bone.parent.position).normalized + Vector3.up * 0.2f, 1, 0.6f);
                }
            }
        }

        // ------------------------------------------------------------------ Mesh dei pezzi

        /// <summary>
        /// Pezzo dell'arto (osso + discendenti, senza il ramo <paramref name="exclude"/>) nello spazio di bind dell'osso,
        /// con asse e raggio della sezione al giunto. Serve la mesh leggibile.
        /// </summary>
        private static GibInfo Gib(SkinnedMeshRenderer skin, int rootBone, int exclude, int endBone)
        {
            Mesh src = skin != null ? skin.sharedMesh : null;
            if (src == null || rootBone < 0 || !src.isReadable)
                return default;
            var key = (src, rootBone, exclude);
            if (s_gibCache.TryGetValue(key, out GibInfo cached))
                return cached;

            Transform[] bones = skin.bones;
            Transform root = bones[rootBone];
            Transform skip = exclude >= 0 ? bones[exclude] : null;
            var inSet = new bool[bones.Length];
            for (int b = 0; b < bones.Length; b++)
                inSet[b] = bones[b] != null && (bones[b] == root || bones[b].IsChildOf(root))
                           && !(skip != null && (bones[b] == skip || bones[b].IsChildOf(skip)));

            BoneWeight[] weights = src.boneWeights;
            Vector3[] vertices = src.vertices;
            Vector3[] normals = src.normals;
            Vector2[] uv = src.uv;
            int[] triangles = src.triangles;
            Matrix4x4[] bindposes = src.bindposes;
            Matrix4x4 bind = bindposes[rootBone];

            // Asse: verso l'osso punta (in bind), altrimenti la Y dell'osso.
            Vector3 axis = Vector3.up;
            float length = 0.25f;
            if (endBone >= 0)
            {
                Vector3 endMesh = bindposes[endBone].inverse.MultiplyPoint3x4(Vector3.zero);
                Vector3 endLocal = bind.MultiplyPoint3x4(endMesh);
                if (endLocal.sqrMagnitude > 1e-8f)
                {
                    length = endLocal.magnitude;
                    axis = endLocal / length;
                }
            }

            var remap = new Dictionary<int, int>();
            var outV = new List<Vector3>();
            var outN = new List<Vector3>();
            var outUv = new List<Vector2>();
            var outT = new List<int>();
            var corner = new int[3];
            for (int t = 0; t < triangles.Length; t += 3)
            {
                corner[0] = triangles[t]; corner[1] = triangles[t + 1]; corner[2] = triangles[t + 2];
                if (!inSet[weights[corner[0]].boneIndex0] || !inSet[weights[corner[1]].boneIndex0] || !inSet[weights[corner[2]].boneIndex0])
                    continue;
                foreach (int v in corner)
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

            // Raggio: distanza media dall'asse dei vertici vicini al giunto (primo 20% della lunghezza).
            float sum = 0f;
            int count = 0;
            foreach (Vector3 v in outV)
            {
                float along = Vector3.Dot(v, axis);
                if (along < -0.02f * length || along > 0.2f * length)
                    continue;
                sum += (v - axis * along).magnitude;
                count++;
            }

            var info = new GibInfo { Axis = axis, Radius = count > 3 ? sum / count : 0f };
            if (outT.Count > 0)
            {
                var gib = new Mesh { name = $"{src.name}_gib_{root.name}" };
                gib.SetVertices(outV);
                gib.SetNormals(outN);
                gib.SetUVs(0, outUv);
                gib.SetTriangles(outT, 0);
                gib.RecalculateBounds();
                info.Mesh = gib;
            }
            s_gibCache[key] = info;
            return info;
        }

        private static void EnsureCapAssets()
        {
            if (s_capMeshes != null)
                return;
            s_capMeshes = new Mesh[CapVariants];
            for (int i = 0; i < CapVariants; i++)
                s_capMeshes[i] = BuildCapMesh(1234 + i * 97);
            s_chunkMesh = BuildChunkMesh(77);
            s_capMaterials = new[]
            {
                CapMaterial("GoreSkin", new Color(0.3f, 0.1f, 0.08f), 0.3f),     // bordo di pelle strappata
                CapMaterial("GoreFlesh", new Color(0.62f, 0.07f, 0.05f), 0.7f),  // carne viva, lucida
                CapMaterial("GoreBone", new Color(0.86f, 0.8f, 0.68f), 0.45f),   // osso
            };
        }

        private static Material CapMaterial(string name, Color color, float smoothness)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        /// <summary>
        /// Moncone di raggio 1 lungo +Y: anello di pelle frastagliata che si sovrappone alla pelle (sotto lo 0),
        /// carne a cupola irregolare, osso (cilindro spezzato) che sporge un po' dal centro. Tre submesh.
        /// </summary>
        private static Mesh BuildCapMesh(int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            const int seg = 14;
            var v = new List<Vector3>();
            var skin = new List<int>();
            var flesh = new List<int>();
            var bone = new List<int>();

            float[] jag = new float[seg];
            for (int i = 0; i < seg; i++)
                jag[i] = R(0.88f, 1.12f);

            // Pelle: anello da sotto lo 0 (dentro il corpo) a un bordo strappato, un po' rientrato.
            int skinBase = v.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(dir * 1.0f + Vector3.down * 0.12f);
                v.Add(dir * (0.95f * jag[i]) + Vector3.up * R(-0.02f, 0.07f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = skinBase + i * 2, a1 = a0 + 1;
                int b0 = skinBase + (i + 1) % seg * 2, b1 = b0 + 1;
                skin.AddRange(new[] { a0, a1, b1, a0, b1, b0 });
            }

            // Carne: due anelli irregolari e una punta, cupola bassa.
            int ring1 = v.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(dir * (0.84f * jag[i]) + Vector3.up * R(0.01f, 0.06f));
            }
            int ring2 = v.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(dir * R(0.42f, 0.6f) + Vector3.up * R(0.04f, 0.1f));
            }
            int tip = v.Count;
            v.Add(new Vector3(R(-0.1f, 0.1f), R(0.06f, 0.12f), R(-0.1f, 0.1f)));
            for (int i = 0; i < seg; i++)
            {
                int a = ring1 + i, b = ring1 + (i + 1) % seg, c = ring2 + i, d = ring2 + (i + 1) % seg;
                flesh.AddRange(new[] { a, c, b, b, c, d });
                flesh.AddRange(new[] { c, tip, d });
            }
            // Raccordo pelle-carne (il bordo della pelle verso il primo anello di carne).
            for (int i = 0; i < seg; i++)
            {
                int s0 = skinBase + i * 2 + 1, s1 = skinBase + (i + 1) % seg * 2 + 1;
                int f0 = ring1 + i, f1 = ring1 + (i + 1) % seg;
                flesh.AddRange(new[] { s0, f0, f1, s0, f1, s1 });
            }

            // Osso: cilindro sottile con la cima spezzata, leggermente fuori centro.
            const int bseg = 8;
            Vector3 off = new(R(-0.08f, 0.08f), 0f, R(-0.08f, 0.08f));
            float br = R(0.17f, 0.23f);
            int boneBase = v.Count;
            for (int i = 0; i < bseg; i++)
            {
                float a = i * Mathf.PI * 2f / bseg;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(off + dir * br + Vector3.up * 0.05f);
                v.Add(off + dir * br * R(0.75f, 1f) + Vector3.up * R(0.3f, 0.55f));
            }
            int boneTop = v.Count;
            v.Add(off + Vector3.up * 0.42f);
            for (int i = 0; i < bseg; i++)
            {
                int a0 = boneBase + i * 2, a1 = a0 + 1;
                int b0 = boneBase + (i + 1) % bseg * 2, b1 = b0 + 1;
                bone.AddRange(new[] { a0, a1, b1, a0, b1, b0 });
                bone.AddRange(new[] { a1, boneTop, b1 });
            }

            var mesh = new Mesh { name = $"GoreCap_{seed}" };
            mesh.SetVertices(v);
            mesh.subMeshCount = 3;
            // Facce in senso orario viste da fuori (+Y): se gli indici sono al contrario, si girano.
            mesh.SetTriangles(FaceOutward(v, skin, true), 0);
            mesh.SetTriangles(FaceOutward(v, flesh, false), 1);
            mesh.SetTriangles(FaceOutward(v, bone, true), 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Orienta ogni triangolo con la normale verso l'esterno (radiale per le pareti, verso l'alto per la cupola).</summary>
        private static List<int> FaceOutward(List<Vector3> v, List<int> tris, bool radial)
        {
            var result = new List<int>(tris.Count);
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 mid = (a + b + c) / 3f;
                Vector3 outward = radial ? new Vector3(mid.x, 0f, mid.z) + Vector3.up * 0.15f : mid + Vector3.up * 0.5f;
                if (Vector3.Dot(n, outward) < 0f)
                    result.AddRange(new[] { tris[t], tris[t + 2], tris[t + 1] });
                else
                    result.AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
            }
            return result;
        }

        /// <summary>Scheggia irregolare (ottaedro deformato) per i frammenti di cranio.</summary>
        private static Mesh BuildChunkMesh(int seed)
        {
            var rng = new System.Random(seed);
            float R() => 0.6f + (float)rng.NextDouble() * 0.5f;
            var v = new List<Vector3>
            {
                Vector3.right * R(), Vector3.left * R(), Vector3.up * R() * 0.5f, Vector3.down * R() * 0.5f, Vector3.forward * R(), Vector3.back * R(),
            };
            int[] t = { 0, 2, 4, 4, 2, 1, 1, 2, 5, 5, 2, 0, 0, 4, 3, 4, 1, 3, 1, 5, 3, 5, 0, 3 };
            var mesh = new Mesh { name = "GoreChunk" };
            mesh.SetVertices(v);
            var list = new List<int>(t);
            mesh.SetTriangles(FaceOutward(v, list, false), 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ Sangue

        private static void Blood(Vector3 position, Vector3 direction, int count, float speed, float spread = 0.6f)
        {
            if (s_blood == null)
                s_blood = CreateBlood();
            var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = false };
            Vector3 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                p.velocity = (dir + Random.insideUnitSphere * spread) * Random.Range(1.5f, 4.5f) * speed;
                p.startSize = Random.Range(0.02f, 0.055f);
                p.startLifetime = Random.Range(0.5f, 1.2f);
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
