using HrdnLab.FpsWave.Net;
using NUnit.Framework;

namespace HrdnLab.FpsWave.Tests
{
    public class EditorInstanceInfoTests
    {
        [Test]
        public void MainProject_IsNotVirtual()
        {
            Assert.That(EditorInstanceInfo.IsVirtualPlayerPath(
                "C:/Users/manue/FpsWaveTest/Assets", @"C:\Users\manue\FpsWaveTest"), Is.False);
        }

        [Test]
        public void ClonePath_IsVirtual_WithForwardSlashes()
        {
            Assert.That(EditorInstanceInfo.IsVirtualPlayerPath(
                "C:/Users/manue/FpsWaveTest/Library/VP/mppm8fee3b36/Assets"), Is.True);
        }

        [Test]
        public void CloneWorkingDirectory_IsVirtual_EvenIfAssetsResolvesToMain()
        {
            // Assets nel clone è un symlink: dataPath può puntare al progetto principale.
            Assert.That(EditorInstanceInfo.IsVirtualPlayerPath(
                "C:/Users/manue/FpsWaveTest/Assets", @"C:\Users\manue\FpsWaveTest\Library\VP\mppm8fee3b36"), Is.True);
        }

        [Test]
        public void NullOrEmpty_IsNotVirtual()
        {
            Assert.That(EditorInstanceInfo.IsVirtualPlayerPath(null), Is.False);
            Assert.That(EditorInstanceInfo.IsVirtualPlayerPath("", null), Is.False);
        }
    }
}
