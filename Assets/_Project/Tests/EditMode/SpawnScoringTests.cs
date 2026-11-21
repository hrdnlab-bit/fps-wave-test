using HrdnLab.FpsWave.Enemies;
using HrdnLab.FpsWave.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace HrdnLab.FpsWave.Tests
{
    public class SpawnScoringTests
    {
        // ---------------------------------------------------------------- Anello

        [Test]
        public void Ring_InsideBand_IsValid()
        {
            Assert.That(SpawnScoring.IsInRing(40f, 40f, 25f, 60f, 15f), Is.True);
        }

        [Test]
        public void Ring_TooCloseOrTooFar_IsRejected()
        {
            Assert.That(SpawnScoring.IsInRing(20f, 20f, 25f, 60f, 15f), Is.False);
            Assert.That(SpawnScoring.IsInRing(65f, 65f, 25f, 60f, 15f), Is.False);
        }

        [Test]
        public void Ring_AnotherPlayerTooClose_IsRejected()
        {
            // Nell'anello del bersaglio ma a 10 m da un compagno: niente spawn in faccia a nessuno.
            Assert.That(SpawnScoring.IsInRing(40f, 10f, 25f, 60f, 15f), Is.False);
        }

        // ---------------------------------------------------------------- Punteggio

        [Test]
        public void Score_IdealDistance_BeatsEdgeOfRing()
        {
            float ideal = SpawnScoring.Score(40f, 0f, 0f, 40f);
            float edge = SpawnScoring.Score(58f, 0f, 0f, 40f);
            Assert.That(ideal, Is.GreaterThan(edge));
        }

        [Test]
        public void Score_BehindThePlayer_BeatsInFront()
        {
            float behind = SpawnScoring.Score(40f, -1f, 0f, 40f);
            float front = SpawnScoring.Score(40f, 1f, 0f, 40f);
            Assert.That(behind - front, Is.EqualTo(SpawnScoring.FlankWeight).Within(1e-4f));
        }

        [Test]
        public void Score_RecentlyUsedNode_IsPenalized()
        {
            float fresh = SpawnScoring.Score(40f, 0f, SpawnScoring.RecentUsePenalty(60f), 40f);
            float recent = SpawnScoring.Score(40f, 0f, SpawnScoring.RecentUsePenalty(1f), 40f);
            Assert.That(fresh, Is.GreaterThan(recent));
            Assert.That(SpawnScoring.RecentUsePenalty(10f), Is.EqualTo(0f));
        }

        // ---------------------------------------------------------------- Classifica dei migliori 3

        [Test]
        public void Top_KeepsBestThreeInOrder()
        {
            var ids = new int[3];
            var scores = new float[3];
            int count = 0;
            float[] input = { 0.5f, 1.2f, 0.1f, 0.9f, 1.5f };
            for (int i = 0; i < input.Length; i++)
                SpawnScoring.InsertTop(ids, scores, ref count, i, input[i]);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(ids, Is.EqualTo(new[] { 4, 1, 3 }));
            Assert.That(scores[0], Is.EqualTo(1.5f));
            Assert.That(scores[2], Is.EqualTo(0.9f));
        }

        [Test]
        public void Top_RemoveAt_ShiftsTheRest()
        {
            var ids = new[] { 7, 8, 9 };
            var scores = new[] { 3f, 2f, 1f };
            int count = 3;
            SpawnScoring.RemoveTopAt(ids, scores, ref count, 0);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(ids[0], Is.EqualTo(8));
            Assert.That(ids[1], Is.EqualTo(9));
        }

        [Test]
        public void PickWeighted_FollowsTheRoll()
        {
            var scores = new[] { 3f, 1f };
            Assert.That(SpawnScoring.PickWeighted(scores, 2, 0f), Is.EqualTo(0));
            Assert.That(SpawnScoring.PickWeighted(scores, 2, 0.74f), Is.EqualTo(0));
            Assert.That(SpawnScoring.PickWeighted(scores, 2, 0.76f), Is.EqualTo(1));
            Assert.That(SpawnScoring.PickWeighted(scores, 2, 0.9999f), Is.EqualTo(1));
            Assert.That(SpawnScoring.PickWeighted(scores, 0, 0.5f), Is.EqualTo(-1));
        }

        // ---------------------------------------------------------------- Quantità

        [Test]
        public void TargetAlive_ScalesWithPlayers_AndRespectsCap()
        {
            Assert.That(SpawnScoring.TargetAlive(0, 4, 4, 40), Is.EqualTo(0));
            Assert.That(SpawnScoring.TargetAlive(1, 4, 4, 40), Is.EqualTo(8));
            Assert.That(SpawnScoring.TargetAlive(4, 4, 4, 40), Is.EqualTo(20));
            Assert.That(SpawnScoring.TargetAlive(4, 30, 10, 40), Is.EqualTo(40));
        }

        // ---------------------------------------------------------------- Validazione colpi su bersagli in movimento

        [Test]
        public void ShotValidation_MovingTarget_GetsWiderTolerance()
        {
            var claimed = new Vector3(0f, 1f, 0f);
            var serverClosest = new Vector3(2.5f, 1f, 0f); // il Runner (6 m/s) si è spostato di 2.5 m rispetto al client
            Assert.That(ShotValidation.IsTargetPlausible(claimed, serverClosest, 0f, ShotValidation.MovingTargetLagSeconds), Is.False);
            Assert.That(ShotValidation.IsTargetPlausible(claimed, serverClosest, 6f, ShotValidation.MovingTargetLagSeconds), Is.True);
        }
    }
}
