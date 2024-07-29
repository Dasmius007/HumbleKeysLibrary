using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using HumbleKeys.Models;
using HumbleKeys.Services;

namespace HumbleKeys
{
    [LoadPlugin]
    public class HumbleKeysLibrary : LibraryPlugin
    {
        #region === Constants ================
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string dbImportMessageId = "humblekeyslibImportError";
        private const string humblePurchaseUrlMask = @"https://www.humblebundle.com/downloads?key={0}";
        const string steamGameUrlMask = @"https://store.steampowered.com/app/{0}";
        const string steamSearchUrlMask = @"https://store.steampowered.com/search/?term={0}";
        private const string REDEEMED_STR = "Key: Redeemed";
        private const string UNREDEEMED_STR = "Key: Unredeemed";
        private static readonly string[] PAST_TAGS = { REDEEMED_STR, UNREDEEMED_STR, "Redeemed", "Unredeemed", };
        private const string HUMBLE_KEYS_SRC_NAME = "Humble Keys";
        private const string HUMBLE_KEYS_PLATFORM_NAME = "Humble Key: ";
        #endregion

        #region === Accessors ================
        private HumbleKeysLibrarySettings Settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("62ac4052-e08a-4a1a-b70a-c2c0c3673bb9");
        public override string Name => "Humble Keys Library";

        // Implementing Client adds ability to open it via special menu in Playnite.
        public override LibraryClient Client { get; } = new HumbleKeysLibraryClient();
        #endregion

        public HumbleKeysLibrary(IPlayniteAPI api) : base(api)
        {
            Properties = new LibraryPluginProperties { CanShutdownClient = false, HasCustomizedGameImport = true };
            Settings = new HumbleKeysLibrarySettings(this);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new HumbleKeysLibrarySettingsView();
        }

