using HrdnLab.FpsWave.Player.Character;
using NUnit.Framework;
using UnityEngine;

namespace HrdnLab.FpsWave.Tests
{
    public sealed class CharacterPoseMathTests
    {
        [TestCase(0f)]
        [TestCase(37.5f)]
        [TestCase(-60f)]
        [TestCase(85f)]
        [TestCase(-85f)]
        public void Pitch_RoundTrip_WithinOneStep(float degrees)
        {
            float back = CharacterPoseMath.UnpackPitch(CharacterPoseMath.PackPitch(degrees));
            Assert.That(back, Is.EqualTo(degrees).Within(CharacterPoseMath.MaxPitch / 127f));
        }

        [Test]
        public void Pitch_IsClamped()
        {
            Assert.That(CharacterPoseMath.PackPitch(500f), Is.EqualTo(127));
            Assert.That(CharacterPoseMath.PackPitch(-500f), Is.EqualTo(-127));
        }

        [Test]
        public void Flags_RoundTrip()
        {
            byte f = CharacterPoseMath.PackFlags(crouching: true, grounded: false);
            Assert.IsTrue(CharacterPoseMath.HasFlag(f, CharacterPoseMath.FlagCrouch));
            Assert.IsFalse(CharacterPoseMath.HasFlag(f, CharacterPoseMath.FlagGrounded));
            f = CharacterPoseMath.PackFlags(crouching: false, grounded: true);
            Assert.IsFalse(CharacterPoseMath.HasFlag(f, CharacterPoseMath.FlagCrouch));
            Assert.IsTrue(CharacterPoseMath.HasFlag(f, CharacterPoseMath.FlagGrounded));
        }

        [Test]
        public void FootCycle_StanceMovesBackOnGround_SwingLifts()
        {
            Vector2 start = CharacterPoseMath.FootCycle(0f, 0.6f);
            Vector2 midStance = CharacterPoseMath.FootCycle(0.3f, 0.6f);
            Vector2 midSwing = CharacterPoseMath.FootCycle(0.8f, 0.6f);
            Assert.That(start.x, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(midStance.y, Is.EqualTo(0f));
            Assert.Less(midStance.x, start.x);
            Assert.Greater(midSwing.y, 0.9f);
        }

        [Test]
        public void FootCycle_IsContinuousAcrossLoop()
        {
            Vector2 end = CharacterPoseMath.FootCycle(0.9999f, 0.6f);
            Vector2 next = CharacterPoseMath.FootCycle(1.0001f, 0.6f);
            Assert.That(end.x, Is.EqualTo(next.x).Within(0.01f));
            Assert.That(end.y, Is.EqualTo(next.y).Within(0.01f));
        }

        [Test]
        public void TwoBoneIk_ReachableTarget_KeepsSegmentLengths()
        {
            Vector3 root = Vector3.zero;
            Vector3 target = new(0f, -0.6f, 0.2f);
            Vector3 mid = CharacterPoseMath.SolveMidJoint(root, target, 0.42f, 0.42f, Vector3.forward, out Vector3 reached);
            Assert.That(Vector3.Distance(root, mid), Is.EqualTo(0.42f).Within(1e-4f));
            Assert.That(Vector3.Distance(mid, reached), Is.EqualTo(0.42f).Within(1e-3f));
            Assert.That(Vector3.Distance(reached, target), Is.LessThan(1e-4f));
            Assert.Greater(mid.z, 0.2f, "il ginocchio si piega verso il suggerimento (avanti)");
        }

        [Test]
        public void TwoBoneIk_UnreachableTarget_IsClampedToReach()
        {
            Vector3 mid = CharacterPoseMath.SolveMidJoint(Vector3.zero, new Vector3(0f, -5f, 0f), 0.4f, 0.4f,
                Vector3.forward, out Vector3 reached);
            Assert.That(reached.magnitude, Is.LessThanOrEqualTo(0.8f));
            Assert.That(Vector3.Distance(mid, reached), Is.EqualTo(0.4f).Within(1e-3f));
        }

        [Test]
        public void StrideLength_GrowsWithSpeed()
        {
            float walk = CharacterPoseMath.StrideLength(4.5f, 0.75f, 1.5f, 4.5f, 7f);
            float run = CharacterPoseMath.StrideLength(7f, 0.75f, 1.5f, 4.5f, 7f);
            Assert.That(walk, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(run, Is.EqualTo(1.5f).Within(1e-4f));
        }
    }
}
