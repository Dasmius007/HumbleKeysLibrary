using HumbleKeys;
using HumbleKeys.Extensions;
using HumbleKeys.Models;
using NSubstitute;
using NUnit.Framework;
using Playnite.SDK;

namespace HumbleKeysLibraryTest
{
    [TestFixture]
    public class ExtensionTests
    {
        [TestCase]
        public void EmptyTpk()
        {
            var tpk = new Order.TpkdDict.Tpk();
            var link = tpk.MakeSteamLink();
            Assert.That(link, Is.Null);
        }

        [TestCase]
        public void EmptyHumanNameCreatesValidSteamLinkViaMachineName()
        {
            var tpk = new Order.TpkdDict.Tpk {machine_name = "flowersanddeities_choice_steam", human_name = string.Empty};
            var link = tpk.MakeSteamLink();
            Assert.That(link, Is.Not.Null);
            Assert.That(link.Url, Is.EqualTo(string.Format(TpkExtensions.SteamSearchUrlMask, "flowersanddeities")));
        }
        
        [TestCase]
        public void HumanNameCreatesValidSteamLinkViaHumanName()
        {
            var tpk = new Order.TpkdDict.Tpk {machine_name = "flowersanddeities_choice_steam", human_name = "Flowers and Deities"};
            var link = tpk.MakeSteamLink();
            Assert.That(link, Is.Not.Null);
            Assert.That(link.Url, Is.EqualTo(string.Format(TpkExtensions.SteamSearchUrlMask, "Flowers%2Band%2BDeities")));
        }
        
        [TestCase]
        public void HumanNameDLCCreatesValidSteamLinkViaHumanName()
        {
            var tpk = new Order.TpkdDict.Tpk {machine_name = "flowersanddeities_choice_steam", human_name = "Flowers and Deities DLC"};
            var link = tpk.MakeSteamLink();
            Assert.That(link, Is.Not.Null);
            Assert.That(link.Url, Is.EqualTo(string.Format(TpkExtensions.SteamSearchUrlMask, "Flowers%2Band%2BDeities")));
        }
        
        [TestCase]
        public void HumanNameSteamCreatesValidSteamLinkViaHumanName()
        {
            var tpk = new Order.TpkdDict.Tpk {machine_name = "flowersanddeities_choice_steam", human_name = "Flowers and Deities Steam"};
            var link = tpk.MakeSteamLink();
            Assert.That(link, Is.Not.Null);
            Assert.That(link.Url, Is.EqualTo(string.Format(TpkExtensions.SteamSearchUrlMask, "Flowers%2Band%2BDeities")));
        }

        [TestCase]
        public void AppIdCreatesValidSteamLink()
        {
            var tpk = new Order.TpkdDict.Tpk {steam_app_id = "3816000", machine_name = "flowersanddeities_choice_steam", human_name = "Flowers and Deities"};
            var link = tpk.MakeSteamLink();
            Assert.That(link, Is.Not.Null);
            Assert.That(link.Url, Is.EqualTo(string.Format(TpkExtensions.SteamGameUrlMask, "3816000")));
        }
    }
}