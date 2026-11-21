using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Net;
using NUnit.Framework;

namespace HrdnLab.FpsWave.Tests
{
    public class LobbyCompatibilityTests
    {
        [Test]
        public void SameProtocol_IsCompatible()
        {
            Assert.That(LobbyCompatibility.IsCompatible(GameInfo.ProtocolVersion.ToString(), GameInfo.ProtocolVersion), Is.True);
        }

        [Test]
        public void DifferentProtocol_IsRejected()
        {
            Assert.That(LobbyCompatibility.IsCompatible("999", 1), Is.False);
        }

        [Test]
        public void MissingOrGarbageProtocol_IsRejected()
        {
            Assert.That(LobbyCompatibility.IsCompatible("", 1), Is.False);
            Assert.That(LobbyCompatibility.IsCompatible(null, 1), Is.False);
            Assert.That(LobbyCompatibility.IsCompatible("abc", 1), Is.False);
        }
    }
}
