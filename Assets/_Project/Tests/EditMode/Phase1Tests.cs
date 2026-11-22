using HrdnLab.FpsWave.Enemies;
using HrdnLab.FpsWave.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace HrdnLab.FpsWave.Tests
{
    public class Phase1Tests
    {
        [Test]
        public void Armor_IntactPlate_LetsOnlyPassThroughToTheBody()
        {
            float body = ArmorMath.BodyDamage(20f, 150f, 0.15f, out float remaining, out bool broke);
            Assert.That(body, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(remaining, Is.EqualTo(130f).Within(1e-4f));
            Assert.That(broke, Is.False);
        }

        [Test]
        public void Armor_BreakingPlate_SendsTheOverflowToTheBody()
        {
            float body = ArmorMath.BodyDamage(30f, 10f, 0.15f, out float remaining, out bool broke);
            Assert.That(broke, Is.True);
            Assert.That(remaining, Is.EqualTo(0f));
            // 15% del colpo + l'85% dei 20 punti oltre la salute della piastra.
            Assert.That(body, Is.EqualTo(30f * 0.15f + 20f * 0.85f).Within(1e-4f));
        }

        [Test]
        public void HitConfirm_ArmorFlags_RoundTrip()
        {
            HitFlags flags = HitConfirm.MakeFlags(false, false, false, true, true);
            Assert.That(HitConfirm.Has(flags, HitFlags.ArmorHit), Is.True);
            Assert.That(HitConfirm.Has(flags, HitFlags.ArmorBreak), Is.True);
            Assert.That(HitConfirm.Has(flags, HitFlags.Kill), Is.False);
            Assert.That((int)flags, Is.LessThan(256)); // resta un byte in rete
        }
    }
}
