using System;
using UnityEngine;

namespace HrdnLab.FpsWave.Player.Character
{
    /// <summary>Tipo di impugnatura dell'arma tenuta in terza persona.</summary>
    public enum CharacterGrip : byte
    {
        None = 0,
        Rifle = 1,
        Pistol = 2,
    }

    /// <summary>Ingressi della posa di un frame (spazio del personaggio dove non indicato).</summary>
    public struct CharacterPoseInput
    {
        /// <summary>Velocità orizzontale nello spazio del personaggio (x destra, z avanti), m/s.</summary>
        public Vector3 LocalVelocity;
        public bool Grounded;
        /// <summary>0 in piedi, 1 accucciato.</summary>
        public float Crouch;
        /// <summary>Pitch della visuale in gradi (positivo = guarda in basso).</summary>
        public float Pitch;
        /// <summary>Bersagli mondo dei polsi (null = braccia libere).</summary>
        public Transform RightHandTarget;
        public Transform LeftHandTarget;
        /// <summary>0 = mano sull'arma (IK pieno), 1 = mano libera (posa della clip, es. sinistra durante la ricarica).</summary>
        public float RightHandRelease;
        public float LeftHandRelease;
        public float Dt;
    }

    /// <summary>
    /// Scheletro del personaggio AIRO animato via codice: gambe con IK sul ciclo del passo, crouch,
    /// busto e testa che seguono la mira, mani agganciate all'arma con IK a due ossa e dita chiuse.
    /// Nessun Animator: nessun dato di animazione passa in rete, tutto deriva da velocità, pitch e flag.
    /// Le ossa sono assegnate dallo strumento <c>FpsWave/Setup/9</c>; le rotazioni sono applicate nello
    /// spazio del personaggio, quindi non dipendono dagli assi locali delle ossa importate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterRig : MonoBehaviour
    {
        [Serializable]
        public sealed class Limb
        {
            public Transform Upper;
            public Transform Lower;
            public Transform End;
        }

        [SerializeField] private CharacterAnimationSettings _settings;
        [SerializeField] private Transform _hips;
        [SerializeField] private Transform _spine;
        [SerializeField] private Transform _spine1;
        [SerializeField] private Transform _spine2;
        [SerializeField] private Transform _neck;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _leftShoulder;
        [SerializeField] private Transform _rightShoulder;
        [SerializeField] private Limb _leftLeg = new();
        [SerializeField] private Limb _rightLeg = new();
        [SerializeField] private Limb _leftArm = new();
        [SerializeField] private Limb _rightArm = new();
        [Tooltip("Primo osso di ogni dito (pollice incluso); le falangi sono i figli.")]
        [SerializeField] private Transform[] _leftFingers = Array.Empty<Transform>();
        [SerializeField] private Transform[] _rightFingers = Array.Empty<Transform>();
        [SerializeField] private Renderer[] _renderers = Array.Empty<Renderer>();
        [Tooltip("Corpo senza testa, collo e braccia per la prima persona (generato da Setup/9).")]
        [SerializeField] private SkinnedMeshRenderer _body;
        [SerializeField] private Mesh _firstPersonMesh;
        [Tooltip("Animator con le clip di locomozione (Pilot): se c'è, gambe e busto vengono dalle clip e qui si aggiungono mira e mani.")]
        [SerializeField] private Animator _animator;
        [SerializeField, Min(0.01f)] private float _reloadClipLength = 1f;
        [Tooltip("Con le clip: distanza fra l'osso del piede più basso e la suola nella posa Idle (m, misurata dal builder).")]
        [SerializeField, Min(0f)] private float _soleBelowFoot = 0.08f;
        private int _clipGroundStage;   // 0 da fare, 1 fatto alla buona (anche in moto), 2 fatto da fermi
        private int _clipGroundFrames;

        private Transform[] _posed;
        private Quaternion[] _restLocal;
        private Vector3 _hipsRestLocalPos;
        private Vector3 _leftFootRest, _rightFootRest;            // spazio del personaggio
        private Quaternion _leftFootRestRot, _rightFootRestRot;   // relativo al personaggio
        private float _legUpper, _legLower, _armUpper, _armLower;
        private Quaternion _leftHandFrameInv, _rightHandFrameInv;
        private float _phase;
        private float _moveWeight;
        private Vector3 _moveDir = Vector3.forward;
        private float _airWeight;
        private bool _initialized;
        private bool _firstPerson;
        private int _leftFingerJoints;

        public CharacterAnimationSettings Settings => _settings;
        public Transform Head => _head;
        public Transform LeftShoulder => _leftShoulder;
        public Transform RightShoulder => _rightShoulder;
        public Renderer[] Renderers => _renderers;
        public Animator Animator => _animator;
        public float ReloadClipLength => _reloadClipLength;
        /// <summary>Gambe e busto animati dalle clip (Animator attivo con controller).</summary>
        public bool Animated => _animator != null && _animator.enabled && _animator.runtimeAnimatorController != null;

        public bool IsVisible
        {
            get
            {
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null && _renderers[i].isVisible) return true;
                return false;
            }
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_initialized || _hips == null)
                return;
            _initialized = true;
            _posed = new[]
            {
                _hips, _spine, _spine1, _spine2, _neck, _head,
                _leftLeg.Upper, _leftLeg.Lower, _leftLeg.End, _rightLeg.Upper, _rightLeg.Lower, _rightLeg.End,
                _leftArm.Upper, _leftArm.Lower, _leftArm.End, _rightArm.Upper, _rightArm.Lower, _rightArm.End,
            };
            _leftFingerJoints = CountJoints(_leftFingers);
            int fingerJoints = _leftFingerJoints + CountJoints(_rightFingers);
            Array.Resize(ref _posed, _posed.Length + fingerJoints);
            int k = 18;
            k = AppendJoints(_leftFingers, k);
            AppendJoints(_rightFingers, k);

