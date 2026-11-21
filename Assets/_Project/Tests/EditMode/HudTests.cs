using HrdnLab.FpsWave.UI;
using NUnit.Framework;

namespace HrdnLab.FpsWave.Tests
{
    public class HudTests
    {
        [Test]
        public void Segments_FullAndEmpty()
        {
            Assert.That(HudMath.LitSegments(1f, 20), Is.EqualTo(20));
            Assert.That(HudMath.LitSegments(0f, 20), Is.EqualTo(0));
        }

        [Test]
        public void Segments_AnyHealthKeepsOneLit()
        {
            Assert.That(HudMath.LitSegments(0.001f, 20), Is.EqualTo(1));
            Assert.That(HudMath.LitSegments(0.5f, 20), Is.EqualTo(10));
            Assert.That(HudMath.LitSegments(0.51f, 20), Is.EqualTo(11));
        }

        [Test]
        public void Spread_ZeroIsCenter_AndGrowsWithAngle()
        {
            Assert.That(HudMath.SpreadToPixels(0f, 90f, 1080f), Is.EqualTo(0f));
            // A 90° di FOV verticale, 45° di spread arriva al bordo: metà schermo.
            Assert.That(HudMath.SpreadToPixels(45f, 90f, 1080f), Is.EqualTo(540f).Within(0.5f));
            Assert.That(HudMath.SpreadToPixels(2f, 90f, 1080f), Is.GreaterThan(HudMath.SpreadToPixels(1f, 90f, 1080f)));
        }
    }
}
