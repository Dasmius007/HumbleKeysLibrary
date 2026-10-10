using System;
using HumbleKeys.Models;
using Playnite.SDK.Models;

namespace HumbleKeys.Extensions
{
    public static class TpkExtensions
    {
        public const string SteamGameUrlMask = @"https://store.steampowered.com/app/{0}";
        public const string SteamSearchUrlMask = @"https://store.steampowered.com/search/?term={0}";

        public static Link MakeSteamLink(string gameKey) => new Link("Steam", string.Format(SteamGameUrlMask, gameKey));

        public static Link MakeSteamLink(this Order.TpkdDict.Tpk tpk)
        {
            if (!string.IsNullOrEmpty(tpk.steam_app_id)) { return MakeSteamLink(tpk.steam_app_id); }
                
            string humanName = tpk.human_name;
            
            if (string.IsNullOrEmpty(humanName))
            {
                humanName = tpk.machine_name?.Replace("_steam", string.Empty).Replace("_choice", string.Empty);
            }

            if (string.IsNullOrEmpty(humanName)) return null;
            if (humanName.EndsWith(" Steam"))
            {
                humanName = humanName.Remove(humanName.LastIndexOf(" Steam", StringComparison.Ordinal));
            }
            if (humanName.EndsWith(" DLC"))
            {
                humanName = humanName.Remove(humanName.LastIndexOf(" DLC", StringComparison.Ordinal));
            }

            return new Link("Steam", string.Format(SteamSearchUrlMask, humanName.Replace(" ", "%2B")));
        }

    }
}