            _restLocal = new Quaternion[_posed.Length];
            for (int i = 0; i < _posed.Length; i++)
                _restLocal[i] = _posed[i] != null ? _posed[i].localRotation : Quaternion.identity;
            _hipsRestLocalPos = _hips.localPosition;

            Transform root = transform;
            _leftFootRest = root.InverseTransformPoint(_leftLeg.End.position);
            _rightFootRest = root.InverseTransformPoint(_rightLeg.End.position);
            _leftFootRestRot = Quaternion.Inverse(root.rotation) * _leftLeg.End.rotation;
            _rightFootRestRot = Quaternion.Inverse(root.rotation) * _rightLeg.End.rotation;
            _legUpper = Vector3.Distance(_leftLeg.Upper.position, _leftLeg.Lower.position);
            _legLower = Vector3.Distance(_leftLeg.Lower.position, _leftLeg.End.position);
            _armUpper = Vector3.Distance(_rightArm.Upper.position, _rightArm.Lower.position);
            _armLower = Vector3.Distance(_rightArm.Lower.position, _rightArm.End.position);

            // Posa di riferimento a T: palmi verso il basso, dita lungo il braccio.
            _leftHandFrameInv = HandFrameInverse(_leftArm.End, _leftFingers, -root.up);
            _rightHandFrameInv = HandFrameInverse(_rightArm.End, _rightFingers, -root.up);
        }

        private static int CountJoints(Transform[] fingers)
        {
            int n = 0;
            foreach (Transform f in fingers)
                for (Transform t = f; t != null; t = t.childCount > 0 ? t.GetChild(0) : null) n++;
            return n;
        }

        private int AppendJoints(Transform[] fingers, int k)
        {
            foreach (Transform f in fingers)
                for (Transform t = f; t != null; t = t.childCount > 0 ? t.GetChild(0) : null) _posed[k++] = t;
            return k;
        }

        private static Quaternion HandFrameInverse(Transform hand, Transform[] fingers, Vector3 palmWorld)
        {
            Vector3 tip = hand.position + hand.forward * 0.05f;
            // Dito medio (secondo dell'elenco: Index, Middle, ...) o il primo disponibile.
            Transform middle = fingers.Length > 1 ? fingers[1] : fingers.Length > 0 ? fingers[0] : null;
            if (middle != null) tip = middle.position;
            Vector3 fingersLocal = hand.InverseTransformDirection((tip - hand.position).normalized);
            Vector3 palmLocal = hand.InverseTransformDirection(palmWorld);
            return Quaternion.Inverse(Quaternion.LookRotation(fingersLocal, palmLocal));
        }

        /// <summary>
        /// Prima persona del proprietario: il corpo usa una mesh senza testa, collo e braccia (si vedono le braccia FPS
        /// e non l'interno del casco), mentre una copia solo-ombra della mesh completa proietta l'ombra intera.
        /// </summary>
        public void SetFirstPerson(bool firstPerson)
        {
            if (_firstPerson == firstPerson || _body == null || _firstPersonMesh == null)
                return;
            _firstPerson = firstPerson;
            if (!firstPerson)
                return;
            var shadowGo = new GameObject(_body.name + "_Shadow") { layer = _body.gameObject.layer };
            shadowGo.transform.SetParent(_body.transform.parent, false);
            shadowGo.transform.SetLocalPositionAndRotation(_body.transform.localPosition, _body.transform.localRotation);
            shadowGo.transform.localScale = _body.transform.localScale;
            var shadow = shadowGo.AddComponent<SkinnedMeshRenderer>();
            shadow.sharedMesh = _body.sharedMesh;
            shadow.bones = _body.bones;
            shadow.rootBone = _body.rootBone;
            shadow.sharedMaterials = _body.sharedMaterials;
            shadow.quality = _body.quality;
            shadow.updateWhenOffscreen = true;
            shadow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            _body.sharedMesh = _firstPersonMesh;
            _body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Calcola e applica la posa del frame. Zero allocazioni.</summary>
        public void Solve(in CharacterPoseInput input)
        {
            Initialize();
            if (!_initialized || _settings == null)
                return;
            CharacterAnimationSettings s = _settings;
            Transform root = transform;
            float dt = Mathf.Max(0f, input.Dt);

            if (Animated)
            {
                GroundClipModel(root, input.Grounded, input.LocalVelocity.sqrMagnitude < 0.04f && input.Crouch < 0.01f);
                SolveOverClips(input, s, root);
                return;
            }

            for (int i = 0; i < _posed.Length; i++)
                if (_posed[i] != null) _posed[i].localRotation = _restLocal[i];
            _hips.localPosition = _hipsRestLocalPos;

            // ---------------------------------------------------------- passo
            Vector3 v = input.LocalVelocity;
            v.y = 0f;
            float speed = v.magnitude;
            _moveWeight = CharacterPoseMath.Damp(_moveWeight, Mathf.Clamp01(speed / 1.2f), 10f, dt);
            if (speed > 0.15f)
                _moveDir = CharacterPoseMath.Damp(_moveDir, v / speed, 12f, dt).normalized;
            float stride = CharacterPoseMath.StrideLength(speed, s.WalkStride, s.RunStride, s.WalkSpeed, s.RunSpeed);
            if (input.Grounded)
                _phase += speed * dt / Mathf.Max(0.1f, stride);
            _airWeight = CharacterPoseMath.Damp(_airWeight, input.Grounded ? 0f : 1f, 12f, dt);
            float run = Mathf.InverseLerp(s.WalkSpeed, s.RunSpeed, speed);
            float lift = Mathf.Lerp(s.WalkLift, s.RunLift, run);
            float crouch = Mathf.Clamp01(input.Crouch);

            // Bacino: abbassamento in crouch, saltello del passo, oscillazione.
            float bob = s.HipBob * _moveWeight * Mathf.Abs(Mathf.Sin(_phase * Mathf.PI * 2f));
            _hips.position += root.up * (-crouch * s.CrouchHipDrop - bob);
            float sway = Mathf.Sin(_phase * Mathf.PI * 2f) * s.HipSway * _moveWeight;
            RotateWorld(_hips, root.up, sway);

            // Gambe con IK verso i piedi sul ciclo del passo.
            Vector3 moveWorld = root.TransformDirection(_moveDir);
            SolveLeg(_leftLeg, _leftFootRest, _leftFootRestRot, _phase, moveWorld, stride, lift, crouch, root);
            SolveLeg(_rightLeg, _rightFootRest, _rightFootRestRot, _phase + 0.5f, moveWorld, stride, lift, crouch, root);

            // ---------------------------------------------------------- busto e testa
            float forwardSpeed = Mathf.Max(0f, v.z);
            float lean = s.RunLean * Mathf.InverseLerp(1f, s.RunSpeed, forwardSpeed) + s.CrouchLean * crouch;
            float pitch = Mathf.Clamp(input.Pitch, -CharacterPoseMath.MaxPitch, CharacterPoseMath.MaxPitch);
            float spinePitch = pitch * s.SpinePitchShare * (_firstPerson ? 0.25f : 1f); // in prima persona il busto resta sotto la camera
            RotateWorld(_spine, root.right, lean * 0.4f);
            RotateWorld(_spine1, root.right, lean * 0.3f + spinePitch * 0.45f);
            RotateWorld(_spine2, root.right, lean * 0.3f + spinePitch * 0.55f);
            RotateWorld(_spine1, root.up, -sway * 0.6f); // contro-rotazione del busto sul bacino
            float headPitch = pitch * s.HeadPitchShare - lean * 0.8f;
            RotateWorld(_neck, root.right, headPitch * 0.4f);
            RotateWorld(_head, root.right, headPitch * 0.6f);

            // ---------------------------------------------------------- braccia (anche in prima persona, per l'ombra)
            if (input.RightHandTarget != null)
            {
                SolveArm(_rightArm, input.RightHandTarget, root.right * 0.6f - root.up - root.forward * 0.2f);
                OrientHand(_rightArm.End, input.RightHandTarget, _rightHandFrameInv);
                CurlFingers(_rightFingers, _rightArm.End, input.RightHandTarget, s.GripCurl, s.ThumbCurl);
            }
            if (input.LeftHandTarget != null)
            {
                SolveArm(_leftArm, input.LeftHandTarget, -root.right * 0.8f - root.up);
                OrientHand(_leftArm.End, input.LeftHandTarget, _leftHandFrameInv);
                CurlFingers(_leftFingers, _leftArm.End, input.LeftHandTarget, s.SupportCurl, s.ThumbCurl);
            }
        }

        /// <summary>
        /// Con le clip, al primo frame animato: il modello si sposta in verticale perché le suole tocchino terra (radice del
        /// rig). L'altezza della posa che l'Animator produce in gioco non coincide con quella misurabile nell'Editor
        /// (posa di default dell'FBX o SampleAnimation), quindi si misura qui una volta sola, sulla posa vera.
        /// </summary>
        private void GroundClipModel(Transform root, bool grounded, bool still)
        {
            if (_clipGroundStage >= 2 || _leftLeg.End == null || _rightLeg.End == null)
                return;
            // Qualche frame a terra perché l'Animator sia già sulla locomozione; poi di nuovo, più preciso, da fermi (Idle).
            _clipGroundFrames = grounded ? _clipGroundFrames + 1 : 0;
            if (_clipGroundFrames < 3 || (_clipGroundStage == 1 && !still))
                return;
            _clipGroundStage = still ? 2 : 1;
            Transform model = _animator.transform;
            if (model == root)
                return;
            float foot = Mathf.Min(root.InverseTransformPoint(_leftLeg.End.position).y,
                root.InverseTransformPoint(_rightLeg.End.position).y);
            float error = foot - _soleBelowFoot;
            if (Mathf.Abs(error) > 0.005f)
                model.position -= root.up * (error * root.lossyScale.y);
        }

        /// <summary>
        /// Con le clip: l'Animator ha già scritto la posa del frame (passo, accucciata, ricarica sul busto); qui si aggiungono
        /// solo la mira (busto e testa seguono il pitch) e le mani sull'arma con l'IK, sfumate dal rilascio.
        /// </summary>
        private void SolveOverClips(in CharacterPoseInput input, CharacterAnimationSettings s, Transform root)
        {
            float pitch = Mathf.Clamp(input.Pitch, -CharacterPoseMath.MaxPitch, CharacterPoseMath.MaxPitch);
            float spinePitch = pitch * s.SpinePitchShare * (_firstPerson ? 0.25f : 1f);
            RotateWorld(_spine1, root.right, spinePitch * 0.45f);
            RotateWorld(_spine2, root.right, spinePitch * 0.55f);
            float headPitch = pitch * s.HeadPitchShare;
            RotateWorld(_neck, root.right, headPitch * 0.4f);
            RotateWorld(_head, root.right, headPitch * 0.6f);

            // Dita: si parte dalla posa di riposo (dritte) e si chiudono sull'arma, come nella versione procedurale.
            if (input.LeftHandTarget != null && input.LeftHandRelease < 0.999f)
                ResetJoints(18, 18 + _leftFingerJoints);
            if (input.RightHandTarget != null && input.RightHandRelease < 0.999f)
                ResetJoints(18 + _leftFingerJoints, _posed.Length);
            if (input.RightHandTarget != null && input.RightHandRelease < 0.999f)
                BlendedArm(_rightArm, input.RightHandTarget, root.right * 0.6f - root.up - root.forward * 0.2f, _rightHandFrameInv,
                    _rightFingers, s.GripCurl, s.ThumbCurl, 1f - input.RightHandRelease);
            if (input.LeftHandTarget != null && input.LeftHandRelease < 0.999f)
                BlendedArm(_leftArm, input.LeftHandTarget, -root.right * 0.8f - root.up, _leftHandFrameInv,
                    _leftFingers, s.SupportCurl, s.ThumbCurl, 1f - input.LeftHandRelease);
        }

        private void ResetJoints(int from, int to)
        {
            for (int i = from; i < to && i < _posed.Length; i++)
                if (_posed[i] != null) _posed[i].localRotation = _restLocal[i];
        }

        /// <summary>IK del braccio verso l'arma con peso: 1 = mano sul bersaglio, 0 = posa della clip.</summary>
        private void BlendedArm(Limb arm, Transform target, Vector3 elbowHint, Quaternion handFrameInv, Transform[] fingers,
            float curl, float thumbCurl, float weight)
        {
            Quaternion upper = arm.Upper.rotation, lower = arm.Lower.rotation, hand = arm.End.rotation;
            SolveArm(arm, target, elbowHint);
            OrientHand(arm.End, target, handFrameInv);
            if (weight < 0.999f)
            {
                Quaternion ikUpper = arm.Upper.rotation, ikLower = arm.Lower.rotation, ikHand = arm.End.rotation;
                arm.Upper.rotation = Quaternion.Slerp(upper, ikUpper, weight);
                arm.Lower.rotation = Quaternion.Slerp(lower, ikLower, weight);
                arm.End.rotation = Quaternion.Slerp(hand, ikHand, weight);
            }
            CurlFingers(fingers, arm.End, target, curl * weight, thumbCurl * weight);
        }

        private void SolveLeg(Limb leg, Vector3 footRest, Quaternion footRestRot, float phase, Vector3 moveWorld,
            float stride, float lift, float crouch, Transform root)
        {
            CharacterAnimationSettings s = _settings;
            Vector2 cycle = CharacterPoseMath.FootCycle(phase, s.StanceRatio);
            Vector3 target = root.TransformPoint(footRest + new Vector3(0f, 0f, -0.06f * crouch));
            target += moveWorld * (cycle.x * stride * _moveWeight) + root.up * (cycle.y * lift * _moveWeight);
            target += root.up * (s.AirFootLift * _airWeight) - root.forward * (0.08f * _airWeight);

            Vector3 hint = root.forward + root.up * 0.1f;
            SolveChain(leg, target, _legUpper, _legLower, hint);
            float footPitch = -cycle.y * 25f * _moveWeight + 15f * _airWeight;
            leg.End.rotation = Quaternion.AngleAxis(footPitch, root.right) * root.rotation * footRestRot;
        }

        private void SolveArm(Limb arm, Transform target, Vector3 elbowHint) =>
            SolveChain(arm, target.position, _armUpper, _armLower, elbowHint);

        private static void SolveChain(Limb limb, Vector3 target, float upperLength, float lowerLength, Vector3 hint)
        {
            Vector3 a = limb.Upper.position;
            Vector3 mid = CharacterPoseMath.SolveMidJoint(a, target, upperLength, lowerLength, hint, out Vector3 reached);
            limb.Upper.rotation = Quaternion.FromToRotation(limb.Lower.position - a, mid - a) * limb.Upper.rotation;
            Vector3 b = limb.Lower.position;
            limb.Lower.rotation = Quaternion.FromToRotation(limb.End.position - b, reached - b) * limb.Lower.rotation;
        }

        /// <summary>Il bersaglio indica con forward la direzione delle dita e con up la normale del palmo.</summary>
        private static void OrientHand(Transform hand, Transform target, Quaternion frameInverse) =>
            hand.rotation = Quaternion.LookRotation(target.forward, target.up) * frameInverse;

        private static void CurlFingers(Transform[] fingers, Transform hand, Transform target, float curl, float thumbCurl)
        {
            Vector3 axis = Vector3.Cross(target.forward, target.up);
            for (int f = 0; f < fingers.Length; f++)
            {
                float amount = f == fingers.Length - 1 ? thumbCurl : curl; // ultimo = pollice
                for (Transform t = fingers[f]; t != null; t = t.childCount > 0 ? t.GetChild(0) : null)
                    t.rotation = Quaternion.AngleAxis(amount, axis) * t.rotation;
            }
        }

        private static void RotateWorld(Transform bone, Vector3 axis, float degrees)
        {
            if (bone != null && degrees != 0f)
                bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

#if UNITY_EDITOR
        public SkinnedMeshRenderer EditorBody => _body;

        public void EditorSetFirstPersonMesh(Mesh mesh) => _firstPersonMesh = mesh;

        public void EditorSetAnimator(Animator animator, float reloadClipLength, float soleBelowFoot = 0.08f)
        {
            _animator = animator;
            _reloadClipLength = Mathf.Max(0.01f, reloadClipLength);
            _soleBelowFoot = Mathf.Max(0f, soleBelowFoot);
        }

        public void EditorSetup(CharacterAnimationSettings settings, Transform hips, Transform spine, Transform spine1,
            Transform spine2, Transform neck, Transform head, Transform leftShoulder, Transform rightShoulder,
            Limb leftLeg, Limb rightLeg, Limb leftArm, Limb rightArm, Transform[] leftFingers, Transform[] rightFingers,
            Renderer[] renderers, SkinnedMeshRenderer body, Mesh firstPersonMesh)
        {
            _body = body;
            _firstPersonMesh = firstPersonMesh;
            _settings = settings;
            _hips = hips; _spine = spine; _spine1 = spine1; _spine2 = spine2; _neck = neck; _head = head;
            _leftShoulder = leftShoulder; _rightShoulder = rightShoulder;
            _leftLeg = leftLeg; _rightLeg = rightLeg; _leftArm = leftArm; _rightArm = rightArm;
            _leftFingers = leftFingers; _rightFingers = rightFingers;
            _renderers = renderers;
        }
#endif
    }
}
