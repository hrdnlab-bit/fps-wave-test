using HrdnLab.FpsWave.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace HrdnLab.FpsWave.Tests
{
    public class WeaponFeelTests
    {
        [Test]
        public void FirstShot_KicksHarder_ThenRampsUp()
        {
            var feel = new WeaponFeel { FirstShotMultiplier = 1.3f, RampShots = 10, RampMultiplier = 1.5f };
            Assert.That(RecoilMath.VerticalScale(feel, 0), Is.EqualTo(1.3f).Within(1e-4f));
            Assert.That(RecoilMath.VerticalScale(feel, 1), Is.EqualTo(1.05f).Within(1e-4f));
            Assert.That(RecoilMath.VerticalScale(feel, 10), Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(RecoilMath.VerticalScale(feel, 50), Is.EqualTo(1.5f).Within(1e-4f));
        }

        [Test]
        public void Kick_HipfireBarelyClimbs_AdsIsFull_CrouchReduces()
        {
            var feel = new WeaponFeel { HipRecoilMultiplier = 0.2f, AdsMultiplier = 1f, CrouchMultiplier = 0.8f, HorizontalRandomness = 0f, PatternStrength = 0f, VerticalMultiplier = 1f };
            Vector2 hip = RecoilMath.Kick(feel, 1f, 0.3f, 3, 42, 0f, false, 0.5f);
            Vector2 ads = RecoilMath.Kick(feel, 1f, 0.3f, 3, 42, 1f, false, 0.5f);
            Vector2 adsCrouch = RecoilMath.Kick(feel, 1f, 0.3f, 3, 42, 1f, true, 0.5f);
            Assert.That(hip.x, Is.EqualTo(ads.x * 0.2f).Within(1e-4f));
            Assert.That(adsCrouch.x, Is.EqualTo(ads.x * 0.8f).Within(1e-4f));
        }

        [Test]
        public void VerticalRandomness_VariesTheClimbWithinItsRange()
        {
            var feel = new WeaponFeel { VerticalMultiplier = 1f, FirstShotMultiplier = 1f, VerticalRandomness = 0.2f };
            float low = RecoilMath.Kick(feel, 1f, 0f, 0, 1, 1f, false, 0.5f, 0f).x;
            float mid = RecoilMath.Kick(feel, 1f, 0f, 0, 1, 1f, false, 0.5f, 0.5f).x;
            float high = RecoilMath.Kick(feel, 1f, 0f, 0, 1, 1f, false, 0.5f, 1f).x;
            Assert.That(low, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(mid, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(high, Is.EqualTo(1.2f).Within(1e-4f));
        }

        [Test]
        public void AutomaticPreset_TapsKickLessThanTheBurst()
        {
            WeaponFeel feel = WeaponFeel.AutomaticPreset();
            Assert.That(RecoilMath.VerticalScale(feel, 0), Is.LessThan(RecoilMath.VerticalScale(feel, 3)));
        }

        [Test]
        public void Presets_PartialRecoilReset_LowHipClimb_AndAViewModelThatBarelyMoves()
        {
            foreach (WeaponFeel feel in new[] { WeaponFeel.AutomaticPreset(), WeaponFeel.SemiAutoPreset() })
            {
                // Stile Call of Duty moderno: parte della salita resta (si compensa col mouse), parte torna da sola lasciando il grilletto.
                Assert.That(feel.PermanentFraction, Is.InRange(0.3f, 0.7f));
                // Da fianco la mira quasi non sale: si paga in dispersione.
                Assert.That(feel.HipRecoilMultiplier, Is.LessThan(0.25f));
                // L'arma in mano non deve coprire il bersaglio: calcio verso l'alto di pochi gradi.
                Assert.That(Mathf.Abs(feel.KickRotation.x * feel.ViewKickScale), Is.LessThan(3f));
            }
        }

        [Test]
        public void VerticalMultiplier_ScalesTheClimb()
        {
            var weak = new WeaponFeel { VerticalMultiplier = 1f, FirstShotMultiplier = 1f };
            var strong = new WeaponFeel { VerticalMultiplier = 2f, FirstShotMultiplier = 1f };
            float a = RecoilMath.Kick(weak, 1f, 0f, 0, 1, 0f, false, 0.5f).x;
            float b = RecoilMath.Kick(strong, 1f, 0f, 0, 1, 0f, false, 0.5f).x;
            Assert.That(b, Is.EqualTo(a * 2f).Within(1e-4f));
        }

        [Test]
        public void Pattern_IsRepeatableForTheSameWeapon_AndBounded()
        {
            int seed = RecoilMath.StableSeed("rifle_ar");
            Assert.That(seed, Is.EqualTo(RecoilMath.StableSeed("rifle_ar")));
            Assert.That(seed, Is.Not.EqualTo(RecoilMath.StableSeed("pistol_base")));
            for (int i = 0; i < 40; i++)
            {
                float v = RecoilMath.PatternValue(i, seed);
                Assert.That(v, Is.InRange(-1f, 1f));
                Assert.That(v, Is.EqualTo(RecoilMath.PatternValue(i, seed)));
            }
        }

        [Test]
        public void Spring_SettlesBackToRest()
        {
            var spring = new Spring3();
            spring.AddImpulse(new Vector3(0f, 0f, -1f));
            for (int i = 0; i < 120; i++)
                spring.Update(1f / 60f, 190f, 17f);
            Assert.That(spring.Value.magnitude, Is.LessThan(1e-3f));
        }

        [Test]
        public void Spring_ImpulseTimesOmega_PeaksNearTheRequestedKick()
        {
            // L'impulso usato dal driver è kick * sqrt(k): il picco deve avvicinarsi al kick (meno per lo smorzamento).
            const float k = 190f, c = 17f, kick = 0.05f;
            var spring = new Spring3();
            spring.AddImpulse(new Vector3(0f, 0f, kick * Mathf.Sqrt(k)));
            float peak = 0f;
            for (int i = 0; i < 60; i++)
            {
                spring.Update(1f / 120f, k, c);
                peak = Mathf.Max(peak, spring.Value.z);
            }
            Assert.That(peak, Is.InRange(kick * 0.4f, kick * 1.05f));
        }

        [Test]
        public void Spring_IsStableWithAVerySlowFrame()
        {
            var spring = new Spring3();
            spring.AddImpulse(Vector3.one);
            spring.Update(0.5f, 400f, 10f); // frame lentissimo: niente esplosione numerica
            Assert.That(float.IsNaN(spring.Value.x), Is.False);
            Assert.That(spring.Value.magnitude, Is.LessThan(1f));
        }
    }
}
