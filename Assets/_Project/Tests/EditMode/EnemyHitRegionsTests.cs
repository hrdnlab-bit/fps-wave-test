using HrdnLab.FpsWave.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace HrdnLab.FpsWave.Tests
{
    public class EnemyHitRegionsTests
    {
        [Test]
        public void Classify_Weakspot_IsHead()
        {
            Assert.AreEqual(HitRegion.Head, EnemyHitRegions.Classify(new Vector3(0f, 0.5f, 0f), true));
        }

        [Test]
        public void Classify_ChestCenter_IsBody()
        {
            Assert.AreEqual(HitRegion.Body, EnemyHitRegions.Classify(new Vector3(0.05f, 1.25f, 0.18f), false));
        }

        [Test]
        public void Classify_Legs_BySideAndHeight()
        {
            // -X è la sinistra del nemico (guarda +Z).
            Assert.AreEqual(HitRegion.LeftUpLeg, EnemyHitRegions.Classify(new Vector3(-0.1f, 0.7f, 0f), false));
            Assert.AreEqual(HitRegion.RightLeg, EnemyHitRegions.Classify(new Vector3(0.1f, 0.3f, 0f), false));
        }

        [Test]
        public void Classify_Arms_UpperAndFore()
        {
            Assert.AreEqual(HitRegion.LeftArm, EnemyHitRegions.Classify(new Vector3(-0.25f, 1.3f, 0.05f), false));
            Assert.AreEqual(HitRegion.RightForeArm, EnemyHitRegions.Classify(new Vector3(0.24f, 1.05f, 0.32f), false));
        }

        [Test]
        public void Classify_ScalesWithHeight()
        {
            // Nemico alto 3.6 m: 1.4 m da terra è ancora gamba (0.7 in proporzione).
            Assert.AreEqual(HitRegion.RightUpLeg, EnemyHitRegions.Classify(new Vector3(0.2f, 1.4f, 0f), false, 3.6f));
        }

        [Test]
        public void SeverThreshold_BodyAndHead_NeverSever()
        {
            Assert.IsTrue(float.IsPositiveInfinity(EnemyHitRegions.SeverThreshold(HitRegion.Body)));
            Assert.IsTrue(float.IsPositiveInfinity(EnemyHitRegions.SeverThreshold(HitRegion.Head)));
            Assert.Less(EnemyHitRegions.SeverThreshold(HitRegion.LeftForeArm), EnemyHitRegions.SeverThreshold(HitRegion.LeftUpLeg));
        }

        [Test]
        public void IsLost_ChildOfLostLimb_IsLost()
        {
            ushort mask = 1 << 1; // braccio sinistro
            Assert.IsTrue(EnemyHitRegions.IsLost(mask, 1));
            Assert.IsTrue(EnemyHitRegions.IsLost(mask, 2));  // avambraccio sinistro: va via con il braccio
            Assert.IsFalse(EnemyHitRegions.IsLost(mask, 3)); // braccio destro
            Assert.IsFalse(EnemyHitRegions.IsLost(mask, -1));
        }

        [Test]
        public void IsCrippled_OnlyWithLegs()
        {
            Assert.IsFalse(EnemyHitRegions.IsCrippled((ushort)((1 << 1) | (1 << 0) | (1 << EnemyHitRegions.HeadExplodedBit))));
            Assert.IsTrue(EnemyHitRegions.IsCrippled(1 << 6));
        }

        [Test]
        public void ChooseDeathType_HeadFromBehind_FallsForwardHeadshot()
        {
            Assert.AreEqual(EnemyHitRegions.DeathHeadBack, EnemyHitRegions.ChooseDeathType(HitRegion.Head, Vector3.forward, 0.5f));
            Assert.AreEqual(EnemyHitRegions.DeathHeadFront, EnemyHitRegions.ChooseDeathType(HitRegion.Head, Vector3.back, 0.5f));
        }

        [Test]
        public void ChooseDeathType_Body_FrontBackAndSide()
        {
            Assert.AreEqual(EnemyHitRegions.DeathBack, EnemyHitRegions.ChooseDeathType(HitRegion.Body, Vector3.back, 0.5f));
            Assert.AreEqual(EnemyHitRegions.DeathForward, EnemyHitRegions.ChooseDeathType(HitRegion.Body, Vector3.forward, 0.5f));
            Assert.AreEqual(EnemyHitRegions.DeathRight, EnemyHitRegions.ChooseDeathType(HitRegion.Body, Vector3.right, 0.5f));
            // Varietà: con un tiro basso anche di fronte crolla di lato.
            byte side = EnemyHitRegions.ChooseDeathType(HitRegion.Body, new Vector3(-0.1f, 0f, -1f), 0.1f);
            Assert.AreEqual(EnemyHitRegions.DeathLeft, side);
        }

        [Test]
        public void ChooseDeathType_Legs_UsuallyLegDeath()
        {
            Assert.AreEqual(EnemyHitRegions.DeathLegs, EnemyHitRegions.ChooseDeathType(HitRegion.LeftLeg, Vector3.back, 0.3f));
            Assert.AreEqual(EnemyHitRegions.DeathForward, EnemyHitRegions.ChooseDeathType(HitRegion.LeftLeg, Vector3.back, 0.9f));
        }
    }
}
