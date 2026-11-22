using HrdnLab.FpsWave.Audio;
using HrdnLab.FpsWave.Editor;
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

        [Test]
        public void ShotEdit_TrimsFromTheAttack_ResamplesAndNormalizes()
        {
            // 0.5 s di silenzio, poi uno "sparo" che decade: a 96 kHz stereo.
            const int rate = 96000;
            var left = new float[rate * 2];
            var right = new float[rate * 2];
            for (int i = rate / 2; i < left.Length; i++)
            {
                float t = (i - rate / 2) / (float)rate;
                left[i] = Mathf.Exp(-t * 8f) * 0.4f * Mathf.Sin(i * 0.3f);
                right[i] = left[i] * 0.8f;
            }
            float[][] shot = ShotEdit.Prepare(new[] { left, right }, rate, 48000, 1f, mono: false);
            Assert.That(shot.Length, Is.EqualTo(2));
            Assert.That(shot[0].Length, Is.EqualTo(48000));
            float peak = 0f;
            int firstLoud = -1;
            for (int i = 0; i < shot[0].Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(shot[0][i]));
                if (firstLoud < 0 && Mathf.Abs(shot[0][i]) > 0.05f)
                    firstLoud = i;
            }
            Assert.That(peak, Is.EqualTo(0.95f).Within(0.01f));
            Assert.That(firstLoud, Is.LessThan(48000 / 50)); // l'attacco è nei primi 20 ms, il silenzio iniziale è sparito
            Assert.That(Mathf.Abs(shot[0][shot[0].Length - 1]), Is.LessThan(0.01f)); // sfumato alla fine
        }

        [Test]
        public void ShotEdit_Mono_AveragesChannels()
        {
            var a = new float[1000];
            var b = new float[1000];
            for (int i = 0; i < 1000; i++) { a[i] = 0.5f; b[i] = -0.5f; }
            a[0] = 0.9f; b[0] = 0.9f;
            float[][] shot = ShotEdit.Prepare(new[] { a, b }, 1000, 1000, 0.5f, mono: true);
            Assert.That(shot.Length, Is.EqualTo(1));
            Assert.That(shot[0][10], Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void SoundLibrary_FindsCuesById()
        {
            var library = ScriptableObject.CreateInstance<SoundLibrary>();
            library.EditorSetCues(new[] { new SoundCue { Id = SoundId.Ping }, new SoundCue { Id = SoundId.KillConfirm } });
            Assert.That(library.Get(SoundId.Ping).Id, Is.EqualTo(SoundId.Ping));
            Assert.That(library.Get(SoundId.KillConfirm).Id, Is.EqualTo(SoundId.KillConfirm));
            Assert.That(library.Get(SoundId.Footstep), Is.Null);
            Object.DestroyImmediate(library);
        }
    }
}
