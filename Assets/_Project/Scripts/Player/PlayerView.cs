using FishNet.Object;
using UnityEngine;
using UnityEngine.Rendering;

namespace HrdnLab.FpsWave.Player
{
    /// <summary>
    /// Configura cosa vede ciascuna istanza: il player locale usa la propria camera e vede solo l'ombra
    /// del proprio corpo; i player remoti hanno la camera spenta e il corpo visibile.
    /// </summary>
    public sealed class PlayerView : NetworkBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private AudioListener _audioListener;
        [SerializeField] private Renderer[] _bodyRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private GameObject _firstPersonRoot;
        [SerializeField] private Behaviour[] _ownerOnlyBehaviours = System.Array.Empty<Behaviour>();

        private void Awake() => SetLocal(false);

        public override void OnStartClient()
        {
            base.OnStartClient();
            SetLocal(IsOwner);
        }

        private void SetLocal(bool isLocal)
        {
            if (_camera != null) _camera.enabled = isLocal;
            if (_audioListener != null) _audioListener.enabled = isLocal;
            if (_firstPersonRoot != null) _firstPersonRoot.SetActive(isLocal);
            foreach (Renderer r in _bodyRenderers)
            {
                if (r != null)
                    r.shadowCastingMode = isLocal ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            }
            foreach (Behaviour b in _ownerOnlyBehaviours)
            {
                if (b != null)
                    b.enabled = isLocal;
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Camera camera, AudioListener listener, Renderer[] body, GameObject firstPersonRoot, Behaviour[] ownerOnly)
        {
            _camera = camera;
            _audioListener = listener;
            _bodyRenderers = body;
            _firstPersonRoot = firstPersonRoot;
            _ownerOnlyBehaviours = ownerOnly;
        }
#endif
    }
}
