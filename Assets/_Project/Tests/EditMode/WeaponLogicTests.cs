using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Stats;
using HrdnLab.FpsWave.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace HrdnLab.FpsWave.Tests
{
    public class WeaponLogicTests
    {
        private static WeaponState CreateRifle(out WeaponDefinition def)
        {
            def = ScriptableObject.CreateInstance<WeaponDefinition>();
            def.EditorSetup("rifle", "Rifle", WeaponSlot.Primary, damage: 18f, rpm: 600f, automatic: true, magazine: 30,
                reload: 2f, range: 100f, hipSpread: 2f, adsSpread: 0.25f, spreadPerShot: 0.25f, maxBloom: 4f,
                recoilPitch: 1f, recoilYaw: 0.3f, adsFov: 0.75f, adsTime: 0.2f, placeholderSize: Vector3.one);
            return new WeaponState(def, def.CreateStats());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var d in Resources.FindObjectsOfTypeAll<WeaponDefinition>())
                if (string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(d)))
                    Object.DestroyImmediate(d);
        }

        [Test]
        public void Damage_BodyHit_EqualsBase()
        {
            Assert.That(DamageCalculator.Compute(20f, 1f, false, 2f, false, 1.5f), Is.EqualTo(20f));
        }

        [Test]
        public void Damage_WeakspotCrit_Multiplies()
        {
            Assert.That(DamageCalculator.Compute(20f, 1f, true, 2f, true, 1.5f), Is.EqualTo(60f).Within(1e-4f));
        }

        [Test]
        public void Crit_RollBelowChance_IsCrit()
        {
            Assert.That(DamageCalculator.RollCritical(0.1f, 0.05f), Is.True);
            Assert.That(DamageCalculator.RollCritical(0.1f, 0.15f), Is.False);
        }

        [Test]
        public void WeaponState_RespectsFireRate()
        {
            var state = CreateRifle(out _);
            Assert.That(state.ShotInterval, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(state.CanFire(0), Is.True);
            state.RegisterShot(0, 4f);
            Assert.That(state.CanFire(0.05), Is.False);
            Assert.That(state.CanFire(0.1), Is.True);
            Assert.That(state.Ammo, Is.EqualTo(29));
        }

        [Test]
        public void WeaponState_ReloadRefillsAfterReloadTime()
        {
            var state = CreateRifle(out _);
            for (int i = 0; i < 30; i++)
                state.RegisterShot(i, 4f);
            Assert.That(state.Ammo, Is.EqualTo(0));
            Assert.That(state.CanFire(100), Is.False);
            Assert.That(state.StartReload(100), Is.True);
            state.Tick(101, 0.016f, 8f);
            Assert.That(state.IsReloading, Is.True);
            state.Tick(102.01, 0.016f, 8f);
            Assert.That(state.IsReloading, Is.False);
            Assert.That(state.Ammo, Is.EqualTo(30));
        }

        [Test]
        public void WeaponState_ReloadWithFullMagazine_IsRefused()
        {
            var state = CreateRifle(out _);
            Assert.That(state.StartReload(0), Is.False);
        }

        [Test]
        public void WeaponState_ModifierChangesMagazine()
        {
            var state = CreateRifle(out _);
            state.Stats.AddModifier(new StatModifier(StatType.MagazineSize, ModifierOp.AddPercent, 0.5f, this));
            Assert.That(state.MagazineSize, Is.EqualTo(45));
        }

        [Test]
        public void WeaponState_BloomCapsAndRecovers()
        {
            var state = CreateRifle(out _);
            for (int i = 0; i < 25; i++)
                state.RegisterShot(i, 4f);
            Assert.That(state.Bloom, Is.EqualTo(4f).Within(1e-4f));
            state.Tick(30, 1f, 8f);
            Assert.That(state.Bloom, Is.EqualTo(0f));
        }

        [Test]
        public void WeaponState_BloomKeepsGrowingWhileFiring_RecoversAfterAPause()
        {
            var state = CreateRifle(out _);
            float perShot = state.Stats.Get(StatType.SpreadPerShot);
            // Raffica a 10 colpi al secondo: tra un colpo e l'altro il bloom non cala.
            for (int i = 0; i < 4; i++)
            {
                state.RegisterShot(100 + i * 0.1, 10f);
                state.Tick(100 + i * 0.1 + 0.05, 0.05f, 8f);
            }
            Assert.That(state.Bloom, Is.EqualTo(perShot * 4f).Within(1e-4f));
            // Dopo la pausa cala.
            state.Tick(100.3 + WeaponState.BloomRecoveryDelay + 0.01, 0.05f, 8f);
            Assert.That(state.Bloom, Is.LessThan(perShot * 4f));
        }

        [Test]
        public void FireRateLimiter_AllowsBurstButNotSustainedOverrate()
        {
            var limiter = new FireRateLimiter(10f, 3f);
            // Raffica di 3 colpi nello stesso istante (jitter): consentita.
            Assert.That(limiter.TryConsume(0), Is.True);
            Assert.That(limiter.TryConsume(0), Is.True);
            Assert.That(limiter.TryConsume(0), Is.True);
            Assert.That(limiter.TryConsume(0), Is.False);
            // Dopo 0.1 s si recupera un colpo.
            Assert.That(limiter.TryConsume(0.1), Is.True);
            Assert.That(limiter.TryConsume(0.1), Is.False);
        }

        [Test]
        public void FireRateLimiter_SustainedAtRate_AlwaysAllowed()
        {
            var limiter = new FireRateLimiter(10f, 3f);
            for (int i = 0; i < 100; i++)
                Assert.That(limiter.TryConsume(i * 0.1), Is.True, $"colpo {i}");
        }

        [Test]
        public void Validation_OriginTolerance()
        {
            Assert.That(ShotValidation.IsOriginPlausible(new Vector3(0, 1.6f, 0), new Vector3(0.5f, 1.6f, 0)), Is.True);
            Assert.That(ShotValidation.IsOriginPlausible(new Vector3(0, 1.6f, 0), new Vector3(5f, 1.6f, 0)), Is.False);
        }

        [Test]
        public void Validation_Range()
        {
            Assert.That(ShotValidation.IsWithinRange(Vector3.zero, new Vector3(0, 0, 99), 100f), Is.True);
            Assert.That(ShotValidation.IsWithinRange(Vector3.zero, new Vector3(0, 0, 120), 100f), Is.False);
        }

        [Test]
        public void Validation_TargetToleranceGrowsWithSpeedAndLatency()
        {
            var hit = new Vector3(0, 0, 2f);
            Assert.That(ShotValidation.IsTargetPlausible(hit, Vector3.zero, 0f, 0f), Is.False);
            Assert.That(ShotValidation.IsTargetPlausible(hit, Vector3.zero, 8f, 0.15f), Is.True);
        }

        [Test]
        public void SpreadDirection_StaysInsideCone()
        {
            for (int i = 0; i < 50; i++)
            {
                float u = (i * 0.137f) % 1f;
                float v = (i * 0.731f) % 1f;
                Vector3 dir = ShotValidation.SpreadDirection(Vector3.forward, Vector3.up, 3f, u, v);
                Assert.That(Vector3.Angle(Vector3.forward, dir), Is.LessThanOrEqualTo(3.001f));
            }
        }
    }
}
