using HrdnLab.FpsWave.Stats;
using NUnit.Framework;

namespace HrdnLab.FpsWave.Tests
{
    public class StatTests
    {
        private static readonly object SourceA = new();
        private static readonly object SourceB = new();

        [Test]
        public void NoModifiers_ReturnsBase()
        {
            var stat = new Stat(StatType.Damage, 20f);
            Assert.That(stat.Value, Is.EqualTo(20f));
        }

        [Test]
        public void Formula_AddThenPercentThenMultiply()
        {
            var stat = new Stat(StatType.Damage, 20f);
            stat.AddModifier(new StatModifier(StatType.Damage, ModifierOp.Add, 5f, SourceA));          // 25
            stat.AddModifier(new StatModifier(StatType.Damage, ModifierOp.AddPercent, 0.25f, SourceA)); // +25%
            stat.AddModifier(new StatModifier(StatType.Damage, ModifierOp.AddPercent, 0.15f, SourceB)); // +15% -> x1.4 = 35
            stat.AddModifier(new StatModifier(StatType.Damage, ModifierOp.Multiply, 2f, SourceB));      // x2 = 70
            Assert.That(stat.Value, Is.EqualTo(70f).Within(1e-4f));
        }

        [Test]
        public void PercentagesStackAdditively_NotMultiplicatively()
        {
            var stat = new Stat(StatType.Damage, 100f);
            stat.AddModifier(new StatModifier(StatType.Damage, ModifierOp.AddPercent, 0.5f, SourceA));
            stat.AddModifier(new StatModifier(StatType.Damage, ModifierOp.AddPercent, 0.5f, SourceA));
            Assert.That(stat.Value, Is.EqualTo(200f).Within(1e-4f));
        }

        [Test]
        public void DebuffPercent_ReducesValue()
        {
            var stat = new Stat(StatType.HipSpread, 4f, min: 0f);
            stat.AddModifier(new StatModifier(StatType.HipSpread, ModifierOp.AddPercent, -0.2f, SourceA));
            Assert.That(stat.Value, Is.EqualTo(3.2f).Within(1e-4f));
        }

        [Test]
        public void Clamp_AppliesMinAndMax()
        {
            var stat = new Stat(StatType.HipSpread, 2f, min: 0f, max: 10f);
            stat.AddModifier(new StatModifier(StatType.HipSpread, ModifierOp.AddPercent, -5f, SourceA));
            Assert.That(stat.Value, Is.EqualTo(0f));
            stat.ClearModifiers();
            stat.AddModifier(new StatModifier(StatType.HipSpread, ModifierOp.Multiply, 100f, SourceA));
            Assert.That(stat.Value, Is.EqualTo(10f));
        }

        [Test]
        public void RemoveModifiersFromSource_OnlyRemovesThatSource()
        {
            var stat = new Stat(StatType.Damage, 10f);
            stat.AddModifier(new StatModifier(StatType.Damage, ModifierOp.Add, 5f, SourceA));
            stat.AddModifier(new StatModifier(StatType.Damage, ModifierOp.Add, 3f, SourceB));
            Assert.That(stat.RemoveModifiersFromSource(SourceA), Is.EqualTo(1));
            Assert.That(stat.Value, Is.EqualTo(13f));
        }

        [Test]
        public void BaseValueChange_InvalidatesCache()
        {
            var stat = new Stat(StatType.Damage, 10f);
            stat.AddModifier(new StatModifier(StatType.Damage, ModifierOp.Multiply, 2f, SourceA));
            Assert.That(stat.Value, Is.EqualTo(20f));
            stat.BaseValue = 15f;
            Assert.That(stat.Value, Is.EqualTo(30f));
        }

        [Test]
        public void StatBlock_IgnoresModifiersForMissingStats()
        {
            var block = new StatBlock();
            block.Define(StatType.Damage, 10f);
            Assert.That(block.AddModifier(new StatModifier(StatType.MoveSpeed, ModifierOp.Add, 1f, SourceA)), Is.False);
            Assert.That(block.Get(StatType.MoveSpeed, -1f), Is.EqualTo(-1f));
            Assert.That(block.AddModifier(new StatModifier(StatType.Damage, ModifierOp.Add, 1f, SourceA)), Is.True);
            Assert.That(block.Get(StatType.Damage), Is.EqualTo(11f));
        }

        [Test]
        public void StatBlock_VersionChangesWhenModifiersChange()
        {
            var block = new StatBlock();
            block.Define(StatType.Damage, 10f);
            int v = block.Version;
            block.AddModifier(new StatModifier(StatType.Damage, ModifierOp.Add, 1f, SourceA));
            Assert.That(block.Version, Is.GreaterThan(v));
            v = block.Version;
            block.RemoveModifiersFromSource(SourceA);
            Assert.That(block.Version, Is.GreaterThan(v));
        }
    }
}
