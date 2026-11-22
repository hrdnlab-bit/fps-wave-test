using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>
    /// Effetti cosmetici degli spari (tracer e segni d'impatto), puramente locali e in pool.
    /// Il tracciante è una scia corta che viaggia alla velocità del proiettile (come nei giochi AAA), non una linea fissa
    /// dalla canna al bersaglio: muovendosi mentre si spara non resta una riga sospesa nel mondo.
    /// nessuna allocazione durante il gioco e nessun oggetto di rete (vedi docs/NETCODE.md).
    /// </summary>
    public sealed class ShotFxPool : MonoBehaviour
    {
        private const int TracerCount = 24;
        private const int ImpactCount = 48;
        private const int FleshCount = 16;
        private const float FleshLifetime = 0.14f;
        private const float TracerSpeed = 420f;  // m/s, abbastanza lento da vedersi, abbastanza veloce da non distrarre
        private const float TracerLength = 3.5f; // lunghezza della scia (m)
        private const float ImpactLifetime = 8f;

        private static ShotFxPool _instance;

        private LineRenderer[] _tracers;
        private float[] _tracerStart;
        private Vector3[] _tracerFrom;
        private Vector3[] _tracerDir;
        private float[] _tracerDistance;
        private Transform[] _impacts;
        private float[] _impactExpiry;
        private int _nextTracer;
        private int _nextImpact;
        private Transform[] _flesh;
        private float[] _fleshExpiry;
        private int _nextFlesh;
        private Material _fleshMaterial;
        private MaterialPropertyBlock _tracerBlock;
        private MaterialPropertyBlock _fleshBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Restituisce il pool della scena corrente, creandolo al primo uso.</summary>
        public static ShotFxPool Get(Material tracerMaterial, Material impactMaterial)
        {
            if (_instance != null)
                return _instance;
            var go = new GameObject("[ShotFxPool]");
            _instance = go.AddComponent<ShotFxPool>();
            _instance.Build(tracerMaterial, impactMaterial);
            return _instance;
        }

        private void Build(Material tracerMaterial, Material impactMaterial)
        {
            _tracers = new LineRenderer[TracerCount];
            _tracerStart = new float[TracerCount];
            _tracerFrom = new Vector3[TracerCount];
            _tracerDir = new Vector3[TracerCount];
            _tracerDistance = new float[TracerCount];
            for (int i = 0; i < TracerCount; i++)
            {
                var t = new GameObject("Tracer");
                t.transform.SetParent(transform, false);
                var lr = t.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.startWidth = 0.003f; // coda sottile
                lr.endWidth = 0.014f;   // testa
                lr.sharedMaterial = tracerMaterial;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                _tracers[i] = lr;
            }

            _impacts = new Transform[ImpactCount];
            _impactExpiry = new float[ImpactCount];
            for (int i = 0; i < ImpactCount; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "Impact";
                quad.transform.SetParent(transform, false);
                quad.transform.localScale = Vector3.one * 0.08f;
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = impactMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                quad.SetActive(false);
                _impacts[i] = quad.transform;
            }

            // Colpo sui personaggi: schizzo rosso che si allarga e sparisce in un attimo (nessun segno permanente).
            _fleshMaterial = new Material(impactMaterial);
            _fleshMaterial.SetColor("_BaseColor", new Color(0.7f, 0.04f, 0.02f));
            _flesh = new Transform[FleshCount];
            _fleshExpiry = new float[FleshCount];
            for (int i = 0; i < FleshCount; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "FleshHit";
                quad.transform.SetParent(transform, false);
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = _fleshMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                quad.SetActive(false);
                _flesh[i] = quad.transform;
            }
        }

        private void OnDestroy()
        {
            if (_fleshMaterial != null)
                Destroy(_fleshMaterial);
            if (_instance == this)
                _instance = null;
        }

        /// <summary>Tracciante del colore dell'energia dell'arma (più chiaro al centro: sembra una scarica).</summary>
        public void Tracer(Vector3 from, Vector3 to, Color energy)
        {
            _tracerBlock ??= new MaterialPropertyBlock();
            _tracerBlock.SetColor(BaseColorId, Color.Lerp(energy, Color.white, 0.25f) * 1.4f);
            _tracers[_nextTracer].SetPropertyBlock(_tracerBlock);
            Tracer(from, to);
        }

        public void Tracer(Vector3 from, Vector3 to)
        {
            int i = _nextTracer;
            _nextTracer = (_nextTracer + 1) % TracerCount;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.5f)
                return;
            _tracerStart[i] = Time.time;
            _tracerFrom[i] = from;
            _tracerDir[i] = delta / distance;
            _tracerDistance[i] = distance;
            _tracers[i].SetPosition(0, from);
            _tracers[i].SetPosition(1, from);
            _tracers[i].enabled = true;
        }

        public void Impact(Vector3 point, Vector3 normal)
        {
            Transform t = _impacts[_nextImpact];
            _impactExpiry[_nextImpact] = Time.time + ImpactLifetime;
            _nextImpact = (_nextImpact + 1) % ImpactCount;
            // Leggermente staccato dalla superficie per evitare z-fighting; il quad guarda fuori dalla superficie.
            t.SetPositionAndRotation(point + normal * 0.01f, Quaternion.LookRotation(-normal));
            t.gameObject.SetActive(true);
        }

        /// <summary>Colpo su un personaggio: effetto brevissimo, mai lasciato in scena.</summary>
        public void FleshImpact(Vector3 point, Vector3 normal) => FleshImpact(point, normal, new Color(0.7f, 0.04f, 0.02f));

        /// <summary>Colpo su metallo (piastre di corazza): stessa scintilla breve, color brace invece che sangue.</summary>
        public void SparkImpact(Vector3 point, Vector3 normal) => FleshImpact(point, normal, new Color(2.2f, 1.4f, 0.5f));

        private void FleshImpact(Vector3 point, Vector3 normal, Color color)
        {
            _fleshBlock ??= new MaterialPropertyBlock();
            _fleshBlock.SetColor(BaseColorId, color);
            _flesh[_nextFlesh].GetComponent<Renderer>().SetPropertyBlock(_fleshBlock);
            Transform t = _flesh[_nextFlesh];
            _fleshExpiry[_nextFlesh] = Time.time + FleshLifetime;
            _nextFlesh = (_nextFlesh + 1) % FleshCount;
            t.SetPositionAndRotation(point + normal * 0.02f, Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.value * 360f));
            t.localScale = Vector3.one * 0.06f;
            t.gameObject.SetActive(true);
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = 0; i < FleshCount; i++)
            {
                if (_fleshExpiry[i] <= 0f)
                    continue;
                float remaining = _fleshExpiry[i] - now;
                if (remaining <= 0f)
                {
                    _fleshExpiry[i] = 0f;
                    _flesh[i].gameObject.SetActive(false);
                    continue;
                }
                float k = 1f - remaining / FleshLifetime; // 0 -> 1
                _flesh[i].localScale = Vector3.one * Mathf.Lerp(0.06f, 0.2f, k);
            }
            for (int i = 0; i < TracerCount; i++)
            {
                LineRenderer lr = _tracers[i];
                if (!lr.enabled)
                    continue;
                float travelled = (now - _tracerStart[i]) * TracerSpeed;
                float head = Mathf.Min(travelled, _tracerDistance[i]);
                float tail = Mathf.Max(0f, travelled - TracerLength); // la coda continua fino al bersaglio
                if (tail >= _tracerDistance[i])
                {
                    lr.enabled = false;
                    continue;
                }
                lr.SetPosition(0, _tracerFrom[i] + _tracerDir[i] * tail);
                lr.SetPosition(1, _tracerFrom[i] + _tracerDir[i] * head);
            }
            for (int i = 0; i < ImpactCount; i++)
            {
                if (_impactExpiry[i] > 0f && now >= _impactExpiry[i])
                {
                    _impactExpiry[i] = 0f;
                    _impacts[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
