using EchoProtocol.Networking.Core;
using NUnit.Framework;

namespace EchoProtocol.Tests
{
    public class LaunchOptionsTests
    {
        [Test]
        public void NoArguments_MeansNoRequest()
        {
            Assert.AreEqual(LaunchMode.None, LaunchOptions.Parse(new string[0], out string address));
            Assert.IsNull(address);
        }

        [Test]
        public void NullArguments_AreSafe()
        {
            Assert.AreEqual(LaunchMode.None, LaunchOptions.Parse(null, out _));
        }

        [Test]
        public void HostFlag_StartsAHost()
        {
            Assert.AreEqual(LaunchMode.Host, LaunchOptions.Parse(new[] { "game.exe", "-echo-host" }, out _));
        }

        [Test]
        public void JoinFlag_WithoutAddress_JoinsLocalhost()
        {
            Assert.AreEqual(LaunchMode.Join, LaunchOptions.Parse(new[] { "game.exe", "-echo-join" }, out string address));
            Assert.IsNull(address);
        }

        [Test]
        public void JoinFlag_TakesTheAddressThatFollowsIt()
        {
            Assert.AreEqual(LaunchMode.Join, LaunchOptions.Parse(new[] { "-echo-join", "192.168.1.20" }, out string address));
            Assert.AreEqual("192.168.1.20", address);
        }

        [Test]
        public void JoinFlag_DoesNotSwallowTheNextFlagAsAnAddress()
        {
            Assert.AreEqual(LaunchMode.Join, LaunchOptions.Parse(new[] { "-echo-join", "-screen-width", "960" }, out string address));
            Assert.IsNull(address);
        }

        [Test]
        public void HostWinsWhenBothFlagsArePresent()
        {
            Assert.AreEqual(LaunchMode.Host, LaunchOptions.Parse(new[] { "-echo-join", "-echo-host" }, out _));
        }

        [Test]
        public void NoSteamFlag_DisablesSteam_AndOnlyThatFlagDoes()
        {
            Assert.IsTrue(LaunchOptions.SteamDisabled(new[] { "game.exe", "-echo-no-steam" }));
            Assert.IsTrue(LaunchOptions.SteamDisabled(new[] { "-ECHO-NO-STEAM" }), "case insensitive");
            Assert.IsFalse(LaunchOptions.SteamDisabled(new[] { "-echo-host" }));
            Assert.IsFalse(LaunchOptions.SteamDisabled(null));
            Assert.IsFalse(LaunchOptions.SteamDisabled(new string[0]));
        }

        [Test]
        public void PhotoFlag_ImpliesHosting_ButNeverOverridesAnExplicitMode()
        {
            Assert.AreEqual(LaunchMode.Host, LaunchOptions.Parse(new[] { "-echo-photo" }, out _), "the photo mode needs a running match");
            Assert.AreEqual(LaunchMode.Host, LaunchOptions.Parse(new[] { "-echo-photo", "C:\\shots" }, out _));
            Assert.AreEqual(LaunchMode.Join, LaunchOptions.Parse(new[] { "-echo-photo", "-echo-join" }, out _), "an explicit join wins");
        }

        [Test]
        public void FlagsAreCaseInsensitive()
        {
            Assert.AreEqual(LaunchMode.Host, LaunchOptions.Parse(new[] { "-ECHO-HOST" }, out _));
        }
    }
}
