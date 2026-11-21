using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>
    /// Effetti cosmetici degli spari (tracer e segni d'impatto), puramente locali e in pool:
    /// nessuna allocazione durante il gioco e nessun oggetto di rete (vedi docs/NETCODE.md).
    /// </summary>
    public sealed class ShotFxPool : MonoBehaviour
    {
        private const int TracerCount = 24;
        private const int ImpactCount = 48;
        private const float TracerLifetime = 0.06f;
        private const float ImpactLifetime = 8f;

        private static ShotFxPool _instance;

        private LineRenderer[] _tracers;
        private float[] _tracerExpiry;
        private Transform[] _impacts;
        private float[] _impactExpiry;
        private int _nextTracer;
        private int _nextImpact;

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
            _tracerExpiry = new float[TracerCount];
            for (int i = 0; i < TracerCount; i++)
            {
                var t = new GameObject("Tracer");
                t.transform.SetParent(transform, false);
                var lr = t.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.startWidth = 0.025f;
                lr.endWidth = 0.01f;
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
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public void Tracer(Vector3 from, Vector3 to)
        {
            LineRenderer lr = _tracers[_nextTracer];
            _tracerExpiry[_nextTracer] = Time.time + TracerLifetime;
            _nextTracer = (_nextTracer + 1) % TracerCount;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.enabled = true;
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

        private void Update()
        {
            float now = Time.time;
            for (int i = 0; i < TracerCount; i++)
            {
                if (_tracers[i].enabled && now >= _tracerExpiry[i])
                    _tracers[i].enabled = false;
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
