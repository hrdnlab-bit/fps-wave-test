using System.Linq;
using HrdnLab.FpsWave.Player.Character;
using HrdnLab.FpsWave.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HrdnLab.FpsWave.Tests
{
    /// <summary>
    /// Posa del personaggio AIRO sul prefab vero: passo, crouch, salto e impugnatura delle armi in terza persona.
    /// Senza i binari AIRO (Assets/ThirdParty/_Own/AIRO, vedi docs/ASSETS.md) i test vengono saltati.
    /// </summary>
    public sealed class CharacterRigTests
    {
        private const string CharacterPrefab = "Assets/_Project/Prefabs/Characters/AIRO_Character.prefab";
        private const string RiflePrefab = "Assets/_Project/Prefabs/Weapons/WM_AssaultRifle.prefab";
        private const string PistolPrefab = "Assets/_Project/Prefabs/Weapons/WM_Pistol.prefab";
        private const float Dt = 1f / 60f;

        private GameObject _root;
        private CharacterRig _rig;
        private Transform _leftFoot, _rightFoot, _hips;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab);
            if (prefab == null || prefab.GetComponentInChildren<SkinnedMeshRenderer>() == null)
                Assert.Ignore("Prefab AIRO assente: esegui FpsWave/Setup/9 con i binari AIRO.");
            _root = Object.Instantiate(prefab);
            _rig = _root.GetComponent<CharacterRig>();
            Assert.IsNotNull(_rig, "CharacterRig mancante sul prefab");
            Assert.IsNotNull(_rig.Settings, "CharacterAnimationSettings non assegnato");
            Transform[] all = _root.GetComponentsInChildren<Transform>(true);
            _leftFoot = all.First(t => t.name.EndsWith("LeftFoot"));
            _rightFoot = all.First(t => t.name.EndsWith("RightFoot"));
            _hips = all.First(t => t.name.EndsWith("Hips"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        private void Run(Vector3 velocity, bool grounded, float crouch, int frames, System.Action perFrame = null,
            Transform right = null, Transform left = null, float pitch = 0f)
        {
            for (int i = 0; i < frames; i++)
            {
                _rig.Solve(new CharacterPoseInput
                {
                    LocalVelocity = velocity, Grounded = grounded, Crouch = crouch, Pitch = pitch,
                    RightHandTarget = right, LeftHandTarget = left, Dt = Dt,
                });
                perFrame?.Invoke();
            }
        }

        [Test]
        public void Idle_FeetStayOnGround()
        {
            Run(Vector3.zero, true, 0f, 30);
            Assert.Less(Mathf.Abs(_leftFoot.position.y - _rightFoot.position.y), 0.02f);
            Assert.Less(_leftFoot.position.y, 0.2f);
        }

        [TestCase(4.5f, 0.45f)]
        [TestCase(7f, 0.9f)]
        public void Walk_FeetAlternateWithStride(float speed, float minSpread)
        {
            float maxSpread = 0f, maxLift = 0f, minY = float.MaxValue;
            float groundY = _leftFoot.position.y;
            bool leftAhead = false, rightAhead = false;
            Run(new Vector3(0f, 0f, speed), true, 0f, 180, () =>
            {
                float dz = _leftFoot.position.z - _rightFoot.position.z;
                maxSpread = Mathf.Max(maxSpread, Mathf.Abs(dz));
                leftAhead |= dz > 0.2f;
                rightAhead |= dz < -0.2f;
                maxLift = Mathf.Max(maxLift, Mathf.Max(_leftFoot.position.y, _rightFoot.position.y) - groundY);
                minY = Mathf.Min(minY, Mathf.Min(_leftFoot.position.y, _rightFoot.position.y));
            });
            Debug.Log($"[CharacterRigTests] {speed} m/s: passo {maxSpread:F2} m, sollevamento {maxLift:F2} m");
            Assert.Greater(maxSpread, minSpread, "passo troppo corto");
            Assert.IsTrue(leftAhead && rightAhead, "i piedi non si alternano");
            Assert.Greater(maxLift, 0.04f, "il piede non si stacca da terra");
            Assert.Greater(minY, groundY - 0.05f, "il piede entra nel pavimento");
        }

        [Test]
        public void Strafe_FeetMoveSideways()
        {
            float maxSpread = 0f;
            Run(new Vector3(4.5f, 0f, 0f), true, 0f, 120,
                () => maxSpread = Mathf.Max(maxSpread, Mathf.Abs(_leftFoot.position.x - _rightFoot.position.x)));
            Run(Vector3.zero, true, 0f, 1);
            float rest = Mathf.Abs(_leftFoot.position.x - _rightFoot.position.x);
            Assert.Greater(maxSpread - rest, 0.25f);
        }

        [Test]
        public void Crouch_DropsHipsAndKeepsFeetDown()
        {
            Run(Vector3.zero, true, 0f, 10);
            float hipsStand = _hips.position.y, footStand = _leftFoot.position.y;
            Run(Vector3.zero, true, 1f, 10);
            Debug.Log($"[CharacterRigTests] crouch: bacino -{hipsStand - _hips.position.y:F2} m");
            Assert.Greater(hipsStand - _hips.position.y, 0.3f, "il bacino non si abbassa");
            Assert.Less(Mathf.Abs(_leftFoot.position.y - footStand), 0.06f, "i piedi si staccano in crouch");
        }

        [Test]
        public void Airborne_LiftsFeet()
        {
            Run(Vector3.zero, true, 0f, 10);
            float ground = _leftFoot.position.y;
            Run(Vector3.zero, false, 0f, 40);
            Assert.Greater(_leftFoot.position.y - ground, 0.1f);
        }

        [TestCase(RiflePrefab, 0f)]
        [TestCase(RiflePrefab, 45f)]
        [TestCase(RiflePrefab, -45f)]
        [TestCase(PistolPrefab, 0f)]
        [TestCase(PistolPrefab, 45f)]
        [TestCase(PistolPrefab, -45f)]
        public void WeaponGrip_HandsReachTargets(string weaponPath, float pitch)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath);
            if (prefab == null)
                Assert.Ignore($"{weaponPath} assente: esegui FpsWave/Setup/9.");
            var weapon = Object.Instantiate(prefab, _root.transform).GetComponent<WorldWeaponModel>();
            Assert.IsNotNull(weapon.GetComponent<MeshFilter>().sharedMesh, "mesh dell'arma mancante");

            // Stesso posizionamento di PlayerCharacter.PlaceWeapon (in piedi, senza rinculo).
            CharacterAnimationSettings s = _rig.Settings;
            Quaternion aim = Quaternion.Euler(pitch, 0f, 0f);
            Vector3 grip = weapon.Grip == CharacterGrip.Pistol ? s.PistolGrip : s.RifleGrip;
            weapon.transform.SetLocalPositionAndRotation(s.AimPivot + aim * grip, aim);

            // La pitch nel rig è positiva verso il basso come in PlayerLook.
            Run(Vector3.zero, true, 0f, 5, null, weapon.RightHand, weapon.LeftHand, pitch);
            Transform[] all = _root.GetComponentsInChildren<Transform>(true);
            Transform rightHand = all.First(t => t.name.EndsWith("RightHand") && t.GetComponentInParent<WorldWeaponModel>() == null);
            Transform leftHand = all.First(t => t.name.EndsWith("LeftHand") && t.GetComponentInParent<WorldWeaponModel>() == null);
            float dr = Vector3.Distance(rightHand.position, weapon.RightHand.position);
            float dl = Vector3.Distance(leftHand.position, weapon.LeftHand.position);
            Debug.Log($"[CharacterRigTests] {prefab.name} pitch {pitch}: mano destra {dr * 100f:F1} cm, sinistra {dl * 100f:F1} cm");
            Assert.Less(dr, 0.03f, "la mano destra non arriva all'impugnatura");
            Assert.Less(dl, 0.03f, "la mano sinistra non arriva al supporto");
        }
    }
}