        public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args)
        {
            var importedGames = new List<Game>();
            Exception importError = null;

            if (!Settings.ConnectAccount) { return importedGames; }

            try
            {
                var orders = ScrapeOrders();
                var selectedTpkds = SelectTpkds(orders);
                TpkdsIntake(orders, selectedTpkds, ref importedGames);
            }
            catch (Exception e)
            {
                importError = e;
                logger.Error($"Humble Keys Library: error {e}");
            }

            if (importError != null)
            {
                logger.Error($"Humble Keys Library: importError {dbImportMessageId}");
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    dbImportMessageId,
                    string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                    System.Environment.NewLine + importError.Message,
                    NotificationType.Error,
                    () => OpenSettingsView()));
            }
            else
            {
                PlayniteApi.Notifications.Remove(dbImportMessageId);
            }

            return importedGames;
        }

        public Dictionary<string, Order> ScrapeOrders()
        {
            Dictionary<string, Order> orders;
            using (var view = PlayniteApi.WebViews.CreateOffscreenView(
                       new WebViewSettings { JavaScriptEnabled = false }))
            {
                var api = new Services.HumbleKeysAccountClient(view,
                    new HumbleKeysAccountClientSettings
                    {
                        CacheEnabled = Settings.CacheEnabled,
                        CachePath = $"{PlayniteApi.Paths.ExtensionsDataPath}\\{Id}"
                    });
                var keys = api.GetLibraryKeys();
                orders = api.GetOrders(keys, Settings.ImportChoiceKeys);
            }

            return orders;
        }

        public IEnumerable<IGrouping<string, Order.TpkdDict.Tpk>> SelectTpkds(Dictionary<string,Order> orders)
        {
            return orders.Select(kv => kv.Value)
                .SelectMany(a => a.tpkd_dict?.all_tpks)
                .Where(t => t != null
                            && Settings.keyTypeWhitelist.Contains(t.key_type)
                            && !string.IsNullOrWhiteSpace(t.gamekey)
                ).GroupBy(tpk => tpk.gamekey);
        }

        protected void TpkdsIntake(Dictionary<string,Order> orders, IEnumerable<IGrouping<string, Order.TpkdDict.Tpk>> tpkds, ref List<Game> importedGames)
        {
            foreach (var tpkdGroup in tpkds)
            {
                var tpkdGroupEntries = tpkdGroup.AsEnumerable();
                Tag groupTag = null;
                var groupEntries = tpkdGroupEntries.ToList();
                if (Settings.ImportChoiceKeys && Settings.CurrentTagMethodology != "none" && groupEntries.Count() > 1)
                {
                    var isHumbleMonthly = orders[tpkdGroup.Key].product.human_name.Contains("Humble Monthly");
                    if (Settings.CurrentTagMethodology == "all" || Settings.CurrentTagMethodology == "monthly" && isHumbleMonthly)
                    {
                        groupTag = PlayniteApi.Database.Tags.Add($"Bundle: {orders[tpkdGroup.Key].product.human_name}");
                    }
                }
                
                foreach (var tpkd in groupEntries)
                {
                    var gameId = GetGameId(tpkd);

                    var alreadyImported = 
                        PlayniteApi.Database.Games.FirstOrDefault(
                            game => game.GameId == GetGameId(tpkd) && game.PluginId == Id);

                    if (alreadyImported == null)
                    {
                        if (!Settings.IgnoreRedeemedKeys || (Settings.IgnoreRedeemedKeys && !IsKeyPresent(tpkd)))
                        {
                            importedGames.Add(ImportNewGame(tpkd, groupTag));
                        }
                    }
                    else
                    {
                        if (!Settings.IgnoreRedeemedKeys || (Settings.IgnoreRedeemedKeys && !IsKeyPresent(tpkd)))
                        {
                            var tagsUpdated = UpdateRedemptionStatus(alreadyImported, tpkd, groupTag);
                            var linksUpdated = UpdateStoreLinks(alreadyImported.Links, tpkd);
                            if (!tagsUpdated && !linksUpdated) continue;
                            
                            PlayniteApi.Database.BeginBufferUpdate();
                            PlayniteApi.Database.Games.Update(alreadyImported);
                            PlayniteApi.Database.EndBufferUpdate();
                            PlayniteApi.Notifications.Add(
                                new NotificationMessage("HumbleKeysLibraryUpdate",
                                    $"Tags Updated for {alreadyImported.Name}", NotificationType.Info,
                                    () =>
                                    {
                                        if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen) return;
                                        PlayniteApi.MainView.SelectGame(alreadyImported.Id);
                                    })
                            );
                        }
                        else
                        {
                            // Remove Existing Game?
                            PlayniteApi.Database.Games.Remove(alreadyImported);
                            logger.Trace(
                                $"Removing game {alreadyImported.Name} since Settings.IgnoreRedeemedKeys is: [{Settings.IgnoreRedeemedKeys}] and IsKeyPresent() is [{IsKeyPresent(tpkd)}]");
                        }
                    }
                }
            }
        }

        bool UpdateStoreLinks(ObservableCollection<Link> links, Order.TpkdDict.Tpk tpkd)
        {
            if (tpkd.key_type != "steam") return false;

            Link steamGameLink;
            string humanName = string.Empty;
            if (!string.IsNullOrEmpty(tpkd.steam_app_id))
            {
                steamGameLink = MakeSteamLink(tpkd.steam_app_id);
            }
            else
            {
                humanName = tpkd.human_name;
                steamGameLink = new Link("Steam", string.Format(steamSearchUrlMask, humanName.Replace(" ", "%2B")));
            }

            if (humanName.EndsWith(" Steam"))
            {
                humanName = humanName.Remove(humanName.LastIndexOf(" Steam", StringComparison.Ordinal));
            }
            if (humanName.EndsWith(" DLC"))
            {
                humanName = humanName.Remove(humanName.LastIndexOf(" DLC", StringComparison.Ordinal));
            }
            var steamLinks = links.Where((link1, i) => link1.Name == "Steam");
            var steamLinksList = steamLinks.ToList();
            var existingSteamLink = steamLinksList.FirstOrDefault();
            if (existingSteamLink == null)
            {
                links.Add(steamGameLink);
                return true;
            }

            if (!string.IsNullOrEmpty(tpkd.steam_app_id) && existingSteamLink.Url == steamGameLink.Url) return false;

            // steam link url doesn't match expected value
            if (existingSteamLink.Url != steamGameLink.Url)
            {
                existingSteamLink.Url = steamGameLink.Url;
            }
            else
            {
                return false;
            }

            return true;
        }

        Game ImportNewGame(Order.TpkdDict.Tpk tpkd, Tag groupTag = null)
        {
            var gameInfo = new GameMetadata()
            {
                Name = tpkd.human_name,
                GameId = GetGameId(tpkd),
                Source = new MetadataNameProperty(HUMBLE_KEYS_SRC_NAME),
                Platforms = new HashSet<MetadataProperty> { new MetadataNameProperty(
                        HUMBLE_KEYS_PLATFORM_NAME + tpkd.key_type) },
                Tags = new HashSet<MetadataProperty>(),
                Links = new List<Link>(),
            };
            
            // add tag reflecting redemption status
            gameInfo.Tags.Add(new MetadataNameProperty(GetRedeemedTag(tpkd)));
            
            if (!string.IsNullOrWhiteSpace(tpkd?.gamekey))
            {
                gameInfo.Links.Add(MakeLink(tpkd?.gamekey));
            }
            // adds link to humble purchase
            var links = new ObservableCollection<Link>();
            if (UpdateStoreLinks(links, tpkd))
            {
                gameInfo.Links.AddRange(links.ToList());
            }
            PlayniteApi.Database.BeginBufferUpdate();
            var game = PlayniteApi.Database.ImportGame(gameInfo, this);
            if (groupTag != null)
            {
                game.TagIds.Add(groupTag.Id);
                PlayniteApi.Database.Games.Update(game);
            }
            PlayniteApi.Database.EndBufferUpdate();
            return game;
        }

        // If a game had been redeemed since last added to Playnite, remove the tag 'Key: Unredeemed' and add the tag 'Key: Redeemed'
        // returns whether tags were updated or not 
        bool UpdateRedemptionStatus(Game existingGame, Order.TpkdDict.Tpk tpkd, Tag groupTag = null)
        {
            var recordChanged = false;
            if (existingGame == null) { return false; }
            if (!Settings.keyTypeWhitelist.Contains(tpkd.key_type)) { return false; }

            // process tags on existingGame only if there was a change in tag status
            
            var existingRedemptionTagIds = existingGame.Tags?.Where(t => PAST_TAGS.Contains(t.Name)).Select(tag => tag.Id);
            
            // This creates a new Tag in the Tag Database if it doesn't already exist for 'Tag: Redeemed'
            var redeemedTag = PlayniteApi.Database.Tags.Add(GetRedeemedTag(tpkd));
            if (existingRedemptionTagIds == null) return false;
            var tagIds = existingRedemptionTagIds.ToList();

            if (groupTag != null)
            {
                if (existingGame.Tags.All(tag => tag.Id != groupTag.Id))
                {
                    existingGame.TagIds.Add(groupTag.Id);
                    recordChanged = true;
                }
            }

            if (tagIds.Contains(redeemedTag.Id)) return recordChanged;

            existingGame.TagIds.RemoveAll(tagId => tagIds.Contains(tagId));
            existingGame.TagIds.Add(
                PlayniteApi.Database.Tags.Add(IsKeyPresent(tpkd) ? REDEEMED_STR : UNREDEEMED_STR).Id);
            
            return true;
        }

        #region === Helper Methods ============
        private static string GetGameId(Order.TpkdDict.Tpk tpk) => $"{tpk.machine_name}_{tpk.gamekey}";
        private static Link MakeLink(string gameKey) => new Link("Humble Purchase URL", string.Format(humblePurchaseUrlMask, gameKey) );
        private static Link MakeSteamLink(string gameKey) => new Link("Steam", string.Format(steamGameUrlMask, gameKey));
        private static bool IsKeyNull(Order.TpkdDict.Tpk t) => t?.redeemed_key_val == null;
        private static bool IsKeyPresent(Order.TpkdDict.Tpk t) => !IsKeyNull(t);
        private static string GetRedeemedTag(Order.TpkdDict.Tpk t) => IsKeyPresent(t) ? REDEEMED_STR : UNREDEEMED_STR;
        #endregion
    }
}