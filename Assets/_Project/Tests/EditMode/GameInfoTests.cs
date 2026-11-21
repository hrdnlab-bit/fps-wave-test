using HrdnLab.FpsWave.Core;
using NUnit.Framework;

namespace HrdnLab.FpsWave.Tests
{
    public class GameInfoTests
    {
        [Test]
        public void ProtocolVersion_IsPositive()
        {
            Assert.That(GameInfo.ProtocolVersion, Is.GreaterThan(0));
        }

        [Test]
        public void Version_IsSemantic()
        {
            Assert.That(GameInfo.Version, Does.Match(@"^\d+\.\d+\.\d+$"));
        }
    }
}
