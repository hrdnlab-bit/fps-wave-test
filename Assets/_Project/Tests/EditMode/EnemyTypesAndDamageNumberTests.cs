using HrdnLab.FpsWave.Enemies;
using HrdnLab.FpsWave.UI;
using HrdnLab.FpsWave.Weapons;
using NUnit.Framework;

namespace HrdnLab.FpsWave.Tests
{
    public class EnemyTypesAndDamageNumberTests
    {
        // ---------------------------------------------------------------- Scelta del tipo di nemico

        [Test]
        public void PickByWeight_ZeroWeight_IsNeverChosen()
        {
            var weights = new[] { 0f, 2f, 0f };
            for (float r = 0f; r < 1f; r += 0.05f)
                Assert.That(SpawnScoring.PickByWeight(weights, 3, r), Is.EqualTo(1));
        }

        [Test]
        public void PickByWeight_SplitsByWeight()
        {
            var weights = new[] { 3f, 1f };
            Assert.That(SpawnScoring.PickByWeight(weights, 2, 0.70f), Is.EqualTo(0));
            Assert.That(SpawnScoring.PickByWeight(weights, 2, 0.80f), Is.EqualTo(1));
            Assert.That(SpawnScoring.PickByWeight(weights, 2, 1f), Is.EqualTo(1));
        }

        [Test]
        public void PickByWeight_AllZero_ReturnsMinusOne()
        {
            Assert.That(SpawnScoring.PickByWeight(new[] { 0f, 0f }, 2, 0.5f), Is.EqualTo(-1));
        }

        // ---------------------------------------------------------------- Conferma colpo

        [Test]
        public void HitFlags_RoundTrip()
        {
            HitFlags flags = HitConfirm.MakeFlags(critical: true, weakspot: false, kill: true);
            Assert.That(HitConfirm.Has(flags, HitFlags.Critical), Is.True);
            Assert.That(HitConfirm.Has(flags, HitFlags.Weakspot), Is.False);
            Assert.That(HitConfirm.Has(flags, HitFlags.Kill), Is.True);
            Assert.That((HitFlags)(byte)flags, Is.EqualTo(flags));
        }

        [Test]
        public void PackDamage_RoundsAndShowsAtLeastOne()
        {
            Assert.That(HitConfirm.PackDamage(20f), Is.EqualTo(20));
            Assert.That(HitConfirm.PackDamage(39.6f), Is.EqualTo(40));
            Assert.That(HitConfirm.PackDamage(0.2f), Is.EqualTo(1));
            Assert.That(HitConfirm.PackDamage(0f), Is.EqualTo(0));
            Assert.That(HitConfirm.PackDamage(1e9f), Is.EqualTo(ushort.MaxValue));
        }

        // ---------------------------------------------------------------- Animazione dei numeri

        [Test]
        public void DamageNumber_PopsThenSettles()
        {
            Assert.That(DamageNumberMotion.Pop(0f), Is.EqualTo(1.3f).Within(1e-4f));
            Assert.That(DamageNumberMotion.Pop(0.06f), Is.LessThan(1.3f).And.GreaterThan(1f));
            Assert.That(DamageNumberMotion.Pop(0.5f), Is.EqualTo(1f));
        }

        [Test]
        public void DamageNumber_RisesAndFades()
        {
            Assert.That(DamageNumberMotion.Rise(0f), Is.EqualTo(0f));
            Assert.That(DamageNumberMotion.Rise(1f), Is.EqualTo(1f));
            Assert.That(DamageNumberMotion.Rise(0.5f), Is.GreaterThan(0.5f)); // ease-out
            Assert.That(DamageNumberMotion.Alpha(0.3f), Is.EqualTo(1f));
            Assert.That(DamageNumberMotion.Alpha(0.8f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(DamageNumberMotion.Alpha(1f), Is.EqualTo(0f).Within(1e-4f));
        }
    }
}
